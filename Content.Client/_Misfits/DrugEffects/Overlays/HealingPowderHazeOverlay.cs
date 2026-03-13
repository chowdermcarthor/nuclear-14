// #Misfits Change /Add:/ Healing powder haze overlay
using Content.Client._Misfits.DrugEffects;
using Content.Shared._Misfits.DrugEffects;
using Content.Shared.StatusEffect;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.DrugEffects.Overlays;

/// <summary>
///     Applies a restrained blur while healing powder is active.
/// </summary>
public sealed class HealingPowderHazeOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public float CurrentPower;
    private float _visualScale;

    private const float PowerDivisor = 30f;
    private const float Intensity = 0.02f; // #Misfits Tweak: Reduced from 0.04 (was 0.08) — further toned down screen haze

    public HealingPowderHazeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("Drowsiness").InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;
        if (playerEntity == null)
            return;

        if (!_entityManager.HasComponent<HealingPowderHazeComponent>(playerEntity)
            || !_entityManager.TryGetComponent<StatusEffectsComponent>(playerEntity, out var status))
            return;

        var statusEffects = _systems.GetEntitySystem<StatusEffectsSystem>();
        if (!statusEffects.TryGetTime(playerEntity.Value, MisfitsDrugOverlaySystem.HealingPowderHazeKey, out var time, status))
            return;

        var timeLeft = (float) (time.Value.Item2 - _timing.CurTime).TotalSeconds;
        CurrentPower += 8f * (0.5f * timeLeft - CurrentPower) * args.DeltaSeconds / (timeLeft + 1f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eye))
            return false;

        if (args.Viewport.Eye != eye.Eye)
            return false;

        _visualScale = Math.Clamp(CurrentPower / PowerDivisor, 0f, 1f);
        return _visualScale > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("Strength", _visualScale * Intensity);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}