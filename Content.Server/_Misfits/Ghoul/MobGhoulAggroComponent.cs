// #Misfits Change: Tracks which player ghouls have provoked feral ghoul mobs.
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Ghoul;

[RegisterComponent]
public sealed partial class MobGhoulAggroComponent : Component
{
    public HashSet<EntityUid> ProvokedPlayerGhouls = new();
}
