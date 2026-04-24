using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Localizations;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Lathe;

/// <summary>
/// This handles...
/// </summary>
public abstract class SharedLatheSystem : EntitySystem
{
    // #Misfits Change Fix: Workbench craftability should include raw materials stored in the
    // bench's Storage container, not only the internal MaterialStorage pool.
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;

    private readonly Dictionary<string, List<LatheRecipePrototype>> _inverseRecipeDictionary = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmagLatheRecipesComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<LatheComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildInverseRecipeDictionary();
    }

    private void OnExamined(Entity<LatheComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.ReagentOutputSlotId != null)
            args.PushMarkup(Loc.GetString("lathe-menu-reagent-slot-examine"));
    }

    [PublicAPI]
    public bool CanProduce(EntityUid uid, string recipe, int amount = 1, LatheComponent? component = null)
    {
        return _proto.TryIndex<LatheRecipePrototype>(recipe, out var proto) && CanProduce(uid, proto, amount, component);
    }

    public bool CanProduce(EntityUid uid, LatheRecipePrototype recipe, int amount = 1, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (!HasRecipe(uid, recipe, component))
            return false;

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.ApplyMaterialDiscount, component.MaterialUseMultiplier);

            if (_materialStorage.GetAvailableMaterialAmount(uid, material) < adjustedAmount * amount)
                return false;
        }
        return true;
    }

    /// <summary>
    /// #Misfits Change Add: Overload that checks craftability using a pre-computed
    /// material amounts dictionary (server-provided) rather than querying the entity
    /// directly.  Used client-side so recipe buttons correctly reflect materials that
    /// are physically stored in attached storage containers but whose composition
    /// components may not be fully synced to the client yet.
    /// </summary>
    public bool CanProduce(EntityUid uid, LatheRecipePrototype recipe, int amount, Dictionary<ProtoId<MaterialPrototype>, int> availableMaterials, LatheComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (!HasRecipe(uid, recipe, component))
            return false;

        foreach (var (material, needed) in recipe.Materials)
        {
            var adjustedAmount = AdjustMaterial(needed, recipe.ApplyMaterialDiscount, component.MaterialUseMultiplier);
            var available = availableMaterials.GetValueOrDefault(material, 0);
            if (available < adjustedAmount * amount)
                return false;
        }
        return true;
    }

    private void OnEmagged(EntityUid uid, EmagLatheRecipesComponent component, ref GotEmaggedEvent args)
    {
        args.Handled = true;
    }

    public static int AdjustMaterial(int original, bool reduce, float multiplier)
        => reduce ? (int) MathF.Ceiling(original * multiplier) : original;

    protected abstract bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component);

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs obj)
    {
        if (!obj.WasModified<LatheRecipePrototype>())
            return;
        BuildInverseRecipeDictionary();
    }

    private void BuildInverseRecipeDictionary()
    {
        _inverseRecipeDictionary.Clear();
        foreach (var latheRecipe in _proto.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (latheRecipe.Result == null)
                continue;

            _inverseRecipeDictionary.GetOrNew(latheRecipe.Result).Add(latheRecipe);
        }
    }

    public bool TryGetRecipesFromEntity(string prototype, [NotNullWhen(true)] out List<LatheRecipePrototype>? recipes)
    {
        recipes = new();
        if (_inverseRecipeDictionary.TryGetValue(prototype, out var r))
            recipes.AddRange(r);
        return recipes.Count != 0;
    }

    public string GetRecipeName(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeName(_proto.Index(proto));
    }

    public string GetRecipeName(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Name))
            return Loc.GetString(proto.Name);

        if (proto.Result is { } result)
        {
            // #Misfits Fix - Some blueprint recipes may still reference missing or abstract-only
            // result prototypes. Fall back to the raw prototype ID instead of crashing the UI.
            if (_proto.TryIndex<EntityPrototype>(result, out var resultProto))
                return resultProto.Name;

            return result;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            return ContentLocalizationManager.FormatList(resultReagents
                .Select(p => Loc.GetString("lathe-menu-result-reagent-display", ("reagent", _proto.Index(p.Key).LocalizedName), ("amount", p.Value)))
                .ToList());
        }

        return string.Empty;
    }

    [PublicAPI]
    public string GetRecipeDescription(ProtoId<LatheRecipePrototype> proto)
    {
        return GetRecipeDescription(_proto.Index(proto));
    }

    public string GetRecipeDescription(LatheRecipePrototype proto)
    {
        if (!string.IsNullOrWhiteSpace(proto.Description))
            return Loc.GetString(proto.Description);

        if (proto.Result is { } result)
        {
            // #Misfits Fix - Match GetRecipeName's defensive lookup so invalid blueprint results
            // don't crash description generation during UI refresh.
            if (_proto.TryIndex<EntityPrototype>(result, out var resultProto))
                return resultProto.Description;

            return string.Empty;
        }

        if (proto.ResultReagents is { } resultReagents)
        {
            // We only use the first one for the description since these descriptions don't combine very well.
            var reagent = resultReagents.First().Key;
            return _proto.Index(reagent).LocalizedDescription;
        }

        return string.Empty;
    }
}
