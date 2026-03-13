using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Server.Announcements.Systems;
using Content.Server._Misfits.FactionAnnounce; // #Misfits Add
using Robust.Shared.Player;

namespace Content.Server.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        private readonly AnnouncerSystem _announcer;
        private readonly ChatSystem _chatSystem;
        private readonly FactionAnnounceSystem _factionAnnounce; // #Misfits Add

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);

            _announcer = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AnnouncerSystem>();
            _chatSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();
            _factionAnnounce = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<FactionAnnounceSystem>(); // #Misfits Add
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState();
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement);
                            break;
                        // TODO: Per-station announcement support
                        case AdminAnnounceType.Station:
                            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Announce"), Filter.Broadcast(),
                                doAnnounce.Announcement, doAnnounce.Announcer, Color.Gold);
                            break;

                        // #Misfits Add — faction-targeted announcements.
                        // Builds a Filter of all players whose pawn belongs to the faction (via NpcFactionMemberComponent),
                        // then sends a coloured announcement only to them with the admin-supplied sender name.

                        case AdminAnnounceType.FactionLegion:
                        {
                            // Dark red to match Legion's colour scheme.
                            var filter = _factionAnnounce.BuildFactionFilter("CaesarLegion");
                            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Announce"), filter,
                                doAnnounce.Announcement, doAnnounce.Announcer, Color.DarkRed);
                            break;
                        }

                        case AdminAnnounceType.FactionNCR:
                        {
                            // Amber/gold to match NCR's colour scheme.
                            var filter = _factionAnnounce.BuildFactionFilter("NCR");
                            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Announce"), filter,
                                doAnnounce.Announcement, doAnnounce.Announcer, new Color(0xFB, 0xC0, 0x2D));
                            break;
                        }

                        case AdminAnnounceType.FactionBOS:
                        {
                            // Steel blue to match Brotherhood's colour scheme.
                            var filter = _factionAnnounce.BuildFactionFilter("BrotherhoodOfSteel");
                            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Announce"), filter,
                                doAnnounce.Announcement, doAnnounce.Announcer, new Color(0x3B, 0x72, 0xBF));
                            break;
                        }

                        case AdminAnnounceType.FactionEnclave:
                        {
                            // Yellow-green to match Enclave's colour scheme.
                            var filter = _factionAnnounce.BuildFactionFilter("Enclave");
                            _announcer.SendAnnouncement(_announcer.GetAnnouncementId("Announce"), filter,
                                doAnnounce.Announcement, doAnnounce.Announcer, new Color(0xC6, 0xCF, 0x00));
                            break;
                        }
                        // End #Misfits Add
                    }

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
