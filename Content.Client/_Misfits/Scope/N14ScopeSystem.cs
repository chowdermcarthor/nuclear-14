using System.Numerics;
using Content.Shared._Misfits.Scope;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;

namespace Content.Client._Misfits.Scope;

/// <summary>
/// Client-side scope system.
/// Overrides the virtual hooks from <see cref="SharedN14ScopeSystem"/> to apply
/// and reset the eye offset when the scope is toggled on or off.
///
/// The offset is shifted forward (toward the player's current facing direction)
/// by <see cref="N14ScopeComponent.EyeOffset"/> tiles.
/// Only affects the local player — other players' scoping is not visually simulated.
/// </summary>
public sealed class N14ScopeSystem : SharedN14ScopeSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    protected override void OnScopeActivated(Entity<N14ScopeComponent> scope, EntityUid user)
    {
        if (user != _player.LocalEntity)
            return;

        if (!TryComp<EyeComponent>(user, out var eye))
            return;

        // Compute forward vector from the player's current facing direction.
        var rotation = Transform(user).LocalRotation;
        var forward = rotation.ToWorldVec();
        var offset = forward * scope.Comp.EyeOffset;

        _eye.SetOffset(user, offset, eye);
    }

    protected override void OnScopeDeactivated(Entity<N14ScopeComponent> scope, EntityUid user)
    {
        if (user != _player.LocalEntity)
            return;

        if (!TryComp<EyeComponent>(user, out var eye))
            return;

        _eye.SetOffset(user, Vector2.Zero, eye);
    }
}
