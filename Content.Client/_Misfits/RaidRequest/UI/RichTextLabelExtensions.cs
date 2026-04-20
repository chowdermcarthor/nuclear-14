// #Misfits Add - Convenience extension so /raid UI code can call rtl.SetMarkup("..."),
// matching the pattern used elsewhere in newer SS14 forks. Wraps the engine's
// SetMessage(FormattedMessage.FromMarkup(...)) plumbing.

using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Misfits.RaidRequest.UI;

internal static class RichTextLabelExtensions
{
    public static void SetMarkup(this RichTextLabel label, string markup)
    {
        // FromMarkupPermissive avoids exceptions if a stray bracket sneaks through user input;
        // EscapeText callers already sanitize, but defense in depth is cheap here.
        label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
    }
}
