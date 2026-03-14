#nullable enable
using System.Linq; // #Misfits Add — for LINQ ticket filtering
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

        public override void Initialize()
        {
            base.Initialize();

            // #Misfits Add — subscribe to ticket messages from server
            SubscribeNetworkEvent<HelpTicketUpdatedMessage>(OnTicketUpdatedMsg);
            SubscribeNetworkEvent<HelpTicketListMessage>(OnTicketListMsg);
        }

        protected override void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs)
        {
            OnBwoinkTextMessageRecieved?.Invoke(this, message);
        }

        // #Misfits Add — relay ticket updates to the UI
        private void OnTicketUpdatedMsg(HelpTicketUpdatedMessage msg)
        {
            if (msg.Ticket.Type == HelpTicketType.AdminHelp)
                OnTicketUpdated?.Invoke(msg.Ticket);
        }

        private void OnTicketListMsg(HelpTicketListMessage msg)
        {
            var ahelpTickets = msg.Tickets.Where(t => t.Type == HelpTicketType.AdminHelp).ToList();
            if (ahelpTickets.Count > 0)
                OnTicketListReceived?.Invoke(ahelpTickets);
        }

        // #Misfits Add — send ticket claim/resolve requests
        public void ClaimTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketClaimMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void ResolveTicket(int ticketId)
        {
            RaiseNetworkEvent(new HelpTicketResolveMessage(ticketId, HelpTicketType.AdminHelp));
        }

        public void RequestTicketList()
        {
            RaiseNetworkEvent(new HelpTicketRequestListMessage(HelpTicketType.AdminHelp));
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
