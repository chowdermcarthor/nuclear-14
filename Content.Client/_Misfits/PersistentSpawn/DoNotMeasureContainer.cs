// #Misfits Add - Zero-measure wrapper container for the PersistentEntitySpawnWindow.
// Mirrors the engine's internal DoNotMeasure (Robust.Client.UserInterface.CustomControls)
// which is inaccessible from Content.Client. Wrapping MeasureButton in this container
// prevents the layout tree from re-measuring it (which would zero out DesiredSize because
// Visible="False" short-circuits MeasureCore), so the manually-called
// MeasureButton.Measure(Infinity) in the window constructor produces a lasting result.
using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.PersistentSpawn;

/// <summary>
/// A container that always reports <see cref="Vector2.Zero"/> to the layout system.
/// Used to host the invisible MeasureButton so the layout pass never overwrites its
/// manually-measured <see cref="Control.DesiredSize"/>.
/// </summary>
public sealed class DoNotMeasureContainer : Control
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Return zero so this container takes no space in the parent BoxContainer.
        return Vector2.Zero;
    }
}
