// #Misfits Add - Job-slot reclaim after death + same-round respawn lock for dead characters.
// Goal:
//   1. When a player dies in an occupied job slot, after a configurable delay (default 15 min)
//      the slot is automatically re-opened for late-join.
//   2. While the round lasts, that (player, character name) pair cannot rejoin through
//      normal spawn flow using the SAME character that died. They must pick a different
//      character. This prevents "die -> /respawn -> pick same character -> re-enter round".
//   3. Locks are cleared ONLY by:
//        - Revival of the same mob out of Dead state (medical / clone success).
//        - Entering cryo (handled in CryostorageSystem via ClearLockFor).
//        - Admin 'respawn' command (handled in RespawnCommand via ClearLocksFor).
//        - Round restart.
//
// Identity: Lock key is (NetUserId, CharacterName). Character name is resolved from
// MindComponent on the dying mob, so mind transfers cannot bypass the lock — possessing
// a new body and dying there still locks the original character name on the player.

using System.Collections.Generic;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Misfits.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.JobSlotReclaim;

/// <summary>
/// Tracks dead-slot reclaim timers and per-character "died this round" locks.
/// </summary>
public sealed class MisfitsJobSlotReclaimSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    // Mob -> slot metadata, recorded at spawn. Used to know which slot to free on death.
    private readonly Dictionary<EntityUid, SlotRecord> _occupied = new();

    // Pending reclaims: mob is dead, timer running.
    private readonly Dictionary<EntityUid, PendingReclaim> _pending = new();

    // Same-round respawn locks, keyed by (NetUserId, CharacterName).
    private readonly HashSet<(NetUserId, string)> _locks = new();

    // Batched scan cadence so we don't hammer the dictionary every tick.
    private TimeSpan _nextScan;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // #Misfits Fix - Use broadcast subscription to avoid duplicate directed <MobStateComponent, MobStateChangedEvent>
        //               registration conflict with SharedStunSystem (RobustToolbox only allows one subscriber per pair).
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0 || _timing.CurTime < _nextScan)
            return;

        _nextScan = _timing.CurTime + ScanInterval;

        var delay = TimeSpan.FromSeconds(_cfg.GetCVar(JobSlotReclaimCVars.JobSlotReclaimSeconds));
        var now = _timing.CurTime;

        // Collect expired entries first so we can mutate the dict.
        List<EntityUid>? expired = null;
        foreach (var (mob, entry) in _pending)
        {
            if (now - entry.DeathTime < delay)
                continue;
            expired ??= new List<EntityUid>();
            expired.Add(mob);
        }

        if (expired == null)
            return;

        foreach (var mob in expired)
        {
            var entry = _pending[mob];
            _pending.Remove(mob);

            // Only try to re-open the slot if the station still exists. Unlimited
            // slots (null count) will no-op inside TryAdjustJobSlot safely.
            if (Exists(entry.Slot.Station))
            {
                _stationJobs.TryAdjustJobSlot(entry.Slot.Station, entry.Slot.JobId, 1,
                    createSlot: false, clamp: true);
            }

            _chat.SendAdminAlert(Loc.GetString(
                "misfits-job-slot-reclaim-admin-alert",
                ("character", entry.Slot.CharacterName),
                ("job", entry.Slot.JobId)));

            _adminLog.Add(LogType.Respawn, LogImpact.Low,
                $"Reclaimed job slot '{entry.Slot.JobId}' on station {entry.Slot.Station} " +
                $"from dead character '{entry.Slot.CharacterName}' (user {entry.Slot.UserId}). " +
                $"Character remains locked for this round.");
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // Only track real job spawns on a real station. Observers, ghost-roles, and
        // special rule spawns (where JobId is null or Station == Invalid) don't count.
        if (ev.JobId == null || ev.Station == EntityUid.Invalid)
            return;

        _occupied[ev.Mob] = new SlotRecord(
            ev.Station,
            ev.JobId,
            ev.Player.UserId,
            ev.Profile.Name);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            HandleDeath(args.Target);
            return;
        }

        if (args.OldMobState == MobState.Dead && args.NewMobState != MobState.Dead)
        {
            HandleRevive(args.Target);
        }
    }

    private void HandleDeath(EntityUid mob)
    {
        // Resolve (userId, characterName) from the mob's mind. This catches mind
        // transfers — the original player keeps their lock even if they possessed
        // a different body and died there.
        if (!TryResolveCharacter(mob, out var userId, out var characterName))
            return;

        _locks.Add((userId, characterName));

        // If this mob was a recorded job-slot occupant, start the reclaim timer.
        if (_occupied.Remove(mob, out var slot))
        {
            // Promote to pending with death time.
            _pending[mob] = new PendingReclaim(slot, _timing.CurTime);
        }
    }

    private void HandleRevive(EntityUid mob)
    {
        // Cancel any pending reclaim for this mob — they came back.
        if (_pending.Remove(mob, out var pending))
        {
            // Put the slot record back so a future death still triggers the timer.
            _occupied[mob] = pending.Slot;
        }

        // Clear the same-round lock for the character currently riding this mob.
        if (TryResolveCharacter(mob, out var userId, out var characterName))
            _locks.Remove((userId, characterName));
    }

    private bool TryResolveCharacter(EntityUid mob, out NetUserId userId, out string characterName)
    {
        userId = default;
        characterName = string.Empty;

        if (!TryComp<MindContainerComponent>(mob, out var container))
            return false;
        if (!_mind.TryGetMind(mob, out var mindId, out var mind, container))
            return false;
        if (mind.UserId == null || string.IsNullOrWhiteSpace(mind.CharacterName))
            return false;

        userId = mind.UserId.Value;
        characterName = mind.CharacterName!;
        return true;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _occupied.Clear();
        _pending.Clear();
        _locks.Clear();
        _nextScan = TimeSpan.Zero;
    }

    #region Public API

    /// <summary>
    /// Returns true if the given (player, character name) pair is locked for this round.
    /// </summary>
    public bool IsCharacterLocked(NetUserId userId, string? characterName)
    {
        if (!_cfg.GetCVar(JobSlotReclaimCVars.JobSlotReclaimLockEnabled))
            return false;
        if (string.IsNullOrWhiteSpace(characterName))
            return false;
        return _locks.Contains((userId, characterName));
    }

    /// <summary>
    /// Clears the lock for a specific (player, character). Called by CryostorageSystem
    /// when a character enters cryo — they're removed from the round cleanly, so the
    /// character is free to rejoin.
    /// </summary>
    public void ClearLockFor(NetUserId userId, string? characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return;
        _locks.Remove((userId, characterName));
    }

    /// <summary>
    /// Removes a mob from all reclaim tracking. Called by CryostorageSystem when a mob
    /// enters cryostorage — the slot was already freed by the vanilla cryo path, so we
    /// must not free it a second time if the mob later dies on the PausedMap.
    /// </summary>
    public void ClearMobTracking(EntityUid mob)
    {
        _occupied.Remove(mob);
        _pending.Remove(mob);
    }

    /// <summary>
    /// Clears all locks for a given player. Called by the admin 'respawn' command so
    /// admins can force a respawn regardless of which character the player picks.
    /// </summary>
    public void ClearLocksFor(NetUserId userId)
    {
        _locks.RemoveWhere(k => k.Item1 == userId);
    }

    #endregion

    private readonly record struct SlotRecord(
        EntityUid Station,
        string JobId,
        NetUserId UserId,
        string CharacterName);

    private readonly record struct PendingReclaim(SlotRecord Slot, TimeSpan DeathTime);
}
