using Content.Shared._Misfits.SandDigging;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Misfits.SandDigging;

/// <summary>
/// Handles digging sand from desert tiles when a shovel with <see cref="SandDiggerComponent"/>
/// is used on a valid sand tile. Spawns a sand entity at the dig location.
/// </summary>
public sealed class SandDiggingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SandDiggerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SandDiggerComponent, SandDiggingDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnAfterInteract(EntityUid uid, SandDiggerComponent component, AfterInteractEvent args)
    {
        // Only handle clicks on empty ground (no entity target), within reach, and not already digging.
        if (args.Handled || args.Target != null || !args.CanReach || component.IsDigging)
            return;

        // Resolve the tile under the click location.
        var gridUid = args.ClickLocation.GetGridUid(EntityManager);
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _maps.GetTileRef(gridUid.Value, grid, args.ClickLocation);
        var tileDef = _tileDefs[tile.Tile.TypeId];

        // Only proceed if the tile is in our sand whitelist.
        if (!component.SandTileIds.Contains(tileDef.ID))
            return;

        // Verify the user can actually reach the tile centre.
        var tileCoords = _maps.GridTileToLocal(gridUid.Value, grid, tile.GridIndices);
        if (!_interaction.InRangeUnobstructed(args.User, tileCoords, popup: false))
            return;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            component.DigDelay,
            new SandDiggingDoAfterEvent(GetNetCoordinates(args.ClickLocation)),
            uid,
            used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        component.Stream ??= _audio.PlayPredicted(component.DigSound, uid, args.User)?.Entity;
        component.IsDigging = true;

        var selfMsg = Loc.GetString("sand-digging-start-user");
        var othersMsg = Loc.GetString("sand-digging-start-others", ("user", args.User));
        _popup.PopupPredicted(selfMsg, othersMsg, args.User, args.User);

        args.Handled = true;
    }

    private void OnDoAfterComplete(EntityUid uid, SandDiggerComponent component, SandDiggingDoAfterEvent args)
    {
        component.IsDigging = false;
        component.Stream = _audio.Stop(component.Stream);

        if (args.Cancelled || args.Handled)
            return;

        // Spawn a pile of sand at the dig location.
        var spawnCoords = GetCoordinates(args.DigCoordinates);
        Spawn(component.SandPrototype, spawnCoords);

        _popup.PopupEntity(Loc.GetString("sand-digging-complete"), args.User, args.User);
    }
}
