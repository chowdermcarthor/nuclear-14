// #Misfits Add - Disease effect: show a localized popup message to the afflicted entity.
// Used for symptoms like "You feel nauseous...", "Your skin itches...", etc.

using Content.Shared._Misfits.Disease;
using Content.Shared.Popups;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Shows a localized popup message to the diseased entity. Used for flavor text
/// symptoms at various disease stages.
/// </summary>

public sealed partial class DiseasePopUp : DiseaseEffect
{
    /// <summary>Localization key for the popup message.</summary>
    [DataField(required: true)]
    public string Message { get; private set; } = string.Empty;

    /// <summary>Popup type (determines visual style).</summary>
    [DataField]
    public PopupType Type { get; private set; } = PopupType.Small;

    public override void Effect(DiseaseEffectArgs args)
    {
        var popup = args.EntityManager.System<SharedPopupSystem>();
        popup.PopupEntity(Loc.GetString(Message), args.DiseasedEntity, args.DiseasedEntity, Type);
    }
}
