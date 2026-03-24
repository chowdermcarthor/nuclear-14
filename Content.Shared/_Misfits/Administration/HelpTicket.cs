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
    public string? ResolvedByName { get; set; }
    public NetUserId? ResolvedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
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
/// Admin/Mentor → Server: request to unclaim (release) a ticket back to Open.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketUnclaimMessage : EntityEventArgs
{
    public int TicketId { get; }
    public HelpTicketType Type { get; }

    public HelpTicketUnclaimMessage(int ticketId, HelpTicketType type)
    {
        TicketId = ticketId;
        Type = type;
    }
}

/// <summary>
/// Admin/Mentor → Server: request to reopen a resolved ticket.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketReopenMessage : EntityEventArgs
{
    public int TicketId { get; }
    public HelpTicketType Type { get; }

    public HelpTicketReopenMessage(int ticketId, HelpTicketType type)
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

// ──────────────────── Audit Log ──────────────────────

/// <summary>
/// The type of lifecycle change that was recorded for a ticket in the persistent audit log.
/// </summary>
[Serializable, NetSerializable]
public enum HelpTicketEventType
{
    /// <summary>A player sent their first message and created the ticket.</summary>
    Created,
    /// <summary>An admin/mentor claimed the ticket.</summary>
    Claimed,
    /// <summary>An admin/mentor released the claim, returning the ticket to Open.</summary>
    Unclaimed,
    /// <summary>An admin/mentor marked the ticket as resolved.</summary>
    Resolved,
    /// <summary>An admin/mentor reopened a previously resolved ticket.</summary>
    Reopened,
    /// <summary>The ticket was auto-resolved because the player disconnected.</summary>
    AutoResolved,
}

/// <summary>
/// One persistent audit log entry returned from the database.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketAuditEntry
{
    /// <summary>Database event row ID.</summary>
    public int EventId { get; init; }
    /// <summary>The player who created/owns the ticket.</summary>
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; } = string.Empty;
    /// <summary>In-round sequential ticket number (1, 2, 3…).</summary>
    public int TicketId { get; init; }
    public HelpTicketType TicketType { get; init; }
    public HelpTicketEventType EventType { get; init; }
    /// <summary>Null when created by the player or auto-resolved by the system.</summary>
    public string? AdminName { get; init; }
    public Guid? AdminId { get; init; }
    public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Admin → Server: request audit log entries from the database.
/// Optionally filtered to a specific player.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketAuditRequestMessage : EntityEventArgs
{
    /// <summary>When set, only return events for this player. Null returns all players.</summary>
    public Guid? FilterPlayerId { get; init; }
    /// <summary>Maximum number of entries to return (default 100).</summary>
    public int Limit { get; init; } = 100;
    /// <summary>Number of entries to skip for pagination (default 0).</summary>
    public int Offset { get; init; } = 0;
}

/// <summary>
/// Server → Admin: audit log entries retrieved from the database.
/// </summary>
[Serializable, NetSerializable]
public sealed class HelpTicketAuditResponseMessage : EntityEventArgs
{
    public List<HelpTicketAuditEntry> Entries { get; init; } = new();
    /// <summary>Total matching row count (for pagination).</summary>
    public int TotalCount { get; init; }
    /// <summary>Offset that was used to produce this page.</summary>
    public int Offset { get; init; }
}
