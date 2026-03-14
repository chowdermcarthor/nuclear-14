// #Misfits Add - Shared ticket data model and network messages for the admin/mentor help ticket system.
// Tickets wrap the existing bwoink/mhelp conversations, adding claim/resolve lifecycle tracking.
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration;

/// <summary>
/// Possible states for a help ticket.
/// </summary>
[Serializable, NetSerializable]
public enum HelpTicketStatus : byte
{
    /// <summary>Player created a ticket; no admin/mentor has claimed it yet.</summary>
    Open,

    /// <summary>An admin/mentor has claimed this ticket.</summary>
    Claimed,

    /// <summary>The ticket has been resolved and closed.</summary>
    Resolved,
}

/// <summary>
/// Whether this is an admin-help or mentor-help ticket.
/// </summary>
[Serializable, NetSerializable]
public enum HelpTicketType : byte
{
    AdminHelp,
    MentorHelp,
}

/// <summary>
/// Lightweight snapshot of a ticket's state, sent to admin/mentor clients.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketInfo
{
    public int TicketId { get; set; }
    public NetUserId PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public HelpTicketStatus Status { get; set; }
    public HelpTicketType Type { get; set; }
    public string? ClaimedByName { get; set; }
    public NetUserId? ClaimedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ────────────────────── Network messages ──────────────────────

/// <summary>
/// Server → Admin/Mentor: full list of tickets (sent on first connect or when requested).
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketListMessage : EntityEventArgs
{
    public List<HelpTicketInfo> Tickets { get; }

    public HelpTicketListMessage(List<HelpTicketInfo> tickets)
    {
        Tickets = tickets;
    }
}

/// <summary>
/// Server → Admin/Mentor: a single ticket was created or its state changed.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketUpdatedMessage : EntityEventArgs
{
    public HelpTicketInfo Ticket { get; }

    public HelpTicketUpdatedMessage(HelpTicketInfo ticket)
    {
        Ticket = ticket;
    }
}

/// <summary>
/// Admin/Mentor → Server: request to claim a ticket.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketClaimMessage : EntityEventArgs
{
    public int TicketId { get; }
    public HelpTicketType Type { get; }

    public HelpTicketClaimMessage(int ticketId, HelpTicketType type)
    {
        TicketId = ticketId;
        Type = type;
    }
}

/// <summary>
/// Admin/Mentor → Server: request to resolve (close) a ticket.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketResolveMessage : EntityEventArgs
{
    public int TicketId { get; }
    public HelpTicketType Type { get; }

    public HelpTicketResolveMessage(int ticketId, HelpTicketType type)
    {
        TicketId = ticketId;
        Type = type;
    }
}

/// <summary>
/// Admin/Mentor → Server: request current ticket list.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketRequestListMessage : EntityEventArgs
{
    public HelpTicketType Type { get; }

    public HelpTicketRequestListMessage(HelpTicketType type)
    {
        Type = type;
    }
}
