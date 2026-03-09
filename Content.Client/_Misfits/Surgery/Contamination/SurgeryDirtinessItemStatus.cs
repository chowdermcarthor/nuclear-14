// #Misfits Change - Ported from Delta-V surgery contamination system
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Misfits.Surgery.Contamination;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Surgery.Contamination;

/// <summary>
///     Item status widget that shows the cleanliness of a held surgical instrument (and the user's gloves).
///     Displayed as a split color bar in the HUD when holding a tool with <see cref="SurgeryDirtinessComponent"/>.
/// </summary>
public sealed class SurgeryDirtinessItemStatus : SplitBar
{
    private readonly IEntityManager _entMan;
    private readonly EntityUid _uid;
    private readonly InventorySystem _inventory;
    private readonly SharedContainerSystem _container;
    private FixedPoint2? _dirtiness = null;
    private FixedPoint2? _gloveDirtiness = null;

    private static readonly Color SelfCleanColor = new(0xD1, 0xD5, 0xD9);
    private static readonly Color SelfDirtyColor = new(0xE9, 0x3D, 0x58);
    private static readonly Color GloveCleanColor = new(0xAB, 0xE9, 0xFB);
    private static readonly Color GloveDirtyColor = new(0xE9, 0x64, 0x3A);

    public SurgeryDirtinessItemStatus(EntityUid uid, IEntityManager entMan, InventorySystem inventory, SharedContainerSystem container)
    {
        _uid = uid;
        _entMan = entMan;
        _inventory = inventory;
        _container = container;
        MinBarSize = new Vector2(10, 0);
        Margin = new Thickness(4);
        MinHeight = 16;
        MaxHeight = 16;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entMan.TryGetComponent<SurgeryDirtinessComponent>(_uid, out var comp))
            return;

        var isTool = _entMan.HasComponent<SurgeryToolComponent>(_uid);

        FixedPoint2? newGloveDirtiness = null;
        if (isTool && _container.TryGetContainingContainer((_uid, null, null), out var container))
        {
            var user = container.Owner;
            if (_inventory.TryGetSlotEntity(user, "gloves", out var gloves) && gloves.HasValue)
            {
                newGloveDirtiness = _entMan.TryGetComponent<SurgeryDirtinessComponent>(gloves.Value, out var glovesComp)
                    ? glovesComp.Dirtiness
                    : FixedPoint2.Zero;
            }
        }

        // Only redraw if values changed
        if (_dirtiness == comp.Dirtiness && _gloveDirtiness == newGloveDirtiness)
            return;

        _dirtiness = comp.Dirtiness;
        _gloveDirtiness = newGloveDirtiness;

        Clear();

        var toolDirty = Math.Clamp((float) comp.Dirtiness.Double() / 100f, 0f, 1f);
        var toolClean = 1f - toolDirty;
        if (toolClean > 0) AddEntry(toolClean, SelfCleanColor);
        if (toolDirty > 0) AddEntry(toolDirty, SelfDirtyColor);

        if (newGloveDirtiness.HasValue)
        {
            var gloveDirty = Math.Clamp((float) newGloveDirtiness.Value.Double() / 100f, 0f, 1f);
            var gloveClean = 1f - gloveDirty;
            if (gloveClean > 0) AddEntry(gloveClean, GloveCleanColor);
            if (gloveDirty > 0) AddEntry(gloveDirty, GloveDirtyColor);
        }
    }
}
