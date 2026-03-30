// World-target action event raised when a Sentry Bot player activates the missile launch action.
// Must be in Content.Shared so the YAML prototype serializer can resolve it on both
// client and server (entity prototypes reference this type via !type:SentryBotMissileLaunchEvent).

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Robot;

/// <summary>
/// World-target action event raised when the player activates the missile launch action
/// on a Sentry Bot chassis. Handled server-side by
/// <c>Content.Server._Misfits.Robot.SentryBotMissileLauncherSystem</c>.
/// </summary>
public sealed partial class SentryBotMissileLaunchEvent : WorldTargetActionEvent { }
