// #Misfits Change - Ported from Delta-V surgery contamination system
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Surgery.Contamination;

[Serializable, NetSerializable]
public sealed partial class SurgeryCleanDirtDoAfterEvent : SimpleDoAfterEvent;
