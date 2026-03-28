using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._Misfits.Network;

/// <summary>
/// Overrides client-side network CVar defaults to reduce rubberbanding.
///
/// These CVars are CLIENTONLY/ARCHIVE — they can't be set from the server TOML.
/// <c>OverrideDefault</c> changes the value only if the player hasn't manually
/// set a custom value themselves (ARCHIVE CVars persist across sessions).
///
/// predict_tick_bias: How many extra ticks ahead the client predicts. Higher
///   values keep the client further ahead of the server, reducing the chance
///   that a late server state causes a visible snap-correction. Cost: slightly
///   more prediction divergence on high-ping connections.
///
/// predict_lag_bias: Extra seconds added to the ping estimate when calculating
///   how far ahead to predict. The Windows default of 0.016 is too tight for
///   Lidgren's timer precision on many systems; 0.025 provides more margin.
/// </summary>
public sealed class MisfitsNetworkDefaultsSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Predict 1 extra tick ahead (default = 1, now 2).
        // Costs a tiny bit of extra prediction work but dramatically reduces
        // visible rubber-banding when the server is slightly behind.
        _config.OverrideDefault(CVars.NetPredictTickBias, 2);

        // Increase ping-estimate padding (default = 0.016).
        // Lidgren's timing can be imprecise; 0.025 absorbs jitter.
        _config.OverrideDefault(CVars.NetPredictLagBias, 0.025f);
    }
}
