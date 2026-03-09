// #Misfits Change
using Content.Server._Misfits.RadiationWarning;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Radiation.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Misfits.RadiationWarning;

/// <summary>
/// Sends private, nameless ambient-flavour messages (like /do) to humanoid
/// players when they stand in radiation fields. No name is attached — the
/// message appears in the local-chat box the same way a subtle admin prayer
/// does. Only the player being irradiated sees their message.
///
/// Tier thresholds (radsPerSecond):
///   0 = mild   (>0.05): faint unease
///   1 = moderate (>0.40): nausea / headache
///   2 = significant (>1.00): hair loss / skin burns
///   3 = severe  (>2.50): "this will kill you"
/// </summary>
public sealed class RadiationWarningSystem : EntitySystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActorSystem _actor = default!;

    // Localisation keys per tier (chosen randomly each time)
    private static readonly string[][] TierMessages =
    {
        // Tier 0 — mild
        new[]
        {
            "rad-warning-tier0-a",
            "rad-warning-tier0-b",
            "rad-warning-tier0-c",
        },
        // Tier 1 — moderate
        new[]
        {
            "rad-warning-tier1-a",
            "rad-warning-tier1-b",
            "rad-warning-tier1-c",
        },
        // Tier 2 — significant
        new[]
        {
            "rad-warning-tier2-a",
            "rad-warning-tier2-b",
            "rad-warning-tier2-c",
        },
        // Tier 3 — severe
        new[]
        {
            "rad-warning-tier3-a",
            "rad-warning-tier3-b",
            "rad-warning-tier3-c",
        },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, OnIrradiatedEvent>(OnIrradiated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RadiationWarningComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            for (var i = 0; i < comp.TierCooldowns.Length; i++)
            {
                if (comp.TierCooldowns[i] > 0f)
                    comp.TierCooldowns[i] = MathF.Max(0f, comp.TierCooldowns[i] - frameTime);
            }
        }
    }

    private void OnIrradiated(EntityUid uid, HumanoidAppearanceComponent _, OnIrradiatedEvent args)
    {
        // Only send to living player-controlled humanoids
        if (!_actor.TryGetSession(uid, out var session))
            return;

        var comp = EnsureComp<RadiationWarningComponent>(uid);

        // Determine highest eligible tier
        var tier = -1;
        for (var i = comp.TierThresholds.Length - 1; i >= 0; i--)
        {
            if (args.RadsPerSecond >= comp.TierThresholds[i])
            {
                tier = i;
                break;
            }
        }

        if (tier < 0)
            return;

        // Respect per-tier cooldown
        if (comp.TierCooldowns[tier] > 0f)
            return;

        comp.TierCooldowns[tier] = comp.TierCooldownTimes[tier];

        // Pick a random message from the tier list
        var messages = TierMessages[tier];
        var key = messages[_random.Next(messages.Length)];
        var text = Loc.GetString(key);

        // Send as nameless local-chat message — identical channel to subtle prayer messages.
        // EntityUid.Invalid means no speech bubble and no entity name is prepended.
        _chatManager.ChatMessageToOne(
            ChatChannel.Local,
            text,
            text,
            EntityUid.Invalid,
            false,
            session!.Channel);
    }
}
