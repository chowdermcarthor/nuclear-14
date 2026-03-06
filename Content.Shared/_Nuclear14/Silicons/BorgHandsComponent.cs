using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Nuclear14.Silicons;

/// <summary>
/// Gives a borg entity a fixed number of persistent empty hand slots on MapInit.
/// These slots remain present regardless of which module (if any) is selected,
/// allowing the borg to carry and manipulate items at all times.
/// </summary>
[RegisterComponent]
public sealed partial class BorgHandsComponent : Component
{
    [DataField]
    public int HandCount = 2;
}
