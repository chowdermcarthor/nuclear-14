// #Misfits Change - Ported from Delta-V chronic pain system
using Content.Shared._Misfits.ChronicPain.Components;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.ChronicPain.EntitySystems;

/// <summary>
///     Shared system for chronic pain. Handles the ticking and popup logic.
///     Client overrides manage the visual overlay; server override is a stub.
/// </summary>
public abstract partial class SharedChronicPainSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] protected readonly IPrototypeManager ProtoManager = default!;
    [Dependency] protected readonly IRobustRandom RobustRandom = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChronicPainComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChronicPainComponent, ComponentInit>(OnChronicPainInit);
        SubscribeLocalEvent<ChronicPainComponent, ComponentShutdown>(OnChronicPainShutdown);
        SubscribeLocalEvent<ChronicPainComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ChronicPainComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    /// <summary>
    ///     Returns true if the entity's chronic pain is currently suppressed (e.g. by medication).
    /// </summary>
    [PublicAPI]
    public bool IsChronicPainSuppressed(Entity<ChronicPainComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return true; // No component = no pain = suppressed

        return _timing.CurTime < entity.Comp.SuppressionEndTime;
    }

    /// <summary>
    ///     Suppresses chronic pain for a given duration. If no duration is given, uses DefaultSuppressionTime.
    /// </summary>
    [PublicAPI]
    public bool TrySuppressChronicPain(Entity<ChronicPainComponent?> entity, TimeSpan? duration)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return true;

        if (!duration.HasValue)
            duration = entity.Comp.DefaultSuppressionTime;

        entity.Comp.SuppressionEndTime = _timing.CurTime + duration.Value;
        Dirty(entity.Owner, entity.Comp);
        return true;
    }

    protected void OnMapInit(Entity<ChronicPainComponent> entity, ref MapInitEvent args)
    {
        entity.Comp.NextUpdateTime = _timing.CurTime;
        entity.Comp.NextPopupTime = _timing.CurTime;
    }

    protected virtual void OnChronicPainInit(Entity<ChronicPainComponent> entity, ref ComponentInit args)
    {
        // Give the player a grace period before they need medication
        if (TrySuppressChronicPain((entity.Owner, entity.Comp), entity.Comp.DefaultSuppressionTimeOnInit))
            entity.Comp.NextUpdateTime = _timing.CurTime + entity.Comp.DefaultSuppressionTimeOnInit;
    }

    protected virtual void OnChronicPainShutdown(Entity<ChronicPainComponent> entity, ref ComponentShutdown args)
    {
        // Overridden in client to clean up overlay
    }

    protected virtual void OnPlayerAttached(Entity<ChronicPainComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        // Overridden in client to show/hide overlay
    }

    protected virtual void OnPlayerDetached(Entity<ChronicPainComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        // Overridden in client to remove overlay
    }

    protected void ShowPainPopup(Entity<ChronicPainComponent> entity)
    {
        if (IsChronicPainSuppressed((entity.Owner, entity.Comp)))
            return;

        if (!ProtoManager.TryIndex(entity.Comp.DatasetPrototype, out var dataset))
            return;

        var effects = dataset.Values;
        if (effects.Count == 0)
            return;

        var message = Loc.GetString(RobustRandom.Pick(effects));
        // #Misfits Change — delegate display to the server override (private emote, self-only)
        ShowPainEffect(entity, message);

        var delaySeconds = RobustRandom.NextDouble()
            * (entity.Comp.MaximumPopupDelay - entity.Comp.MinimumPopupDelay).TotalSeconds
            + entity.Comp.MinimumPopupDelay.TotalSeconds;
        entity.Comp.NextPopupTime = _timing.CurTime + TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    ///     Called when a pain message should be shown. Overridden server-side to send a
    ///     private emote-channel message visible only to the affected player.
    /// </summary>
    protected virtual void ShowPainEffect(Entity<ChronicPainComponent> entity, string message) { }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ChronicPainComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (curTime < component.NextUpdateTime)
                continue;

            if (curTime >= component.NextPopupTime)
                ShowPainPopup((uid, component));

            component.NextUpdateTime = curTime + TimeSpan.FromSeconds(5);
        }
    }
}
