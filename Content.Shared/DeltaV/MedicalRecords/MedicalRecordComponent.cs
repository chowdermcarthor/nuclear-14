// #Misfits Change
using Robust.Shared.GameStates;

namespace Content.Shared.DeltaV.MedicalRecords;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMedicalRecordsSystem))]
public sealed partial class MedicalRecordComponent : Component
{
    [DataField, AutoNetworkedField]
    public MedicalRecord Record = new();
}