// #Misfits Add - Server-side group (/party) system.
// Players can form small ephemeral groups using /group command.
// Group state is entirely server-side (no DB, cleared on round restart).
// Server broadcasts overlay updates every 2s to members who opt-in.

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Misfits.Group;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Group;

/// <summary>
/// Manages ephemeral player groups. Groups are lost on round restart.
/// Max 8 members, invite expires after 60 seconds.
/// Only the leader can invite/kick; any member can leave.
/// Leadership transfers to the oldest remaining member when the leader leaves.
/// </summary>
public sealed class GroupSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming    _timing        = default!;

    private const int MaxGroupSize = 8;
    private static readonly TimeSpan InviteExpiry       = TimeSpan.FromSeconds(60);
    private const float OverlayBroadcastInterval = 2.0f;

    // ── Inner types ────────────────────────────────────────────────────────

    private sealed class GroupData
    {
        public int   Id;
        public NetUserId LeaderUserId;
        /// <summary>Members in join order (leader is always first).</summary>
        public List<NetUserId> MemberOrder = new();
    }

    private sealed class PendingInvite
    {
        public NetUserId InviterUserId;
        public NetUserId TargetUserId;
        public int       GroupId;
        public TimeSpan  ExpiresAt;
    }

    // ── State ──────────────────────────────────────────────────────────────

    private readonly Dictionary<int, GroupData>       _groups       = new();
    private readonly Dictionary<NetUserId, int>       _playerToGroup = new();
    private readonly List<PendingInvite>              _pendingInvites = new();
    private readonly HashSet<NetUserId>               _overlayEnabled = new();
    private int _nextGroupId = 1;
    private float _overlayAccumulator;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GroupOpenPanelRequestEvent>(OnOpenPanel);
        SubscribeNetworkEvent<GroupCreateRequestEvent>(OnCreate);
        SubscribeNetworkEvent<GroupInviteRequestEvent>(OnInvite);
        SubscribeNetworkEvent<GroupInviteResponseEvent>(OnInviteResponse);
        SubscribeNetworkEvent<GroupLeaveRequestEvent>(OnLeave);
        SubscribeNetworkEvent<GroupKickRequestEvent>(OnKick);
        SubscribeNetworkEvent<GroupToggleOverlayRequestEvent>(OnToggleOverlay);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Expire outdated invites.
        var now = _timing.CurTime;
        _pendingInvites.RemoveAll(inv => inv.ExpiresAt < now);

        // Periodic overlay broadcast.
        _overlayAccumulator += frameTime;
        if (_overlayAccumulator < OverlayBroadcastInterval)
            return;
        _overlayAccumulator = 0f;

        BroadcastOverlays();
    }

    // ── Public API (used by WastelandMapSystem) ────────────────────────────

    /// <summary>
    /// Returns the EntityUids of group members for a given actor (for WastelandMap blip injection).
    /// Returns null if the actor is not in a group or has no AttachedEntity.
    /// </summary>
    public List<EntityUid>? GetGroupMemberEntities(EntityUid actor)
    {
        // Find which session this entity belongs to.
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity != actor)
                continue;

            if (!_playerToGroup.TryGetValue(session.UserId, out var groupId))
                return null;

            if (!_groups.TryGetValue(groupId, out var group))
                return null;

            var result = new List<EntityUid>();
            foreach (var memberId in group.MemberOrder)
            {
                if (!TryGetSession(memberId, out var memberSession) || memberSession == null)
                    continue;
                if (memberSession.AttachedEntity is { } ent)
                    result.Add(ent);
            }
            return result;
        }

        return null;
    }

    // ── Overlay broadcast ─────────────────────────────────────────────────

    private void BroadcastOverlays()
    {
        foreach (var (_, group) in _groups)
        {
            // Pre-build the members dict for this group.
            var memberEntities = new Dictionary<NetEntity, string>();
            foreach (var memberId in group.MemberOrder)
            {
                if (!TryGetSession(memberId, out var ms) || ms == null)
                    continue;
                if (ms.AttachedEntity is not { } ent)
                    continue;
                memberEntities[GetNetEntity(ent)] = Name(ent);
            }

            foreach (var memberId in group.MemberOrder)
            {
                if (!TryGetSession(memberId, out var ms) || ms == null)
                    continue;

                // Members with overlay enabled get the full dict; others get empty to clear stale state.
                var dict = _overlayEnabled.Contains(memberId)
                    ? memberEntities
                    : new Dictionary<NetEntity, string>();

                RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = dict }, ms);
            }
        }
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    private void OnOpenPanel(GroupOpenPanelRequestEvent msg, EntitySessionEventArgs args)
    {
        SendStateTo(args.SenderSession);
    }

    private void OnCreate(GroupCreateRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (_playerToGroup.ContainsKey(userId))
        {
            SendResult(session, false, Loc.GetString("group-already-in-group"));
            return;
        }

        var id    = _nextGroupId++;
        var group = new GroupData
        {
            Id           = id,
            LeaderUserId = userId,
            MemberOrder  = new List<NetUserId> { userId },
        };

        _groups[id]          = group;
        _playerToGroup[userId] = id;

        BroadcastStateToGroup(id);
        SendResult(session, true, Loc.GetString("group-created"));
    }

    private void OnInvite(GroupInviteRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];

        if (group.LeaderUserId != userId)
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        if (group.MemberOrder.Count >= MaxGroupSize)
        {
            SendResult(session, false, Loc.GetString("group-full"));
            return;
        }

        // Resolve target by character name.
        if (!TryGetSessionByCharacterName(msg.TargetCharacterName, out var targetSession) || targetSession == null)
        {
            SendResult(session, false, Loc.GetString("group-player-not-found", ("name", msg.TargetCharacterName)));
            return;
        }

        var targetUserId = targetSession.UserId;

        if (_playerToGroup.ContainsKey(targetUserId))
        {
            SendResult(session, false, Loc.GetString("group-target-already-in-group", ("name", msg.TargetCharacterName)));
            return;
        }

        // Replace any existing invite to this target from this group.
        _pendingInvites.RemoveAll(i => i.GroupId == groupId && i.TargetUserId == targetUserId);

        _pendingInvites.Add(new PendingInvite
        {
            InviterUserId = userId,
            TargetUserId  = targetUserId,
            GroupId       = groupId,
            ExpiresAt     = _timing.CurTime + InviteExpiry,
        });

        // Send state update to the target with invite info populated.
        var inviterName = session.AttachedEntity is { } ie ? Name(ie) : session.Name;
        var inviteState = new GroupStateUpdateEvent
        {
            PendingInviteFromName   = inviterName,
            PendingInviteFromUserId = userId,
        };
        RaiseNetworkEvent(inviteState, targetSession);

        SendResult(session, true, Loc.GetString("group-invite-sent", ("name", msg.TargetCharacterName)));
    }

    private void OnInviteResponse(GroupInviteResponseEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        var invite = _pendingInvites.FirstOrDefault(i =>
            i.TargetUserId == userId && i.InviterUserId == msg.InviterUserId);

        if (invite == null)
        {
            SendResult(session, false, Loc.GetString("group-invite-expired"));
            return;
        }

        _pendingInvites.Remove(invite);

        if (!msg.Accept)
        {
            // Clear invite state on the declining player.
            RaiseNetworkEvent(new GroupStateUpdateEvent(), session);
            // Notify the inviter.
            if (TryGetSession(invite.InviterUserId, out var inviterSession) && inviterSession != null)
            {
                var targetName = session.AttachedEntity is { } te ? Name(te) : session.Name;
                SendResult(inviterSession, false, Loc.GetString("group-invite-declined", ("name", targetName)));
            }
            return;
        }

        // Accept path.
        if (!_groups.TryGetValue(invite.GroupId, out var group))
        {
            SendResult(session, false, Loc.GetString("group-not-found"));
            return;
        }

        if (group.MemberOrder.Count >= MaxGroupSize)
        {
            SendResult(session, false, Loc.GetString("group-full"));
            return;
        }

        if (_playerToGroup.ContainsKey(userId))
        {
            SendResult(session, false, Loc.GetString("group-already-in-group"));
            return;
        }

        group.MemberOrder.Add(userId);
        _playerToGroup[userId] = invite.GroupId;

        BroadcastStateToGroup(invite.GroupId);
        SendResult(session, true, Loc.GetString("group-joined"));
    }

    private void OnLeave(GroupLeaveRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        // Clear overlay for this player.
        _overlayEnabled.Remove(userId);
        RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = new Dictionary<NetEntity, string>() }, session);

        RemoveMemberFromGroup(userId, groupId);
        SendResult(session, true, Loc.GetString("group-left"));
    }

    private void OnKick(GroupKickRequestEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var userId  = session.UserId;

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
        {
            SendResult(session, false, Loc.GetString("group-not-in-group"));
            return;
        }

        var group = _groups[groupId];

        if (group.LeaderUserId != userId)
        {
            SendResult(session, false, Loc.GetString("group-not-leader"));
            return;
        }

        if (!TryGetSessionByCharacterName(msg.TargetCharacterName, out var targetSession) || targetSession == null)
        {
            SendResult(session, false, Loc.GetString("group-player-not-found", ("name", msg.TargetCharacterName)));
            return;
        }

        var targetUserId = targetSession.UserId;

        if (targetUserId == userId)
        {
            SendResult(session, false, Loc.GetString("group-cannot-kick-self"));
            return;
        }

        if (!_playerToGroup.TryGetValue(targetUserId, out var targetGroupId) || targetGroupId != groupId)
        {
            SendResult(session, false, Loc.GetString("group-target-not-in-group", ("name", msg.TargetCharacterName)));
            return;
        }

        // Clear overlay and state for the kicked player.
        _overlayEnabled.Remove(targetUserId);
        RaiseNetworkEvent(new GroupOverlayUpdateEvent { GroupMembers = new Dictionary<NetEntity, string>() }, targetSession);
        RaiseNetworkEvent(new GroupStateUpdateEvent(), targetSession);
        SendResult(targetSession, false, Loc.GetString("group-you-were-kicked"));

        RemoveMemberFromGroup(targetUserId, groupId);
        SendResult(session, true, Loc.GetString("group-kicked", ("name", msg.TargetCharacterName)));
    }

    private void OnToggleOverlay(GroupToggleOverlayRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        if (msg.Enabled)
            _overlayEnabled.Add(userId);
        else
            _overlayEnabled.Remove(userId);
    }

    // ── Group state helpers ────────────────────────────────────────────────

    /// <summary>
    /// Removes a member from their group, handles leadership transfer, and disbands if empty.
    /// </summary>
    private void RemoveMemberFromGroup(NetUserId userId, int groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return;

        group.MemberOrder.Remove(userId);
        _playerToGroup.Remove(userId);

        if (group.MemberOrder.Count == 0)
        {
            // Group is empty — disband.
            _groups.Remove(groupId);
            return;
        }

        // Transfer leadership to the next oldest member if the leader left.
        if (group.LeaderUserId == userId)
            group.LeaderUserId = group.MemberOrder[0];

        BroadcastStateToGroup(groupId);
    }

    /// <summary>Sends current group state to all members of the group.</summary>
    private void BroadcastStateToGroup(int groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return;

        // Build the member list for the event.
        var members = new List<(NetEntity, string)>();
        foreach (var memberId in group.MemberOrder)
        {
            if (!TryGetSession(memberId, out var ms) || ms == null)
                continue;
            if (ms.AttachedEntity is not { } ent)
                continue;
            members.Add((GetNetEntity(ent), Name(ent)));
        }

        // Send individual state to each member.
        foreach (var memberId in group.MemberOrder)
        {
            if (!TryGetSession(memberId, out var ms) || ms == null)
                continue;

            var ev = new GroupStateUpdateEvent
            {
                Members      = new List<(NetEntity, string)>(members),
                LeaderUserId = group.LeaderUserId,
            };

            RaiseNetworkEvent(ev, ms);
        }
    }

    /// <summary>Sends current state to a single session (includes pending invite info if any).</summary>
    private void SendStateTo(ICommonSession session)
    {
        var userId = session.UserId;
        var ev     = new GroupStateUpdateEvent();

        if (_playerToGroup.TryGetValue(userId, out var groupId) &&
            _groups.TryGetValue(groupId, out var group))
        {
            ev.LeaderUserId = group.LeaderUserId;
            foreach (var memberId in group.MemberOrder)
            {
                if (!TryGetSession(memberId, out var ms) || ms == null)
                    continue;
                if (ms.AttachedEntity is not { } ent)
                    continue;
                ev.Members.Add((GetNetEntity(ent), Name(ent)));
            }
        }

        // Check for a pending invite addressed to this player.
        var invite = _pendingInvites.FirstOrDefault(i => i.TargetUserId == userId);
        if (invite != null && TryGetSession(invite.InviterUserId, out var inviterSession) && inviterSession != null)
        {
            ev.PendingInviteFromName   = inviterSession.AttachedEntity is { } ie ? Name(ie) : inviterSession.Name;
            ev.PendingInviteFromUserId = invite.InviterUserId;
        }

        RaiseNetworkEvent(ev, session);
    }

    // ── Utilities ──────────────────────────────────────────────────────────

    private void SendResult(ICommonSession session, bool success, string message)
    {
        RaiseNetworkEvent(new GroupActionResultEvent { Success = success, Message = message }, session);
    }

    private bool TryGetSession(NetUserId userId, out ICommonSession? session)
    {
        foreach (var s in _playerManager.Sessions)
        {
            if (s.UserId != userId)
                continue;
            session = s;
            return true;
        }

        session = null;
        return false;
    }

    /// <summary>
    /// Finds an in-game session whose attached entity name matches (case-insensitive trim).
    /// </summary>
    private bool TryGetSessionByCharacterName(string characterName, out ICommonSession? session)
    {
        var trimmed = characterName.Trim();
        foreach (var s in _playerManager.Sessions)
        {
            if (s.Status != SessionStatus.InGame || s.AttachedEntity == null)
                continue;
            if (string.Equals(Name(s.AttachedEntity.Value).Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                session = s;
                return true;
            }
        }

        session = null;
        return false;
    }

    // ── Event subscriptions ────────────────────────────────────────────────

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _groups.Clear();
        _playerToGroup.Clear();
        _pendingInvites.Clear();
        _overlayEnabled.Clear();
        _nextGroupId = 1;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var userId = args.Session.UserId;

        // Clean up pending invites involving this player.
        _pendingInvites.RemoveAll(i => i.InviterUserId == userId || i.TargetUserId == userId);
        _overlayEnabled.Remove(userId);

        if (!_playerToGroup.TryGetValue(userId, out var groupId))
            return;

        // Clear stale overlay for the disconnecting player (no-op since they're gone, but safe).
        RemoveMemberFromGroup(userId, groupId);
    }
}
