<#
.SYNOPSIS
Measures what Deskbridge costs while a client is connected and idle.

.DESCRIPTION
Starts the published relay (which starts its own broker on a private pipe) 5 times and times each
start from launch to a tools/list answer. Then starts it once more, waits 10 s for start-up work to
settle, and samples the relay and broker every SampleSeconds for Minutes. Prints a table and a
verdict against the budgets, then stops both processes. Uses a temporary config folder, never the
user's own.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File scripts/measure-idle.ps1 -Minutes 5
#>
param(
    [int]$Minutes = 5,
    [int]$SampleSeconds = 10
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$relayPath = Join-Path $root 'artifacts\relay\deskbridge.exe'
$brokerPath = Join-Path $root 'artifacts\broker\deskbridge-broker.exe'
if (-not (Test-Path $relayPath) -or -not (Test-Path $brokerPath)) { throw 'Publish first: powershell -ExecutionPolicy Bypass -File scripts/publish-broker.ps1' }
$configDir = Join-Path ([IO.Path]::GetTempPath()) ('deskbridge-measure-' + [guid]::NewGuid().ToString('N').Substring(0, 8))  # never the user's real config (D40)

$budgetBrokerCpu = 0.5   # percent of one core
$budgetBrokerMb = 80
$budgetRelayMb = 10

$budgetColdStartMs = 1000
$coldStartRuns = 5

# Starts the relay on a new private pipe (so it starts its own broker), completes the MCP handshake
# and a tools/list as a real client would, and returns both processes with the time it took.
function Start-Deskbridge {
    $suffix = 'measure-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $info = New-Object System.Diagnostics.ProcessStartInfo $relayPath, 'mcp'
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.EnvironmentVariables['DESKBRIDGE_PIPE_SUFFIX'] = $suffix
    $info.EnvironmentVariables['DESKBRIDGE_BROKER_PATH'] = $brokerPath
    $info.EnvironmentVariables['DESKBRIDGE_CONFIG_DIR'] = $configDir

    $startedAt = Get-Date
    $relay = [System.Diagnostics.Process]::Start($info)
    $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"measure-idle","version":"1.0"}}}')
    $relay.StandardInput.Flush()
    $null = $relay.StandardOutput.ReadLine()
    $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $relay.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list"}')
    $relay.StandardInput.Flush()
    $toolList = $relay.StandardOutput.ReadLine()
    if ($toolList -notmatch 'windows_list') { throw "tools/list did not answer as expected: $toolList" }
    $ms = [int]((Get-Date) - $startedAt).TotalMilliseconds

    $broker = Get-Process -Name 'deskbridge-broker' | Where-Object { $_.StartTime -ge $startedAt.AddSeconds(-1) } | Select-Object -First 1
    if ($null -eq $broker) { throw 'No broker started.' }
    [pscustomobject]@{ Relay = $relay; Broker = $broker; Ms = $ms }
}

function Stop-Deskbridge($run) {
    $run.Relay.StandardInput.Close()
    Start-Sleep -Milliseconds 800
    if (-not $run.Relay.HasExited) { $run.Relay.Kill() }
    if (-not $run.Broker.HasExited) { $run.Broker.Kill() }
}

$coldStarts = @()
for ($i = 1; $i -le $coldStartRuns; $i++) {
    $run = Start-Deskbridge
    $coldStarts += $run.Ms
    Write-Host "Cold start $i of ${coldStartRuns}: $($run.Ms) ms"
    Stop-Deskbridge $run
}
$coldStartMedian = ($coldStarts | Sort-Object)[[int][math]::Floor($coldStarts.Count / 2)]

$run = Start-Deskbridge
$relay = $run.Relay
$broker = $run.Broker
$handshakeMs = $run.Ms

Write-Host "Relay $($relay.Id) and broker $($broker.Id) ready in $handshakeMs ms. Settling for 10 s, then sampling for $Minutes min."
Start-Sleep -Seconds 10

function Sample($process) {
    $process.Refresh()
    [pscustomobject]@{
        Cpu        = $process.TotalProcessorTime.TotalMilliseconds
        WorkingMb  = [math]::Round($process.WorkingSet64 / 1MB, 1)
        PrivateMb  = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
    }
}

$clock = [System.Diagnostics.Stopwatch]::StartNew()
$brokerStart = Sample $broker
$relayStart = Sample $relay
$brokerPeak = 0.0
$relayPeak = 0.0
$rows = @()
while ($clock.Elapsed.TotalMinutes -lt $Minutes) {
    Start-Sleep -Seconds $SampleSeconds
    $b = Sample $broker
    $r = Sample $relay
    $brokerPeak = [math]::Max($brokerPeak, $b.WorkingMb)
    $relayPeak = [math]::Max($relayPeak, $r.WorkingMb)
    $rows += [pscustomobject]@{
        Seconds          = [int]$clock.Elapsed.TotalSeconds
        'Broker CPU ms'  = [int]($b.Cpu - $brokerStart.Cpu)
        'Broker MB'      = $b.WorkingMb
        'Broker private' = $b.PrivateMb
        'Relay CPU ms'   = [int]($r.Cpu - $relayStart.Cpu)
        'Relay MB'       = $r.WorkingMb
        'Relay private'  = $r.PrivateMb
    }
}

$wallMs = $clock.Elapsed.TotalMilliseconds
$brokerEnd = Sample $broker
$relayEnd = Sample $relay
$brokerCpuPct = [math]::Round(($brokerEnd.Cpu - $brokerStart.Cpu) / $wallMs * 100, 3)
$relayCpuPct = [math]::Round(($relayEnd.Cpu - $relayStart.Cpu) / $wallMs * 100, 3)

$rows | Format-Table -AutoSize | Out-String | Write-Host

function Verdict($ok) { if ($ok) { 'within budget' } else { 'OVER BUDGET' } }
Write-Host ''
Write-Host '| Measure | Measured | Budget | Verdict |'
Write-Host '|---|---|---|---|'
Write-Host ("| Broker idle CPU | {0}% of one core | under {1}% | {2} |" -f $brokerCpuPct, $budgetBrokerCpu, (Verdict ($brokerCpuPct -lt $budgetBrokerCpu)))
Write-Host ("| Broker idle memory (peak working set) | {0} MB | under {1} MB | {2} |" -f $brokerPeak, $budgetBrokerMb, (Verdict ($brokerPeak -lt $budgetBrokerMb)))
Write-Host ("| Broker private bytes (end) | {0} MB | | |" -f $brokerEnd.PrivateMb)
Write-Host ("| Relay memory (private bytes) | {0} MB | under {1} MB | {2} |" -f $relayEnd.PrivateMb, $budgetRelayMb, (Verdict ($relayEnd.PrivateMb -lt $budgetRelayMb)))
Write-Host ("| Relay working set (peak; mostly shared Windows code) | {0} MB | | |" -f $relayPeak)
Write-Host ("| Relay idle CPU | {0}% of one core | | |" -f $relayCpuPct)
Write-Host ("| Cold start, relay to tool list (median of {0}: {1}) | {2} ms | under {3} ms | {4} |" -f $coldStartRuns, ($coldStarts -join ', '), $coldStartMedian, $budgetColdStartMs, (Verdict ($coldStartMedian -lt $budgetColdStartMs)))

Stop-Deskbridge $run
if (Test-Path $configDir) { Remove-Item -Recurse -Force $configDir }
