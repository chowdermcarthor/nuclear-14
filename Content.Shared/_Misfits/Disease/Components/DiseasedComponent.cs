// #Misfits Add - Marker component for actively diseased entities.
// Present when the entity has at least one active disease (used for event subscriptions).

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Marker added when an entity has at least one active disease.
/// Removed when all diseases are cured. Used for efficient event queries
/// (contact spread, emote triggers, etc.).
/// </summary>
[RegisterComponent]
public sealed partial class DiseasedComponent : Component;
