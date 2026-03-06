using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.Shared._Nuclear14.Silicons;

public sealed class BorgHandsSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BorgHandsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BorgHandsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HandsComponent>(ent, out var hands))
            return;

        for (var i = 0; i < ent.Comp.HandCount; i++)
        {
            _hands.AddHand(ent, $"borg-hand-{i}", HandLocation.Middle, hands);
        }
    }
}
