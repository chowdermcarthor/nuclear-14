// #Misfits Add — BUI key and messages for the Spirit Board response selection UI.

using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.SpiritBoard;

/// <summary>
/// UiKey for the ghost response-selection popup.
/// </summary>
[Serializable, NetSerializable]
public enum SpiritBoardUiKey : byte
{
    Key,
}

/// <summary>
/// Sent from ghost client → server when the ghost selects a response from the board UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class SpiritBoardSelectResponseMessage : BoundUserInterfaceMessage
{
    /// <summary>
    /// The chosen response token (e.g., "YES", "NO", "A", "GOODBYE").
    /// </summary>
    public readonly string Response;

    public SpiritBoardSelectResponseMessage(string response)
    {
        Response = response;
    }
}
