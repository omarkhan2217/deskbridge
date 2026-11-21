namespace Deskbridge.Core;

/// <summary>
/// An error whose message is safe to show to the user and to the agent.
/// </summary>
public class DeskbridgeException(string userMessage) : Exception(userMessage);
