<#
.SYNOPSIS
Publishes the broker precompiled (ReadyToRun) and self-contained into artifacts/broker, and the relay
with Native AOT into artifacts/relay.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File scripts/publish-broker.ps1
#>
param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:PATH += ";${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"  # Native AOT finds the linker through vswhere

dotnet publish (Join-Path $root 'src\Deskbridge.Broker') -c Release -r $Runtime --self-contained `
    -p:PublishReadyToRun=true -o (Join-Path $root 'artifacts\broker')
if ($LASTEXITCODE -ne 0) { throw 'Publishing the broker failed.' }

dotnet publish (Join-Path $root 'src\Deskbridge.Relay') -c Release -r $Runtime -o (Join-Path $root 'artifacts\relay')
if ($LASTEXITCODE -ne 0) { throw 'Publishing the relay failed.' }

$broker = Get-ChildItem (Join-Path $root 'artifacts\broker') -Recurse | Measure-Object -Property Length -Sum
'Broker folder: {0:N1} MB in {1} files' -f ($broker.Sum / 1MB), $broker.Count
'Relay: {0:N1} MB' -f ((Get-Item (Join-Path $root 'artifacts\relay\deskbridge.exe')).Length / 1MB)
