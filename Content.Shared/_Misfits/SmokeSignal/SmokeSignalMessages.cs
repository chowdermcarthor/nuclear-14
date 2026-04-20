// #Misfits Add — BUI key and messages for the Smoke Signal text-input UI.

using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.SmokeSignal;

/// <summary>
/// UI key for the smoke signal text input window.
/// </summary>
[Serializable, NetSerializable]
public enum SmokeSignalUiKey : byte
{
    Key,
}

/// <summary>
/// Sent from client → server when the player confirms a smoke signal message.
/// </summary>
[Serializable, NetSerializable]
public sealed class SmokeSignalSendMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The message text to broadcast.
    /// </summary>
    public readonly string Message;

    public SmokeSignalSendMessage(string message)
    {
        Message = message;
    }
}
