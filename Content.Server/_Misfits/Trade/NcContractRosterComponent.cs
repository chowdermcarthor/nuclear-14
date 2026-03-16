using Content.Shared._Misfits.Trade;

namespace Content.Server._Misfits.Trade;

// Attach to a trade vendor entity to track which players have engaged with contracts
// and their highest achieved tier this round. Data is not persisted between rounds.
[RegisterComponent]
public sealed partial class NcContractRosterComponent : Component
{
    private readonly Dictionary<string, TierRosterEntry> _entries = new();

    // Returns a snapshot list of all current roster entries for network serialisation.
    public List<TierRosterEntry> GetSnapshot()
    {
        var list = new List<TierRosterEntry>(_entries.Count);
        list.AddRange(_entries.Values);
        return list;
    }

    // Creates a new entry or updates an existing one for the given player name.
    public void UpdateEntry(string name, string highestTier, int total)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            _entries[name] = new TierRosterEntry(name, highestTier, total);
            return;
        }

        entry.HighestTier = highestTier;
        entry.TotalCompleted = total;
    }
}
