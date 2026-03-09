// #Misfits Change
using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.RichText;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client._Misfits.Chat;

/// <summary>
/// #Misfits Change — Rich text tag for clickable admin chat links (e.g. [Info] and [Ghost]).
/// Syntax: [adminlink="display text" link="payload"/]
/// When clicked, walks up the parent control tree to find an <see cref="ILinkClickHandler"/>
/// and forwards the payload string to it.
/// Only rendered when the OutputPanel is given an allowed-tags list that includes this type.
/// </summary>
[UsedImplicitly]
public sealed class AdminChatLinkTag : IMarkupTag
{
    public string Name => "adminlink";

    public bool TryGetControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var text)
            || !node.Attributes.TryGetValue("link", out var linkParameter)
            || !linkParameter.TryGetString(out var link))
        {
            control = null;
            return false;
        }

        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = Color.SkyBlue,
            DefaultCursorShape = Control.CursorShape.Hand
        };

        label.OnMouseEntered += _ => label.FontColorOverride = Color.LightCyan;
        label.OnMouseExited += _ => label.FontColorOverride = Color.SkyBlue;
        label.OnKeyBindDown += args => OnKeybindDown(args, link, label);

        control = label;
        return true;
    }

    private static void OnKeybindDown(GUIBoundKeyEventArgs args, string link, Control source)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var current = source.Parent;
        while (current != null)
        {
            if (current is ILinkClickHandler handler)
            {
                handler.HandleClick(link);
                args.Handle();
                return;
            }
            current = current.Parent;
        }

        Logger.Warning("AdminChatLinkTag: No ILinkClickHandler found in parent tree.");
    }
}
