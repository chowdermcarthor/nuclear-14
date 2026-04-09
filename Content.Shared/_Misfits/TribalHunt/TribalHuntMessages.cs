using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.TribalHunt;

/// <summary>
/// Full tribal hunt UI state snapshot sent from the server.
/// </summary>
[Serializable, NetSerializable]
public sealed class TribalHuntUiState
{
    public bool Active;
    public int Offered;
    public int Required;
    public int SecondsRemaining;
    public string StatusText = string.Empty;
    public string CoordinatesText = string.Empty;
    public bool CoordinatesKnown;
    public bool JoinWindowOpen;
    public bool CanJoin;
    public bool IsJoined;
    public int JoinedHunters;
}

/// <summary>
/// Server -> client tribal hunt UI update event.
/// </summary>
[Serializable, NetSerializable]
public sealed class TribalHuntUiUpdateEvent : EntityEventArgs
{
    public TribalHuntUiState State = new();
}

/// <summary>
/// Client -> server request to join the currently gathering tribal hunt.
/// </summary>
[Serializable, NetSerializable]
public sealed class TribalHuntJoinRequestEvent : EntityEventArgs;

/// <summary>
/// Server -> client request to toggle the hunt tracker GUI.
/// </summary>
[Serializable, NetSerializable]
public sealed class TribalHuntToggleWindowEvent : EntityEventArgs;