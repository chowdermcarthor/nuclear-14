// #Misfits Change
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DeltaV.MedicalRecords;
using Content.Shared.StationRecords;
using Robust.Shared.Timing;

namespace Content.Server.DeltaV.MedicalRecords;

public sealed class MedicalRecordsSystem : SharedMedicalRecordsSystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;

    private static readonly TimeSpan ExpirationTime = TimeSpan.FromMinutes(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(OnGeneralRecordCreated);
    }

    private void OnGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        _records.AddRecordEntry(ev.Key, new MedicalRecord());
        _records.Synchronize(ev.Key);
    }

    public void SetStatus(StationRecordKey key, MedicalRecord record)
    {
        if (_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            UpdateMedicalRecords(general.Name, record);

        _records.AddRecordEntry(key, record);
        _records.Synchronize(key);
    }

    public MedicalRecord? GetMedicalRecords(EntityUid patient)
    {
        _access.FindStationRecordKeys(patient, out var keys);
        foreach (var key in keys)
        {
            if (!_records.TryGetRecord<MedicalRecord>(key, out var record))
                continue;

            if (record.LastUpdated != null && (_timing.CurTime - record.LastUpdated.Value) >= ExpirationTime)
            {
                record = new MedicalRecord();
                SetStatus(key, record);
            }

            return record;
        }

        foreach (var key in keys)
        {
            var record = new MedicalRecord();
            SetStatus(key, record);
            return record;
        }

        return null;
    }

    public StationRecordKey? GetMedicalRecordsKey(EntityUid patient)
    {
        _access.FindStationRecordKeys(patient, out var keys);
        foreach (var key in keys)
        {
            if (_records.TryGetRecord<MedicalRecord>(key, out _))
                return key;
        }

        foreach (var key in keys)
        {
            SetStatus(key, new MedicalRecord());
            return key;
        }

        return null;
    }

    public void SetPatientStatus(StationRecordKey patient, TriageStatus status)
    {
        if (_records.TryGetRecord<MedicalRecord>(patient, out var record) && status != TriageStatus.None)
        {
            record.Status = status;
            record.LastUpdated = _timing.CurTime;
            SetStatus(patient, record);
            return;
        }

        SetStatus(patient, new MedicalRecord());
    }

    public void ClaimPatient(StationRecordKey patient, EntityUid claimer)
    {
        if (!_idCard.TryFindIdCard(claimer, out var idCard))
            return;

        if (!TryComp<AccessComponent>(idCard.Owner, out var access) || !access.Tags.Contains("Medical"))
            return;

        var claimerName = idCard.Comp.FullName;
        if (string.IsNullOrEmpty(claimerName))
            return;

        if (!_records.TryGetRecord<MedicalRecord>(patient, out var record))
            return;

        record.ClaimedName = record.ClaimedName == claimerName ? null : claimerName;
        record.LastUpdated = record.ClaimedName != null ? _timing.CurTime : null;
        SetStatus(patient, record);
    }
}