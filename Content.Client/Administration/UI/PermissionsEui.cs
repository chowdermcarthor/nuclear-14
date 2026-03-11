using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Client.Administration.Managers;
using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;
using static Content.Shared.Administration.PermissionsEuiMsg;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Administration.UI
{
    [UsedImplicitly]
    public sealed class PermissionsEui : BaseEui
    {
        private const int NoRank = -1;

        [Dependency] private readonly IClientAdminManager _adminManager = default!;

        // #Misfits Change — short descriptions shown next to each flag in the rank editor
        private static readonly Dictionary<AdminFlags, string> FlagDescriptions = new()
        {
            { AdminFlags.Admin,       "Basic admin verbs" },
            { AdminFlags.Ban,         "Ban/unban players" },
            { AdminFlags.Debug,       "Developer debug commands" },
            { AdminFlags.Fun,         "Events & fun commands" },
            { AdminFlags.Permissions, "Edit admin permissions" },
            { AdminFlags.Server,      "Restart/manage server" },
            { AdminFlags.Spawn,       "Spawn entities in-game" },
            { AdminFlags.VarEdit,     "Use VarView (VV)" },
            { AdminFlags.Mapping,     "Large mapping operations" },
            { AdminFlags.Logs,        "View admin/server logs" },
            { AdminFlags.Round,       "Force map/round management" },
            { AdminFlags.Query,       "Run BQL queries" },
            { AdminFlags.Adminhelp,   "Use the ahelp system" },
            { AdminFlags.ViewNotes,   "View player notes — also grants MHelp access and shows staff as Mentor in /admins" }, // #Misfits Change — ViewNotes is the gate for MHelp and Mentor categorisation
            { AdminFlags.EditNotes,   "Create/edit player notes" },
            { AdminFlags.MassBan,     "Ban multiple players at once" },
            { AdminFlags.Stealth,     "Hide from non-stealth admins" },
            { AdminFlags.Adminchat,   "Use admin chat" },
            { AdminFlags.Pii,         "View IPs & HWIDs" },
            { AdminFlags.Whitelist,   "Manage the whitelist" },
            { AdminFlags.Mentorhelp,  "(Unused — MHelp access and Mentor listing are both handled by VIEWNOTES)" }, // #Misfits Change — superseded
            { AdminFlags.Host,        "Full host-level access (dangerous)" },
        };

        private readonly Menu _menu;
        private readonly List<DefaultWindow> _subWindows = new();

        private Dictionary<int, PermissionsEuiState.AdminRankData> _ranks =
            new();
        private PermissionsEuiState.SearchPlayerData[] _searchResults = Array.Empty<PermissionsEuiState.SearchPlayerData>();

        public PermissionsEui()
        {
            IoCManager.InjectDependencies(this);

            _menu = new Menu(this);
            _menu.AddAdminButton.OnPressed += AddAdminPressed;
            _menu.AddAdminRankButton.OnPressed += AddAdminRankPressed;
            _menu.OnClose += CloseEverything;
        }

        public override void Closed()
        {
            base.Closed();

            SendMessage(new CloseEuiMessage());
            CloseEverything();
        }

        private void CloseEverything()
        {
            foreach (var subWindow in _subWindows.ToArray())
            {
                subWindow.Close();
            }

            _menu.Close();
        }

        private void AddAdminPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenEditWindow(null);
        }

        private void AddAdminRankPressed(BaseButton.ButtonEventArgs obj)
        {
            OpenRankEditWindow(null);
        }


        private void OnEditPressed(PermissionsEuiState.AdminData admin)
        {
            OpenEditWindow(admin);
        }

        private void OpenEditWindow(PermissionsEuiState.AdminData? data)
        {
            var window = new EditAdminWindow(this, data);
            window.SaveButton.OnPressed += _ => SaveAdminPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (data != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveButtonPressed(window);
            }

            _subWindows.Add(window);
        }


        private void OpenRankEditWindow(KeyValuePair<int, PermissionsEuiState.AdminRankData>? rank)
        {
            var window = new EditAdminRankWindow(this, rank);
            window.SaveButton.OnPressed += _ => SaveAdminRankPressed(window);
            window.OpenCentered();
            window.OnClose += () => _subWindows.Remove(window);
            if (rank != null)
            {
                window.RemoveButton!.OnPressed += _ => RemoveRankButtonPressed(window);
            }

            _subWindows.Add(window);
        }

        private void RemoveButtonPressed(EditAdminWindow window)
        {
            SendMessage(new RemoveAdmin { UserId = window.SourceData!.Value.UserId });

            window.Close();
        }

        private void RemoveRankButtonPressed(EditAdminRankWindow window)
        {
            SendMessage(new RemoveAdminRank { Id = window.SourceId!.Value });

            window.Close();
        }

        private void SaveAdminPressed(EditAdminWindow popup)
        {
            popup.CollectSetFlags(out var pos, out var neg);

            int? rank = popup.RankButton.SelectedId;
            if (rank == NoRank)
            {
                rank = null;
            }

            var title = string.IsNullOrWhiteSpace(popup.TitleEdit.Text) ? null : popup.TitleEdit.Text;

            if (popup.SourceData is { } src)
            {
                SendMessage(new UpdateAdmin
                {
                    UserId = src.UserId,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank
                });
            }
            else
            {
                DebugTools.AssertNotNull(popup.NameEdit);

                SendMessage(new AddAdmin
                {
                    UserId = popup.SelectedUserId,
                    UserNameOrId = popup.NameEdit!.Text,
                    Title = title,
                    PosFlags = pos,
                    NegFlags = neg,
                    RankId = rank
                });
            }

            popup.Close();
        }


        private void SaveAdminRankPressed(EditAdminRankWindow popup)
        {
            var flags = popup.CollectSetFlags();
            var name = popup.NameEdit.Text;

            if (popup.SourceId is { } src)
            {
                SendMessage(new UpdateAdminRank
                {
                    Id = src,
                    Flags = flags,
                    Name = name
                });
            }
            else
            {
                SendMessage(new AddAdminRank
                {
                    Flags = flags,
                    Name = name
                });
            }

            popup.Close();
        }

        public override void Opened()
        {
            _menu.OpenCentered();
        }

        public override void HandleState(EuiStateBase state)
        {
            var s = (PermissionsEuiState) state;

            if (s.IsLoading)
            {
                return;
            }

            _ranks = s.AdminRanks;
            _searchResults = s.SearchResults;

            foreach (var window in _subWindows.OfType<EditAdminWindow>())
            {
                window.UpdateSearchResults(_searchResults);
            }

            // ---- Admins tab: group by rank, sort groups by total perm count descending ----
            // #Misfits Change — rank-grouped card layout instead of flat grid
            _menu.AdminsList.RemoveAllChildren();

            // Compute combined flags per admin for sorting
            AdminFlags CombinedFlags(PermissionsEuiState.AdminData a)
            {
                var f = a.PosFlags;
                if (a.RankId is { } rid && s.AdminRanks.TryGetValue(rid, out var rd))
                    f |= rd.Flags;
                return f;
            }

            // Group admins by rank id (null = unranked)
            var grouped = s.Admins
                .OrderByDescending(a => BitOperations.PopCount((uint) CombinedFlags(a)))
                .GroupBy(a => a.RankId)
                .OrderByDescending(g =>
                {
                    // Order groups by the highest perm count in the group
                    return g.Max(a => BitOperations.PopCount((uint) CombinedFlags(a)));
                });

            foreach (var group in grouped)
            {
                string rankName;
                int rankPermCount;
                if (group.Key is { } rankId && s.AdminRanks.TryGetValue(rankId, out var rankData))
                {
                    rankName = rankData.Name;
                    rankPermCount = BitOperations.PopCount((uint) rankData.Flags);
                }
                else
                {
                    rankName = Loc.GetString("permissions-eui-edit-no-rank-text");
                    rankPermCount = 0;
                }

                // Rank group header
                var headerBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(0, 6, 0, 2),
                    HorizontalExpand = true,
                };
                headerBox.AddChild(new Label
                {
                    Text = $"{rankName}",
                    StyleClasses = { StyleBase.StyleClassLabelHeading },
                    HorizontalExpand = true,
                });
                headerBox.AddChild(new Label
                {
                    Text = Loc.GetString("permissions-eui-rank-perms-count", ("count", rankPermCount)),
                    StyleClasses = { StyleBase.StyleClassItalic },
                    Margin = new Thickness(8, 0, 0, 0),
                });
                _menu.AdminsList.AddChild(headerBox);

                // Separator line
                _menu.AdminsList.AddChild(new PanelContainer
                {
                    StyleClasses = { "LowDivider" },
                    Margin = new Thickness(0, 0, 0, 4),
                });

                // Admin entries in this group
                foreach (var admin in group)
                {
                    var combinedFlags = CombinedFlags(admin);
                    var name = admin.UserName ?? admin.UserId.ToString();
                    var canEdit = _adminManager.HasFlag(combinedFlags);

                    var row = new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        Margin = new Thickness(12, 2, 0, 2),
                        HorizontalExpand = true,
                    };

                    // Clickable name button
                    var capturedAdmin = admin;
                    var nameButton = new Button
                    {
                        Text = name,
                        Disabled = !canEdit,
                        MinWidth = 160,
                    };
                    nameButton.OnPressed += _ => OnEditPressed(capturedAdmin);
                    if (!canEdit)
                        nameButton.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-admin-tooltip");
                    row.AddChild(nameButton);

                    // Title
                    var titleText = admin.Title ?? Loc.GetString("permissions-eui-edit-admin-title-control-text").ToLowerInvariant();
                    var titleLabel = new Label
                    {
                        Text = $"[{titleText}]",
                        Margin = new Thickness(8, 0, 0, 0),
                        HorizontalExpand = true,
                        VerticalAlignment = Control.VAlignment.Center,
                    };
                    if (admin.Title == null)
                        titleLabel.StyleClasses.Add(StyleBase.StyleClassItalic);
                    row.AddChild(titleLabel);

                    // Perm count badge
                    var permCount = BitOperations.PopCount((uint) combinedFlags);
                    row.AddChild(new Label
                    {
                        Text = Loc.GetString("permissions-eui-perms-badge", ("count", permCount)),
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = Control.VAlignment.Center,
                    });

                    _menu.AdminsList.AddChild(row);
                }
            }

            // ---- Ranks tab: card-style rank list ----
            // #Misfits Change — card-style rank list sorted by perm count descending
            _menu.AdminRanksList.RemoveAllChildren();
            foreach (var kv in s.AdminRanks.OrderByDescending(r => BitOperations.PopCount((uint) r.Value.Flags)))
            {
                var rank = kv.Value;
                var flagCount = BitOperations.PopCount((uint) rank.Flags);
                var flagsText = string.Join(", ", AdminFlagsHelper.FlagsToNames(rank.Flags));

                var card = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Margin = new Thickness(0, 4, 0, 4),
                    HorizontalExpand = true,
                };

                // Rank header row
                var headerRow = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                };
                headerRow.AddChild(new Label
                {
                    Text = rank.Name,
                    StyleClasses = { StyleBase.StyleClassLabelHeading },
                    HorizontalExpand = true,
                });
                headerRow.AddChild(new Label
                {
                    Text = Loc.GetString("permissions-eui-rank-perms-count", ("count", flagCount)),
                    Margin = new Thickness(8, 0, 0, 0),
                });

                var editButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-edit-admin-rank-button"),
                    Margin = new Thickness(8, 0, 0, 0),
                };
                var capturedKv = kv;
                editButton.OnPressed += _ => OnEditRankPressed(capturedKv);
                if (!_adminManager.HasFlag(rank.Flags))
                {
                    editButton.Disabled = true;
                    editButton.ToolTip = Loc.GetString("permissions-eui-do-not-have-required-flags-to-edit-rank-tooltip");
                }
                headerRow.AddChild(editButton);

                card.AddChild(headerRow);

                // Flags detail line
                card.AddChild(new Label
                {
                    Text = flagsText,
                    StyleClasses = { StyleBase.StyleClassItalic },
                    Margin = new Thickness(12, 2, 0, 0),
                });

                // Separator
                card.AddChild(new PanelContainer
                {
                    StyleClasses = { "LowDivider" },
                    Margin = new Thickness(0, 4, 0, 0),
                });

                _menu.AdminRanksList.AddChild(card);
            }
        }

        private void OnEditRankPressed(KeyValuePair<int, PermissionsEuiState.AdminRankData> rank)
        {
            OpenRankEditWindow(rank);
        }

        // #Misfits Change — completely overhauled Menu to use rank-grouped card layout
        private sealed class Menu : DefaultWindow
        {
            private readonly PermissionsEui _ui;
            public readonly BoxContainer AdminsList;
            public readonly BoxContainer AdminRanksList;
            public readonly Button AddAdminButton;
            public readonly Button AddAdminRankButton;

            public Menu(PermissionsEui ui)
            {
                _ui = ui;
                Title = Loc.GetString("permissions-eui-menu-title");

                var tab = new TabContainer();

                AddAdminButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-add-admin-button"),
                    HorizontalAlignment = HAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0),
                };

                AddAdminRankButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-add-admin-rank-button"),
                    HorizontalAlignment = HAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0),
                };

                AdminsList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                };
                var adminVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new ScrollContainer { VerticalExpand = true, Children = { AdminsList } },
                        AddAdminButton,
                    },
                };
                TabContainer.SetTabTitle(adminVBox, Loc.GetString("permissions-eui-menu-admins-tab-title"));

                AdminRanksList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                };
                var rankVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new ScrollContainer { VerticalExpand = true, Children = { AdminRanksList } },
                        AddAdminRankButton,
                    },
                };
                TabContainer.SetTabTitle(rankVBox, Loc.GetString("permissions-eui-menu-admin-ranks-tab-title"));

                tab.AddChild(adminVBox);
                tab.AddChild(rankVBox);

                Contents.AddChild(tab);
            }

            protected override Vector2 ContentsMinimumSize => new Vector2(700, 500);
        }

        private sealed class EditAdminWindow : DefaultWindow
        {
            public readonly PermissionsEuiState.AdminData? SourceData;
            public readonly LineEdit? NameEdit;
            public readonly LineEdit TitleEdit;
            public readonly OptionButton RankButton;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            public readonly BoxContainer SearchResultsList;
            public NetUserId? SelectedUserId;

            public readonly Dictionary<AdminFlags, (Button inherit, Button sub, Button plus)> FlagButtons
                = new();

            private CancellationTokenSource? _searchDebounce;
            private bool _suppressSearchTextChanged;
            private readonly PermissionsEui _ui;

            public EditAdminWindow(PermissionsEui ui, PermissionsEuiState.AdminData? data)
            {
                MinSize = new Vector2(600, 400);
                _ui = ui;
                SourceData = data;

                Control nameControl;

                if (data is { } dat)
                {
                    var name = dat.UserName ?? dat.UserId.ToString();
                    Title = Loc.GetString("permissions-eui-edit-admin-window-edit-admin-label",
                                          ("admin", name));

                    nameControl = new Label { Text = name };
                }
                else
                {
                    Title = Loc.GetString("permissions-eui-menu-add-admin-button");

                    nameControl = NameEdit = new LineEdit { PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-name-edit-placeholder") };
                    NameEdit.OnTextChanged += NameEditOnTextChanged;
                    NameEdit.OnTextEntered += NameEditOnTextEntered;
                }

                TitleEdit = new LineEdit { PlaceHolder = Loc.GetString("permissions-eui-edit-admin-window-title-edit-placeholder") };
                RankButton = new OptionButton();
                SaveButton = new Button { Text = Loc.GetString("permissions-eui-edit-admin-window-save-button"), HorizontalAlignment = HAlignment.Right };
                SearchResultsList = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    SeparationOverride = 2,
                };

                RankButton.AddItem(Loc.GetString("permissions-eui-edit-admin-window-no-rank-button"), NoRank);
                foreach (var (rId, rank) in ui._ranks)
                {
                    RankButton.AddItem(rank.Name, rId);
                }

                RankButton.SelectId(data?.RankId ?? NoRank);
                RankButton.OnItemSelected += RankSelected;

                var permGrid = new GridContainer
                {
                    Columns = 4,
                    HSeparationOverride = 0,
                    VSeparationOverride = 0
                };

                // #Misfits Change — HOST implies all flags; prevents newly added custom flags from being
                //                  permanently uneditable for HOST admins who were promoted before the flag existed.
                var editorIsHost = ui._adminManager.HasFlag(AdminFlags.Host);

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !editorIsHost && !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    // #Misfits Change — tooltip shows short description for each flag
                    var flagTooltip = FlagDescriptions.TryGetValue(flag, out var flagLabelDesc) ? flagLabelDesc : null;

                    var group = new ButtonGroup();

                    var inherit = new Button
                    {
                        Text = "I",
                        StyleClasses = { StyleBase.ButtonOpenRight },
                        Disabled = disable,
                        Group = group,
                    };
                    var sub = new Button
                    {
                        Text = "-",
                        StyleClasses = { StyleBase.ButtonOpenBoth },
                        Disabled = disable,
                        Group = group
                    };
                    var plus = new Button
                    {
                        Text = "+",
                        StyleClasses = { StyleBase.ButtonOpenLeft },
                        Disabled = disable,
                        Group = group
                    };

                    if (data is { } d)
                    {
                        if ((d.NegFlags & flag) != 0)
                        {
                            sub.Pressed = true;
                        }
                        else if ((d.PosFlags & flag) != 0)
                        {
                            plus.Pressed = true;
                        }
                        else
                        {
                            inherit.Pressed = true;
                        }
                    }
                    else
                    {
                        inherit.Pressed = true;
                    }

                    permGrid.AddChild(new Label { Text = flagName, ToolTip = flagTooltip }); // #Misfits Change
                    permGrid.AddChild(inherit);
                    permGrid.AddChild(sub);
                    permGrid.AddChild(plus);

                    FlagButtons.Add(flag, (inherit, sub, plus));
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal
                };
                if (data != null)
                {
                    // show remove button.
                    RemoveButton = new Button { Text = Loc.GetString("permissions-eui-edit-admin-window-remove-flag-button") };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(SaveButton);

                var detailsBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    Children =
                    {
                        nameControl,
                        new ScrollContainer
                        {
                            VerticalExpand = true,
                            MinSize = new Vector2(220, 140),
                            Children = { SearchResultsList }
                        },
                        TitleEdit,
                        RankButton
                    }
                };

                if (data != null)
                {
                    SearchResultsList.Visible = false;
                }
                else
                {
                    UpdateSearchResults(ui._searchResults);
                }

                Contents.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 2,
                            Children =
                            {
                                detailsBox,
                                permGrid
                            },
                            VerticalExpand = true
                        },
                        bottomButtons
                    }
                });
            }

            private void RankSelected(OptionButton.ItemSelectedEventArgs obj)
            {
                RankButton.SelectId(obj.Id);
            }

            private void NameEditOnTextEntered(LineEdit.LineEditEventArgs args)
            {
                QueueSearch(args.Text);
            }

            private void NameEditOnTextChanged(LineEdit.LineEditEventArgs args)
            {
                if (_suppressSearchTextChanged)
                    return;

                SelectedUserId = null;
                QueueSearch(args.Text);
            }

            private void QueueSearch(string text)
            {
                _searchDebounce?.Cancel();

                var query = text.Trim();
                if (query.Length < 2)
                {
                    UpdateSearchResults(Array.Empty<PermissionsEuiState.SearchPlayerData>());
                    return;
                }

                _searchDebounce = new CancellationTokenSource();
                Timer.Spawn(300, () =>
                {
                    _ui.SendMessage(new SearchPlayers { Query = query });
                }, _searchDebounce.Token);
            }

            public void UpdateSearchResults(IReadOnlyList<PermissionsEuiState.SearchPlayerData> results)
            {
                if (SourceData != null)
                    return;

                SearchResultsList.RemoveAllChildren();

                if (results.Count == 0)
                {
                    SearchResultsList.AddChild(new Label
                    {
                        Text = Loc.GetString("permissions-eui-edit-admin-window-search-empty"),
                        StyleClasses = { StyleBase.StyleClassItalic },
                    });
                    return;
                }

                foreach (var result in results)
                {
                    var button = new Button
                    {
                        Text = result.UserName,
                        HorizontalExpand = true,
                        ToolTip = result.UserId.ToString(),
                    };

                    var captured = result;
                    button.OnPressed += _ => SelectSearchResult(captured);
                    SearchResultsList.AddChild(button);
                }
            }

            private void SelectSearchResult(PermissionsEuiState.SearchPlayerData result)
            {
                SelectedUserId = result.UserId;
                if (NameEdit != null)
                {
                    _suppressSearchTextChanged = true;
                    NameEdit.Text = result.UserName;
                    _suppressSearchTextChanged = false;
                }
            }

            public void CollectSetFlags(out AdminFlags pos, out AdminFlags neg)
            {
                pos = default;
                neg = default;

                foreach (var (flag, (_, s, p)) in FlagButtons)
                {
                    if (s.Pressed)
                    {
                        neg |= flag;
                    }
                    else if (p.Pressed)
                    {
                        pos |= flag;
                    }
                }
            }

            [Obsolete]
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                if (!disposing)
                    return;

                _searchDebounce?.Cancel();
                if (NameEdit != null)
                {
                    NameEdit.OnTextChanged -= NameEditOnTextChanged;
                    NameEdit.OnTextEntered -= NameEditOnTextEntered;
                }
            }
        }

        private sealed class EditAdminRankWindow : DefaultWindow
        {
            public readonly int? SourceId;
            public readonly LineEdit NameEdit;
            public readonly Button SaveButton;
            public readonly Button? RemoveButton;
            public readonly Dictionary<AdminFlags, CheckBox> FlagCheckBoxes = new();

            public EditAdminRankWindow(PermissionsEui ui, KeyValuePair<int, PermissionsEuiState.AdminRankData>? data)
            {
                Title = Loc.GetString("permissions-eui-edit-admin-rank-window-title");
                MinSize = new Vector2(600, 400);
                SourceId = data?.Key;

                NameEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("permissions-eui-edit-admin-rank-window-name-edit-placeholder"),
                };

                if (data != null)
                {
                    NameEdit.Text = data.Value.Value.Name;
                }

                SaveButton = new Button
                {
                    Text = Loc.GetString("permissions-eui-menu-save-admin-rank-button"),
                    HorizontalAlignment = HAlignment.Right,
                    HorizontalExpand = true,
                };
                var flagsBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical
                };

                // #Misfits Change — HOST implies all flags; prevents newly added custom flags from being
                //                  permanently uneditable for HOST admins who were promoted before the flag existed.
                var editorIsHost = ui._adminManager.HasFlag(AdminFlags.Host);

                foreach (var flag in AdminFlagsHelper.AllFlags)
                {
                    // Can only grant out perms you also have yourself.
                    // Primarily intended to prevent people giving themselves +HOST with +PERMISSIONS but generalized.
                    var disable = !editorIsHost && !ui._adminManager.HasFlag(flag);
                    var flagName = flag.ToString().ToUpper();

                    // #Misfits Change — show short description alongside the flag name
                    var checkBoxText = FlagDescriptions.TryGetValue(flag, out var flagDesc)
                        ? $"{flagName} — {flagDesc}"
                        : flagName;

                    var checkBox = new CheckBox
                    {
                        Disabled = disable,
                        Text = checkBoxText
                    };

                    if (data != null && (data.Value.Value.Flags & flag) != 0)
                    {
                        checkBox.Pressed = true;
                    }

                    FlagCheckBoxes.Add(flag, checkBox);
                    flagsBox.AddChild(checkBox);
                }

                var bottomButtons = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal
                };
                if (data != null)
                {
                    // show remove button.
                    RemoveButton = new Button { Text = Loc.GetString("permissions-eui-menu-remove-admin-rank-button") };
                    bottomButtons.AddChild(RemoveButton);
                }

                bottomButtons.AddChild(SaveButton);

                Contents.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        NameEdit,
                        flagsBox,
                        bottomButtons
                    }
                });
            }

            public AdminFlags CollectSetFlags()
            {
                AdminFlags flags = default;
                foreach (var (flag, chk) in FlagCheckBoxes)
                {
                    if (chk.Pressed)
                    {
                        flags |= flag;
                    }
                }

                return flags;
            }
        }
    }
}
