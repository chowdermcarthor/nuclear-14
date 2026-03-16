using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Trade;

// Tracks per-player contract tier unlock progress for the current round.
// Attached to a player entity on first interaction with any tier-enabled trade vendor.
[RegisterComponent, NetworkedComponent]
public sealed partial class NcTierProgressComponent : Component
{
    // Ordered list of all six tiers from lowest to highest.
    public static readonly string[] AllTiers =
        { "Bronze", "Iron", "Silver", "Gold", "Mithril", "Diamond" };

    // Number of contracts a player must complete in a tier before the next tier unlocks.
    public const int ContractsToAdvance = 3;

    // Tiers this player currently has access to (starting empty; Bronze is granted on first vendor access).
    [ViewVariables]
    public HashSet<string> UnlockedTiers { get; } = new();

    // How many contracts have been completed per tier this round.
    [ViewVariables]
    public Dictionary<string, int> CompletedByTier { get; } = new();
}
