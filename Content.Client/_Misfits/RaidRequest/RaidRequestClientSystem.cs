// #Misfits Add - Client-side /raid request system. Registers the /raid console command,
// owns both the requester window and the admin embedded control, and translates server
// decision broadcasts into a screen popup so affected players can't miss the news.
// The actual chat-line for the decision is delivered server-side via DispatchServerMessage,
// so this system only needs to handle the popup + UI sync.

using Content.Client._Misfits.RaidRequest.UI;
using Content.Client.Popups;
using Content.Shared._Misfits.RaidRequest;
using Content.Shared.Popups;
using Robust.Client.Console;
using Robust.Shared.Console;

namespace Content.Client._Misfits.RaidRequest;

public sealed class RaidRequestClientSystem : EntitySystem
{
    [Dependency] private readonly IClientConsoleHost _conHost = default!;
    [Dependency] private readonly PopupSystem        _popup   = default!;

    private RaidRequestWindow? _window;

    /// <summary>Latest admin snapshot. Mirrored into <see cref="RaidRequestAdminControl"/> when present.</summary>
    public IReadOnlyList<RaidRequestEntry> AdminRequests => _adminRequests;
    private List<RaidRequestEntry> _adminRequests = new();

    /// <summary>Bound by <see cref="RaidRequestAdminControl"/> while the tab is open.</summary>
    public RaidRequestAdminControl? AdminControl;

    // #Misfits Add - Server-broadcast dict of every entity participating in an approved
    // faction-tier raid (NetEntity → canonical faction side ID). Read by AllyTagOverlay
    // alongside the war participants dict to draw [ALLY]/[ENEMY] tags during raids.
    // Empty when no faction-tier raid is approved.
    public IReadOnlyDictionary<NetEntity, string> RaidParticipants => _raidParticipants;
    private Dictionary<NetEntity, string> _raidParticipants = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RaidRequestPanelDataMsg>(OnPanelData);
        SubscribeNetworkEvent<RaidRequestSubmitResultMsg>(OnSubmitResult);
        SubscribeNetworkEvent<RaidRequestAdminListMsg>(OnAdminList);
        SubscribeNetworkEvent<RaidRequestAdminUpdateMsg>(OnAdminUpdate);
        SubscribeNetworkEvent<RaidRequestDecisionResultMsg>(OnDecisionResult);
        SubscribeNetworkEvent<RaidRequestDecisionAnnouncementMsg>(OnDecisionAnnouncement);
        // #Misfits Add - Overlay participants stream (drives AllyTagOverlay).
        SubscribeNetworkEvent<RaidRequestParticipantsUpdatedMsg>(OnParticipantsUpdated);

        _conHost.RegisterCommand(
            "raid",
            "Open the Raid Request panel.",
            "raid",
            OpenRaidPanel);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
    }

    // ── /raid console command ─────────────────────────────────────────────

    private void OpenRaidPanel(IConsoleShell shell, string argStr, string[] args)
    {
        EnsureWindow();
        _window!.OpenCentered();
        // Ask the server for fresh panel data — faction resolution lives server-side.
        RaiseNetworkEvent(new RaidRequestOpenPanelMsg());
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = new RaidRequestWindow();
        _window.OnClose += () => _window = null;

        _window.OnSubmit += (target, location, reason) =>
        {
            RaiseNetworkEvent(new RaidRequestSubmitMsg
            {
                TargetFaction = target,
                LocationNotes = location,
                Reason        = reason,
            });
        };
    }

    // ── Network handlers: requester ───────────────────────────────────────

    private void OnPanelData(RaidRequestPanelDataMsg msg)
    {
        _window?.UpdateState(msg);
    }

    private void OnSubmitResult(RaidRequestSubmitResultMsg msg)
    {
        _window?.ShowResult(msg.Success, msg.Message);
        if (msg.Success)
        {
            // Auto-refresh panel so the player sees their new entry in "My Requests".
            RaiseNetworkEvent(new RaidRequestOpenPanelMsg());
        }
    }

    // ── Network handlers: admin ───────────────────────────────────────────

    private void OnAdminList(RaidRequestAdminListMsg msg)
    {
        _adminRequests = msg.Requests;
        AdminControl?.UpdateList(_adminRequests);
    }

    private void OnAdminUpdate(RaidRequestAdminUpdateMsg msg)
    {
        // Replace or insert this single entry, keep newest-first ordering.
        var existing = _adminRequests.FindIndex(e => e.Id == msg.Entry.Id);
        if (existing >= 0)
            _adminRequests[existing] = msg.Entry;
        else
            _adminRequests.Insert(0, msg.Entry);

        AdminControl?.UpdateList(_adminRequests);
    }

    private void OnDecisionResult(RaidRequestDecisionResultMsg msg)
    {
        AdminControl?.ShowDecisionResult(msg.RequestId, msg.Success, msg.Message);
    }

    /// <summary>Subscribed by <see cref="RaidRequestAdminControl"/> to send approve/deny.</summary>
    public void SendDecision(int requestId, bool approve, string comment)
    {
        RaiseNetworkEvent(new RaidRequestDecisionMsg
        {
            RequestId = requestId,
            Approve   = approve,
            Comment   = comment,
        });
    }

    /// <summary>Subscribed by <see cref="RaidRequestAdminControl"/> on first open.</summary>
    public void SendAdminSubscribe() => RaiseNetworkEvent(new RaidRequestAdminSubscribeMsg());

    // ── Decision popup ────────────────────────────────────────────────────

    private void OnDecisionAnnouncement(RaidRequestDecisionAnnouncementMsg msg)
    {
        var entry = msg.Entry;
        var verb  = entry.Status == RaidRequestStatus.Approved ? "APPROVED" : "DENIED";
        var who   = entry.IsIndividual
            ? entry.RequesterCharacterName
            : RaidRequestConfig.FactionDisplayName(entry.RequesterFaction);
        var target = RaidRequestConfig.FactionDisplayName(entry.TargetFaction);

        // Different framing depending on whether you're the raider side or being raided.
        var header = msg.IsTargetSide
            ? $"INCOMING RAID REQUEST {verb}"
            : $"RAID REQUEST {verb}";

        var text = $"{header}\n{who} → {target}\nSee chat for admin remarks.";

        // Cursor popup is the most reliable way to grab the player's attention without disrupting flow.
        var popupType = entry.Status == RaidRequestStatus.Approved
            ? PopupType.LargeCaution
            : PopupType.Large;
        _popup.PopupCursor(text, popupType);
    }

    // ── Overlay participants ──────────────────────────────────────────────

    // #Misfits Add - Replace the cached raid-participants dict whenever the server
    // re-broadcasts. After swapping, prod the war system to (re)evaluate overlay
    // lifecycle since the war system owns the AllyTagOverlay.
    private void OnParticipantsUpdated(RaidRequestParticipantsUpdatedMsg msg)
    {
        _raidParticipants = msg.Participants;

        if (EntityManager.TrySystem<Content.Client._Misfits.FactionWar.FactionWarClientSystem>(out var war))
            war.RefreshOverlay();
    }
}
