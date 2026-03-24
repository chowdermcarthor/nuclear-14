#nullable enable
using System.Linq; // #Misfits Add — for LINQ ticket filtering
using Content.Client._Misfits.Administration.UI; // #Misfits Add — for TicketToastPopup
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Shared._Misfits.Administration; // #Misfits Add — ticket system types
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Administration.Systems
{
    [UsedImplicitly]
    public sealed class BwoinkSystem : SharedBwoinkSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly AudioSystem _audio = default!;
        [Dependency] private readonly AdminSystem _adminSystem = default!;

        public event EventHandler<BwoinkTextMessage>? OnBwoinkTextMessageRecieved;
        private (TimeSpan Timestamp, bool Typing) _lastTypingUpdateSent;

        // #Misfits Add — ticket events for the UI
        public event Action<HelpTicketInfo>? OnTicketUpdated;
        public event Action<List<HelpTicketInfo>>? OnTicketListReceived;
        // #Misfits Add — audit log response from server DB query
        public event Action<HelpTicketAuditResponseMessage>? OnAuditLogReceived;

        // #Misfits Add — track known tickets to only toast on new or significant state changes
        private readonly Dictionary<int, HelpTicketStatus> _knownTickets = new();

        public override void Initialize()
        {
            base.Initialize();

            // #Misfits Add — subscribe to ticket messages from server
            SubscribeNetworkEvent<HelpTicketUpdatedMessage>(OnTicketUpdatedMsg);
            SubscribeNetworkEvent<HelpTicketListMessage>(OnTicketListMsg);
            // #Misfits Add — subscribe to audit log responses from server
            SubscribeNetworkEvent<HelpTicketAuditResponseMessage>(OnAuditLogMsg);
        }

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkTextMessageRecieved?.Invoke(this, message);
        }

        // #Misfits Add — relay ticket updates to the UI and show toast notifications
        private void OnTicketUpdatedMsg(HelpTicketUpdatedMessage msg)
        {
            if (msg.Ticket.Type == HelpTicketType.AdminHelp)
            {
                ShowTicketToast(msg.Ticket);
                OnTicketUpdated?.Invoke(msg.Ticket);
            }
        }

        private void OnTicketListMsg(HelpTicketListMessage msg)
        {
            var ahelpTickets = msg.Tickets.Where(t => t.Type == HelpTicketType.AdminHelp).ToList();
            // #Misfits Fix — replace known ticket cache from authoritative server list.
            // This prevents old round ticket IDs from persisting client-side.
            _knownTickets.Clear();
            foreach (var t in ahelpTickets)
                _knownTickets[t.TicketId] = t.Status;

            // #Misfits Fix — always notify listeners, including empty lists,
            // so UI caches can clear stale entries between rounds.
            OnTicketListReceived?.Invoke(ahelpTickets);
        }

        // #Misfits Add — relay server DB audit log response to the UI
        private void OnAuditLogMsg(HelpTicketAuditResponseMessage msg)
        {
            OnAuditLogReceived?.Invoke(msg);
        }

        // #Misfits Add — show a toast popup for notable ticket events
        private void ShowTicketToast(HelpTicketInfo ticket)
        {
            var previouslyKnown = _knownTickets.TryGetValue(ticket.TicketId, out var prevStatus);
            _knownTickets[ticket.TicketId] = ticket.Status;

            // Only toast for meaningful events, not every redundant update
            string? title = null;
            string? body = null;

            if (!previouslyKnown && ticket.Status == HelpTicketStatus.Open)
            {
                // Brand-new ticket
                title = Loc.GetString("ticket-system-toast-new-title");
                body = Loc.GetString("ticket-system-toast-new-body", ("id", ticket.TicketId), ("player", ticket.PlayerName));
            }
            else if (previouslyKnown && prevStatus != ticket.Status)
            {
                // Status changed
                switch (ticket.Status)
                {
                    case HelpTicketStatus.Claimed:
                        title = Loc.GetString("ticket-system-toast-claimed-title");
                        body = Loc.GetString("ticket-system-toast-claimed-body", ("id", ticket.TicketId), ("role", "Admin"), ("admin", ticket.ClaimedByName ?? "?"));
                        break;
                    case HelpTicketStatus.Resolved:
                        title = Loc.GetString("ticket-system-toast-resolved-title");
                        body = Loc.GetString("ticket-system-toast-resolved-body", ("id", ticket.TicketId), ("role", "Admin"), ("admin", ticket.ResolvedByName ?? "?"));
                        break;
                    case HelpTicketStatus.Open when prevStatus == HelpTicketStatus.Resolved:
                        title = Loc.GetString("ticket-system-toast-reopened-title");
                        body = Loc.GetString("ticket-system-toast-reopened-body", ("id", ticket.TicketId), ("player", ticket.PlayerName));
                        break;
                }
            }

            if (title != null && body != null)
            {
                var toast = new TicketToastPopup();
                toast.Show(title, body);
            }
        }

        // #Misfits Add — send ticket claim/resolve/unclaim/reopen requests
        public void ClaimTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketClaimMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void ResolveTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketResolveMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void UnclaimTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketUnclaimMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void ReopenTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketReopenMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void RequestTicketList()
        {
            RaiseNetworkEvent(new HelpTicketRequestListMessage(HelpTicketType.AdminHelp));
        }

        // #Misfits Add — request a page of the persistent audit log from the server DB
        public void RequestAuditLog(Guid? filterPlayerId = null, int limit = 100, int offset = 0)
        {
            RaiseNetworkEvent(new HelpTicketAuditRequestMessage
            {
                FilterPlayerId = filterPlayerId,
                Limit = limit,
                Offset = offset,
            });
        }

        public void Send(NetUserId channelId, string text, bool playSound)
        {
            var info = _adminSystem.PlayerInfos.GetValueOrDefault(channelId)?.Connected ?? true;
            _audio.PlayGlobal(info ? AHelpUIController.AHelpSendSound : AHelpUIController.AHelpErrorSound,
                Filter.Local(), false);

            // Reuse the channel ID as the 'true sender'.
            // Server will ignore this and if someone makes it not ignore this (which is bad, allows impersonation!!!), that will help.
            RaiseNetworkEvent(new BwoinkTextMessage(channelId, channelId, text, playSound: playSound));
            SendInputTextUpdated(channelId, false);
        }

        public void SendInputTextUpdated(NetUserId channel, bool typing)
        {
            if (_lastTypingUpdateSent.Typing == typing &&
                _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
                return;

            _lastTypingUpdateSent = (_timing.RealTime, typing);
            RaiseNetworkEvent(new BwoinkClientTypingUpdated(channel, typing));
        }

        // #Misfits Add — sends a ghost-follow request to the server for the AHelp Follow button.
        // Server will ensure aghost mode is active before starting the orbit.
        public void GhostFollow(NetUserId targetUserId)
        {
            RaiseNetworkEvent(new BwoinkAdminGhostFollowMessage(targetUserId));
        }
    }
}
