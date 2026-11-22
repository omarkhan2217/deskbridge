using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;

namespace Deskbridge.Core.Policy;

/// <summary>
/// Decides every call, in this order: paused, client approval, never available, app off, then by effect.
/// Read is allowed. Change asks (a remembered grant skips the question in an Act app). Destructive always asks.
/// The engine never consults a model, and every refusal says what the user can change.
/// </summary>
public sealed class PolicyEngine(ConfigStore config, ClientRegistry clients) : IToolPolicy
{
    public PolicyDecision Decide(ToolCall call, ClientContext client)
    {
        var settings = config.Current;
        if (settings.Paused)
        {
            return new PolicyDecision.Deny("Deskbridge is paused. Resume it from the tray icon.");
        }

        if (!clients.IsApproved(client.Process))
        {
            return new PolicyDecision.Confirm(ClientApproval(call, client));
        }

        if (NeverAvailable.Reason(call) is { } never)
        {
            return new PolicyDecision.Deny(never);
        }

        var app = call.App.DisplayName;
        var level = config.Level(call.App);
        if (level == PermissionLevel.Off)
        {
            return new PolicyDecision.Deny($"Deskbridge is off for {app}. Change it in Deskbridge settings.");
        }

        switch (call.Effect)
        {
            case ToolEffect.Read:
                return new PolicyDecision.Allow();

            case ToolEffect.Change when level == PermissionLevel.Act:
                return HasGrant(settings, call, client)
                    ? new PolicyDecision.Allow(Remembered: true)
                    : new PolicyDecision.Confirm(Ask(call, client, canRemember: true, note: null));

            case ToolEffect.Change:
                return new PolicyDecision.Confirm(Ask(call, client, canRemember: false,
                    note: $"{app} is read-only in Deskbridge. Allowing lets this one change through."));

            case ToolEffect.Destructive when level == PermissionLevel.Act:
                return new PolicyDecision.Confirm(Ask(call, client, canRemember: false, note: null));

            default:
                return new PolicyDecision.Deny(
                    $"{app} is read-only in Deskbridge, and deleting or sending needs Act. Change it in Deskbridge settings.");
        }
    }

    public void Record(ToolCall call, ClientContext client, ConfirmationRequest request, ConfirmationAnswer answer)
    {
        var allowed = answer is ConfirmationAnswer.Allow or ConfirmationAnswer.AllowAndRemember;
        if (request.Kind == ConfirmationKind.ClientApproval && allowed)
        {
            clients.Approve(client.Process);
            return;
        }

        if (request.Kind == ConfirmationKind.ToolCall && answer == ConfirmationAnswer.AllowAndRemember
            && call.Effect == ToolEffect.Change && request.CanRemember)
        {
            config.Update(c => HasGrant(c, call, client)
                ? c
                : c with { Grants = c.Grants.Append(new Grant(client.Key, call.App.Key, DateTimeOffset.Now)).ToList() });
        }
    }

    static bool HasGrant(DeskbridgeConfig settings, ToolCall call, ClientContext client) =>
        settings.Grants.Any(g => g.Client == client.Key && g.App == call.App.Key);

    static ConfirmationRequest Ask(ToolCall call, ClientContext client, bool canRemember, string? note) =>
        new(ConfirmationKind.ToolCall, client.Name, call.App, call.Action, call.Content, call.Effect, canRemember, note);

    ConfirmationRequest ClientApproval(ToolCall call, ClientContext client)
    {
        var path = client.Process.Path;
        var content = path is null
            ? $"Process ID {client.Process.Pid}. Deskbridge could not see which program this is."
            : $"{path}\nPublisher: {clients.PublisherOf(path) ?? "not signed"}";
        return new ConfirmationRequest(ConfirmationKind.ClientApproval, client.Name, call.App,
            $"{client.Name} wants to use Deskbridge.", content, call.Effect, CanRemember: false,
            Note: "Allow once to let this program use Deskbridge from now on. You can remove it in Deskbridge settings.");
    }
}
