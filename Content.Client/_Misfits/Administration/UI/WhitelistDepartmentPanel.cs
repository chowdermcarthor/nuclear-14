// #Misfits Change - Department panel for enriched whitelist administration rows
using System.Linq;
using Content.Shared._Misfits.Administration;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.Administration.UI;

public sealed class WhitelistDepartmentPanel : PanelContainer
{
    public Action<ProtoId<JobPrototype>, bool>? OnSetJob;
    public Action<ProtoId<JobPrototype>, string>? OnAddRoleTime;
    public Action<ProtoId<JobPrototype>, string>? OnSetRoleTime;
    public Action<ProtoId<JobPrototype>, int>? OnAdjustJobSlots;
    public Action<string, string>? OnAddDeptTime;
    public Action<string, string>? OnSetDeptTime;

    private readonly List<(string JobName, WhitelistJobRow Row)> _rows = new();
    private readonly BoxContainer _jobs;
    private readonly string _departmentId;

    public WhitelistDepartmentPanel(
        DepartmentPrototype department,
        IPrototypeManager proto,
        HashSet<ProtoId<JobPrototype>> whitelists,
        IReadOnlyDictionary<ProtoId<JobPrototype>, WhitelistJobAdminInfo> adminInfo,
        bool canManagePlaytime)
    {
        _departmentId = department.ID;
        Margin = new Thickness(0, 0, 0, 6);
        StyleClasses.Add("BackgroundDark");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Margin = new Thickness(6),
        };

        var allWhitelisted = department.Roles.All(whitelists.Contains);
        var headerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        var header = new Button
        {
            Text = Loc.GetString(department.ID),
            ToggleMode = true,
            Pressed = allWhitelisted,
            HorizontalExpand = true,
            Modulate = department.Color,
        };

        // Bulk time controls in header
        var deptTimeInput = new LineEdit
        {
            PlaceHolder = "e.g. 1h30m",
            MinWidth = 80,
            Editable = canManagePlaytime,
            VerticalAlignment = VAlignment.Center,
        };

        var addAllButton = new Button
        {
            Text = Loc.GetString("misfits-whitelist-search-dept-add-all"),
            Disabled = !canManagePlaytime,
            MinWidth = 80,
            StyleClasses = { "ButtonSquare" },
        };
        addAllButton.ToolTip = Loc.GetString("misfits-whitelist-search-dept-add-all-tooltip");

        addAllButton.OnPressed += _ =>
        {
            var t = deptTimeInput.Text.Trim();
            if (t.Length == 0) return;
            OnAddDeptTime?.Invoke(_departmentId, t);
            deptTimeInput.Text = string.Empty;
        };

        var setAllButton = new Button
        {
            Text = Loc.GetString("misfits-whitelist-search-dept-set-all"),
            Disabled = !canManagePlaytime,
            MinWidth = 80,
            StyleClasses = { "ButtonSquare" },
        };
        setAllButton.ToolTip = Loc.GetString("misfits-whitelist-search-dept-set-all-tooltip");

        setAllButton.OnPressed += _ =>
        {
            var t = deptTimeInput.Text.Trim();
            if (t.Length == 0) return;
            OnSetDeptTime?.Invoke(_departmentId, t);
            deptTimeInput.Text = string.Empty;
        };

        headerRow.AddChild(header);
        if (canManagePlaytime)
        {
            headerRow.AddChild(deptTimeInput);
            headerRow.AddChild(addAllButton);
            headerRow.AddChild(setAllButton);
        }

        _jobs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };

        foreach (var (jobProtoId, job) in department.Roles
                     .Select(id => (Id: id, Job: proto.Index<JobPrototype>(id)))
                     .OrderByDescending(entry => entry.Job.RealDisplayWeight)
                     .ThenBy(entry => entry.Job.ID))
        {
            adminInfo.TryGetValue(jobProtoId, out var info);

            var row = new WhitelistJobRow(
                jobProtoId,
                job,
                whitelists.Contains(jobProtoId),
                info?.RoleTime ?? TimeSpan.Zero,
                canManagePlaytime);

            row.OnSetJob += (id, enabled) => OnSetJob?.Invoke(id, enabled);
            row.OnAddRoleTime += (id, timeString) => OnAddRoleTime?.Invoke(id, timeString);
            row.OnSetRoleTime += (id, timeString) => OnSetRoleTime?.Invoke(id, timeString);
            row.OnAdjustJobSlots += (id, delta) => OnAdjustJobSlots?.Invoke(id, delta);
            _rows.Add((job.LocalizedName, row));
            _jobs.AddChild(row);
        }

        header.OnPressed += _ =>
        {
            foreach (var id in department.Roles)
            {
                if (whitelists.Contains(id) == header.Pressed)
                    continue;

                OnSetJob?.Invoke(id, header.Pressed);
            }
        };

        root.AddChild(headerRow);
        root.AddChild(_jobs);
        AddChild(root);
    }

    /// <summary>
    /// Shows/hides individual job rows based on the filter string.
    /// Returns true if any rows are visible.
    /// </summary>
    public bool Filter(string filter)
    {
        var empty = string.IsNullOrWhiteSpace(filter);
        var anyVisible = false;
        foreach (var (name, row) in _rows)
        {
            var visible = empty || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            row.Visible = visible;
            if (visible)
                anyVisible = true;
        }
        Visible = anyVisible;
        return anyVisible;
    }
}