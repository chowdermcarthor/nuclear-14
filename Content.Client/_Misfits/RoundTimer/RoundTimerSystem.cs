// #Misfits Change: Client-side round timer in the OS window title bar.
using Content.Shared._Misfits.RoundTimer;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.RoundTimer;

/// <summary>
///     Listens for <see cref="RoundTimerUpdatedEvent"/> from the server and appends
///     a round-end countdown to the OS window title bar.
/// </summary>
public sealed class RoundTimerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameController _gameController = default!;

    private TimeSpan _deadline;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RoundTimerUpdatedEvent>(OnRoundTimerUpdated);
    }

    private void OnRoundTimerUpdated(RoundTimerUpdatedEvent ev)
    {
        _deadline = ev.Deadline;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var baseTitle = _gameController.GameTitle();

        if (_deadline == TimeSpan.Zero)
        {
            _clyde.SetWindowTitle(baseTitle);
            return;
        }

        var remaining = _deadline - _gameTiming.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var hours = (int) remaining.TotalHours;
        var minutes = remaining.Minutes;
        var seconds = remaining.Seconds;
        _clyde.SetWindowTitle($"{baseTitle} — Round End: {hours}:{minutes:D2}:{seconds:D2}");
    }
}

