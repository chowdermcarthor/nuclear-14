// #Misfits Add - DISABLED: Aggro warning delay system caused mobs to follow without attacking
// and introduced server lag. The ping-on-aggro feature was moved into CombatModePingSystem.
// Kept commented out per project no-deletion policy.

// using Content.Server._Misfits.NPC.Components;
// using Content.Server.NPC.Components;
// using Content.Server.NPC.HTN;
// using Robust.Server.Audio;
// using Robust.Shared.Audio;
// using Robust.Shared.Timing;
//
// namespace Content.Server._Misfits.NPC.Systems;
//
// public sealed class AggroWarningSystem : EntitySystem
// {
//     [Dependency] private readonly AudioSystem _audio = default!;
//     [Dependency] private readonly IGameTiming _timing = default!;
//     [Dependency] private readonly SharedTransformSystem _transform = default!;
//
//     private const string WarningPingSound = "/Audio/Effects/toggleoncombat.ogg";
//     private const float PingMaxDistance = 10f;
//
//     public override void Initialize()
//     {
//         base.Initialize();
//         SubscribeLocalEvent<AggroWarningComponent, ComponentShutdown>(OnWarningShutdown);
//     }
//
//     private void OnWarningShutdown(EntityUid uid, AggroWarningComponent comp, ComponentShutdown args) { }
//
//     private void AttachWarning(EntityUid uid)
//     {
//         if (!HasComp<HTNComponent>(uid)) return;
//         if (HasComp<AggroWarningComponent>(uid)) return;
//         var warning = EnsureComp<AggroWarningComponent>(uid);
//         warning.TimeRemaining = 2f;
//         warning.PingPlayed = false;
//     }
//
//     public override void Update(float frameTime)
//     {
//         // ... (full implementation removed — see git history)
//     }
// }
