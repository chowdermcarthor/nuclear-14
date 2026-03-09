// #Misfits Change
using System.Linq;
using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Organ;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Body
{
    [TestFixture]
    [TestOf(typeof(BodySystem))]
    public sealed class SpeciesTransplantTest
    {
        [Test]
        public async Task HumanAndGhoulCanExchangePartsAndOrgans()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var bodySystem = entityManager.System<BodySystem>();
            var xformSystem = entityManager.System<SharedTransformSystem>();

            var map = await pair.CreateTestMap();

            await server.WaitAssertion(() =>
            {
                var coordinates = new MapCoordinates(Vector2.Zero, map.MapId);

                var humanRecipient = entityManager.SpawnEntity("MobHuman", coordinates);
                var ghoulRecipient = entityManager.SpawnEntity("N14BaseMobGhoul", coordinates);

                var humanTorso = bodySystem.GetRootPartOrNull(humanRecipient);
                var ghoulTorso = bodySystem.GetRootPartOrNull(ghoulRecipient);

                Assert.That(humanTorso, Is.Not.Null);
                Assert.That(ghoulTorso, Is.Not.Null);

                var humanLeftArm = GetBodyPart(bodySystem, humanRecipient, BodyPartType.Arm, BodyPartSymmetry.Left);
                var ghoulLeftArm = GetBodyPart(bodySystem, ghoulRecipient, BodyPartType.Arm, BodyPartSymmetry.Left);

                DetachEntity(entityManager, xformSystem, humanLeftArm.Id);
                DetachEntity(entityManager, xformSystem, ghoulLeftArm.Id);

                var spareHumanArm = entityManager.SpawnEntity("LeftArmHuman", coordinates);
                var spareGhoulArm = entityManager.SpawnEntity("N14LeftArmGhoul", coordinates);

                Assert.That(bodySystem.CanAttachPart(ghoulTorso!.Value.Entity, "left arm", spareHumanArm), Is.True);
                Assert.That(bodySystem.AttachPart(ghoulTorso.Value.Entity, "left arm", spareHumanArm), Is.True);
                Assert.That(entityManager.GetComponent<BodyPartComponent>(spareHumanArm).Body, Is.EqualTo(ghoulRecipient));

                Assert.That(bodySystem.CanAttachPart(humanTorso!.Value.Entity, "left arm", spareGhoulArm), Is.True);
                Assert.That(bodySystem.AttachPart(humanTorso.Value.Entity, "left arm", spareGhoulArm), Is.True);
                Assert.That(entityManager.GetComponent<BodyPartComponent>(spareGhoulArm).Body, Is.EqualTo(humanRecipient));

                var humanHeart = GetRequiredOrgan(bodySystem, humanTorso.Value.Entity, typeof(HeartComponent));
                var ghoulHeart = GetRequiredOrgan(bodySystem, ghoulTorso.Value.Entity, typeof(HeartComponent));

                Assert.That(bodySystem.RemoveOrgan(humanHeart), Is.True);
                Assert.That(bodySystem.RemoveOrgan(ghoulHeart), Is.True);

                var spareHumanHeart = entityManager.SpawnEntity("OrganHumanHeart", coordinates);
                var spareGhoulHeart = entityManager.SpawnEntity("N14OrganGhoulHeart", coordinates);

                Assert.That(bodySystem.CanInsertOrgan(ghoulTorso.Value.Entity, "heart"), Is.True);
                Assert.That(bodySystem.InsertOrgan(ghoulTorso.Value.Entity, spareHumanHeart, "heart"), Is.True);
                Assert.That(entityManager.GetComponent<OrganComponent>(spareHumanHeart).Body, Is.EqualTo(ghoulRecipient));

                Assert.That(bodySystem.CanInsertOrgan(humanTorso.Value.Entity, "heart"), Is.True);
                Assert.That(bodySystem.InsertOrgan(humanTorso.Value.Entity, spareGhoulHeart, "heart"), Is.True);
                Assert.That(entityManager.GetComponent<OrganComponent>(spareGhoulHeart).Body, Is.EqualTo(humanRecipient));

                var ghoulHeartAfterTransplant = GetRequiredOrgan(bodySystem, ghoulTorso.Value.Entity, typeof(HeartComponent));
                var humanHeartAfterTransplant = GetRequiredOrgan(bodySystem, humanTorso.Value.Entity, typeof(HeartComponent));

                Assert.That(ghoulHeartAfterTransplant, Is.EqualTo(spareHumanHeart));
                Assert.That(humanHeartAfterTransplant, Is.EqualTo(spareGhoulHeart));
            });

            await pair.CleanReturnAsync();
        }

        private static (EntityUid Id, BodyPartComponent Component) GetBodyPart(
            BodySystem bodySystem,
            EntityUid body,
            BodyPartType type,
            BodyPartSymmetry symmetry)
        {
            return bodySystem.GetBodyChildrenOfType(body, type, symmetry: symmetry).Single();
        }

        private static EntityUid GetRequiredOrgan(BodySystem bodySystem, EntityUid part, Type organType)
        {
            Assert.That(bodySystem.TryGetBodyPartOrgans(part, organType, out var organs), Is.True);
            Assert.That(organs, Is.Not.Null);
            return organs!.Single().Id;
        }

        private static void DetachEntity(
            IEntityManager entityManager,
            SharedTransformSystem xformSystem,
            EntityUid uid)
        {
            xformSystem.DetachEntity(uid, entityManager.GetComponent<TransformComponent>(uid));
        }
    }
}