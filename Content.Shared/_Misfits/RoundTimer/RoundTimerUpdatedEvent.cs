// #Misfits Change: Network event to sync round auto-call deadline to clients.
using System;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.RoundTimer;

/// <summary>
///     Raised as a network event to inform clients of the current round auto-call deadline.
///     Clients use this to display a countdown timer on the HUD.
/// </summary>
[Serializable, NetSerializable]
public sealed class RoundTimerUpdatedEvent : EntityEventArgs
{
    /// <summary>
    ///     The game CurTime at which the emergency shuttle will auto-call.
    ///     <see cref="TimeSpan.Zero"/> if auto-call is disabled.
    /// </summary>
    public TimeSpan Deadline { get; }

    public RoundTimerUpdatedEvent(TimeSpan deadline)
    {
        Deadline = deadline;
    }
}
