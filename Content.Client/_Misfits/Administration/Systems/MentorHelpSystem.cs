// #Misfits Change - Client-side mentor help system
using System.Linq; // #Misfits Add — for LINQ ticket filtering
using Content.Client._Misfits.UserInterface.Systems.MentorHelp;
using Content.Shared._Misfits.Administration;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Administration.Systems;

[UsedImplicitly]
public sealed class MentorHelpSystem : SharedMentorHelpSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public event EventHandler<MentorHelpTextMessage>? OnMentorHelpTextMessageReceived;
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

    protected override void OnMentorHelpTextMessage(MentorHelpTextMessage message, EntitySessionEventArgs eventArgs)
    {
        OnMentorHelpTextMessageReceived?.Invoke(this, message);
    }

    // #Misfits Add — relay ticket updates to the UI
    private void OnTicketUpdatedMsg(HelpTicketUpdatedMessage msg)
    {
        if (msg.Ticket.Type == HelpTicketType.MentorHelp)
            OnTicketUpdated?.Invoke(msg.Ticket);
    }

    private void OnTicketListMsg(HelpTicketListMessage msg)
    {
        var mhelpTickets = msg.Tickets.Where(t => t.Type == HelpTicketType.MentorHelp).ToList();
        if (mhelpTickets.Count > 0)
            OnTicketListReceived?.Invoke(mhelpTickets);
    }

    // #Misfits Add — send ticket claim/resolve requests
    public void ClaimTicket(int ticketId)
    {
        RaiseNetworkEvent(new HelpTicketClaimMessage(ticketId, HelpTicketType.MentorHelp));
    }

    public void ResolveTicket(int ticketId)
    {
        RaiseNetworkEvent(new HelpTicketResolveMessage(ticketId, HelpTicketType.MentorHelp));
    }

    public void RequestTicketList()
    {
        RaiseNetworkEvent(new HelpTicketRequestListMessage(HelpTicketType.MentorHelp));
    }

    public void Send(NetUserId channelId, string text, bool playSound)
    {
        _audio.PlayGlobal(MentorHelpUIController.MHelpSendSound, Filter.Local(), false);
        RaiseNetworkEvent(new MentorHelpTextMessage(channelId, channelId, text, playSound: playSound));
        SendInputTextUpdated(channelId, false);
    }

    public void SendInputTextUpdated(NetUserId channel, bool typing)
    {
        if (_lastTypingUpdateSent.Typing == typing &&
            _lastTypingUpdateSent.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
            return;

        _lastTypingUpdateSent = (_timing.RealTime, typing);
        RaiseNetworkEvent(new MentorHelpClientTypingUpdated(channel, typing));
    }
}
