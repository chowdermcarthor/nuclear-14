// #Misfits Change - Enriched job row with whitelist, role time, and slot controls
using System.Globalization;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.Administration.UI;

public sealed class WhitelistJobRow : PanelContainer
{
    public Action<Robust.Shared.Prototypes.ProtoId<JobPrototype>, bool>? OnSetJob;
    public Action<Robust.Shared.Prototypes.ProtoId<JobPrototype>, string>? OnAddRoleTime;
    public Action<Robust.Shared.Prototypes.ProtoId<JobPrototype>, string>? OnSetRoleTime;
    public Action<Robust.Shared.Prototypes.ProtoId<JobPrototype>, int>? OnAdjustJobSlots;

    public WhitelistJobRow(
        Robust.Shared.Prototypes.ProtoId<JobPrototype> jobId,
        JobPrototype job,
        bool whitelisted,
        TimeSpan roleTime,
        bool canManagePlaytime)
    {
        Margin = new Thickness(0, 0, 0, 2);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        // WhitelistJobRow only handles whitelist checkbox + role time.
        // Slot adjustments are managed by the separate JobSlotsWindow.

        var whitelistBox = new CheckBox
        {
            Pressed = whitelisted,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 18,
        };

        whitelistBox.OnPressed += _ => OnSetJob?.Invoke(jobId, whitelistBox.Pressed);

        var jobLabel = new Label
        {
            Text = job.LocalizedName,
            MinWidth = 180,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };

        var roleTimeLabel = new Label
        {
            Text = Loc.GetString("misfits-whitelist-search-role-time", ("time", FormatTime(roleTime))),
            MinWidth = 120,
            VerticalAlignment = VAlignment.Center,
        };

        var addTimeInput = new LineEdit
        {
            PlaceHolder = Loc.GetString("misfits-whitelist-search-add-time-placeholder"),
            MinWidth = 80,
            Editable = canManagePlaytime,
        };

        var addTimeButton = new Button
        {
            Text = Loc.GetString("misfits-whitelist-search-add-time"),
            Disabled = !canManagePlaytime,
            MinWidth = 72,
            StyleClasses = { "ButtonSquare" },
        };

        addTimeButton.OnPressed += _ =>
        {
            var timeString = addTimeInput.Text.Trim();
            if (timeString.Length == 0)
                return;

            OnAddRoleTime?.Invoke(jobId, timeString);
            addTimeInput.Text = string.Empty;
        };

        var setTimeButton = new Button
        {
            Text = Loc.GetString("misfits-whitelist-search-set-time"),
            Disabled = !canManagePlaytime,
            MinWidth = 60,
            StyleClasses = { "ButtonSquare" },
            TooltipDelay = 0,
        };
        setTimeButton.ToolTip = Loc.GetString("misfits-whitelist-search-set-time-tooltip");

        setTimeButton.OnPressed += _ =>
        {
            var timeString = addTimeInput.Text.Trim();
            if (timeString.Length == 0)
                return;

            OnSetRoleTime?.Invoke(jobId, timeString);
            addTimeInput.Text = string.Empty;
        };

        if (!job.Whitelisted)
            whitelistBox.Modulate = Color.FromHex("#cccccc");

        root.AddChild(whitelistBox);
        root.AddChild(jobLabel);
        root.AddChild(roleTimeLabel);
        root.AddChild(addTimeInput);
        root.AddChild(addTimeButton);
        root.AddChild(setTimeButton);

        AddChild(root);
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours < 1)
            return string.Format(CultureInfo.InvariantCulture, "{0:0}m", Math.Round(time.TotalMinutes));

        if (time.TotalDays < 1)
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}h", time.TotalHours);

        return string.Format(CultureInfo.InvariantCulture, "{0:0.#}d", time.TotalDays);
    }

    private static string FormatSlotText(int? slots, bool hasSlotConfiguration)
    {
        if (slots == null)
            return Loc.GetString("misfits-whitelist-search-slot-unlimited");

        var value = hasSlotConfiguration ? slots.Value : 0;
        return Loc.GetString("misfits-whitelist-search-slot-count", ("count", value));
    }
}