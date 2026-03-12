using Content.Shared.Objectives;
using Robust.Shared.Serialization;

namespace Content.Shared.CharacterInfo;

[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;

    public RequestCharacterInfoEvent(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public readonly NetEntity NetEntity;
    public readonly string JobTitle;
    public readonly Dictionary<string, List<ObjectiveInfo>> Objectives;
    public readonly string? Briefing;
    public readonly List<string> Special;

    // #Misfits Add - Persistent player data (SPECIAL stats, lifetime counters, history log)
    public CharacterPersistentStats? PersistentStats { get; set; }

    public CharacterInfoEvent(NetEntity netEntity, string jobTitle, Dictionary<string, List<ObjectiveInfo>> objectives, string? briefing, List<string> special)
    {
        NetEntity = netEntity;
        JobTitle = jobTitle;
        Objectives = objectives;
        Briefing = briefing;
        Special = special;
    }
}

/// <summary>
/// Serialisable snapshot of a character's persistent SPECIAL stats, lifetime statistics, and history log.
/// Sent from server → client inside <see cref="CharacterInfoEvent"/>.
/// </summary>
// #Misfits Add
[Serializable, NetSerializable]
public sealed class CharacterPersistentStats
{
    // SPECIAL
    public int Strength;
    public int Perception;
    public int Endurance;
    public int Charisma;
    public int Agility;
    public int Intelligence;
    public int Luck;

    // Lifetime stats
    public int MobKills;
    public int Deaths;
    public int RoundsPlayed;

    // History log
    public List<string> HistoryLog = new();

    // Allocation confirmation
    public bool StatsConfirmed;

    // #Misfits Add - Currency wallet balance (populated server-side from PersistentCurrencyComponent)
    public int Bottlecaps;
}
