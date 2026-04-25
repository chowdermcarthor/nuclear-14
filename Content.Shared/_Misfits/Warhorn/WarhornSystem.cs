using System;
using Content.Shared._Misfits.Warhorn.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;


namespace Content.Shared._Misfits.Warhorn;


public sealed class WarhornSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _netMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarhornComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<WarhornComponent> ent, ref UseInHandEvent args)
    {
        var sound = ent.Comp.Sound;
        var hornXform = Transform(ent);

        var audioParams = new AudioParams
        {
            Volume = ent.Comp.Volume,
            Variation = 0.125f,
            MaxDistance = ent.Comp.Range
        };

        if (_netMan.IsServer)
        {
            _audio.PlayEntity(
                filename: _audio.ResolveSound(sound),
                Filter.Empty().AddInRange(_transform.GetMapCoordinates(ent, hornXform), ent.Comp.Range),
                ent,
                recordReplay: true,
                audioParams
            );
        }
    }
}
