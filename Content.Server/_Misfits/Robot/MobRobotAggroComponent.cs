// #Misfits Change: Tracks which player robot species have provoked hostile robot NPC mobs.
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Robot;

[RegisterComponent]
public sealed partial class MobRobotAggroComponent : Component
{
    public HashSet<EntityUid> ProvokedPlayerRobots = new();
}
