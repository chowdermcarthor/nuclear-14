using Content.Shared.Teleportation.Systems;

namespace Content.Server._N14.PortalAutoLink
{
    public sealed partial class PortalAutoLinkSystem : EntitySystem
    {
        [Dependency] private readonly LinkedEntitySystem _linkedEntitySystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            // #Misfits Fix — also subscribe to ComponentInit so that re-adding the component
            // in-game (e.g. via admin verbs) re-triggers linking, not just initial map load.
            SubscribeLocalEvent<PortalAutoLinkComponent, MapInitEvent>(HandleMapInitialization);
            SubscribeLocalEvent<PortalAutoLinkComponent, ComponentInit>(HandleComponentInit);
        }

        private void HandleMapInitialization(Entity<PortalAutoLinkComponent> entity, ref MapInitEvent eventArgs)
        {
            PerformAutoLink(entity, out _);
        }

        // #Misfits Add — triggered when the component is added to an already map-inited entity
        // (e.g. admin panel component add on a live server).
        private void HandleComponentInit(Entity<PortalAutoLinkComponent> entity, ref ComponentInit eventArgs)
        {
            // MapInitEvent fires first on initial load, so only run here if the map is already running.
            if (MetaData(entity).EntityLifeStage >= EntityLifeStage.MapInitialized)
                PerformAutoLink(entity, out _);
        }

        public bool PerformAutoLink(Entity<PortalAutoLinkComponent> entity, out EntityUid? linkedEntityId)
        {
            linkedEntityId = null;

            // #Misfits Fix — null/empty LinkKey means "do not link"; previously the default
            // "IgnoreMe" caused all keyless stairs to pair with each other arbitrarily.
            if (string.IsNullOrEmpty(entity.Comp.LinkKey))
                return false;

            var entityEnumerator = EntityQueryEnumerator<PortalAutoLinkComponent>();
            while (entityEnumerator.MoveNext(out var currentEntityUid, out var currentAutoLinkComponent))
            {
                if (entity.Owner == currentEntityUid)
                    continue;

                // #Misfits Fix — skip partner candidates with no key too.
                if (string.IsNullOrEmpty(currentAutoLinkComponent.LinkKey))
                    continue;

                if (entity.Comp.LinkKey == currentAutoLinkComponent.LinkKey)
                {
                    var linked = _linkedEntitySystem.TryLink(entity, currentEntityUid, false);

                    // #Misfits Fix — always remove PortalAutoLink after a TryLink attempt
                    // (success or failure) so a bad state doesn't leave the component stuck
                    // forever and cause repeated mis-links on subsequent map init events.
                    RemComp<PortalAutoLinkComponent>(currentEntityUid);
                    RemComp<PortalAutoLinkComponent>(entity);

                    if (linked)
                        linkedEntityId = currentEntityUid;

                    return linked;
                }
            }
            return false;
        }
    }
}
