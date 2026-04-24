using Content.Shared.Actions;

namespace Content.Shared._Misfits.TribalHunt;

/// <summary>
/// Raised when the tribal elder starts a new hunt contract.
/// </summary>
public sealed partial class PerformTribalStartHuntActionEvent : InstantActionEvent;

/// <summary>
/// Raised when a tribal participant starts a minor hunt.
/// </summary>
public sealed partial class PerformTribalStartMinorHuntActionEvent : InstantActionEvent;

/// <summary>
/// Raised when a tribal participant offers a trophy to the active hunt.
/// </summary>
public sealed partial class PerformTribalOfferTrophyActionEvent : InstantActionEvent;

/// <summary>
/// Raised when a tribal participant toggles the hunt tracker GUI.
/// </summary>
public sealed partial class PerformTribalToggleHuntGuiActionEvent : InstantActionEvent;
