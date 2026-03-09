// #Misfits Change
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;

namespace Content.Shared.DeltaV.MedicalRecords;

public abstract class SharedMedicalRecordsSystem : EntitySystem
{
    public void UpdateMedicalRecords(string name, MedicalRecord status)
    {
        var query = EntityQueryEnumerator<IdentityComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (!Identity.Name(uid, EntityManager).Equals(name))
                continue;

            if (status is { Status: TriageStatus.None, ClaimedName: null })
            {
                RemComp<MedicalRecordComponent>(uid);
                continue;
            }

            EnsureComp<MedicalRecordComponent>(uid, out var record);
            record.Record = status;
            Dirty(uid, record);
        }
    }
}