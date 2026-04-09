using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Internal server event raised when a legendary hunt creature is actually destroyed.
/// This avoids multiple systems directly subscribing to DestructionEventArgs.
/// </summary>
public sealed class LegendaryCreatureKilledEvent : EntityEventArgs
{
}
