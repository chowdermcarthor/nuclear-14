// #Misfits Change
using Robust.Shared.Serialization;

namespace Content.Shared.DeltaV.MedicalRecords;

/// <summary>
/// Status used in medical records triage.
/// </summary>
[Serializable, NetSerializable]
public enum TriageStatus : byte
{
    None,
    Minor,
    Delayed,
    Immediate,
    Expectant,
}

/// <summary>
/// Medical record entry attached to a station record.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class MedicalRecord
{
    [DataField]
    public TriageStatus Status = TriageStatus.None;

    [DataField]
    public string? ClaimedName;

    [DataField]
    public TimeSpan? LastUpdated;
}