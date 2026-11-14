namespace EveShipFit.EsfFormat;

/// <summary>Helpers to build an <see cref="ICatalog"/> from the SDE, for the fields that take more than a lookup.</summary>
/// <remarks>Each helper takes plain values from the SDE files, so it works however you load them.</remarks>
public static class Sde
{
    private const int CategoryShip = 6;
    private const int CategoryModule = 7;
    private const int CategoryCharge = 8;
    private const int CategoryDrone = 18;
    private const int CategoryImplant = 20;
    private const int CategorySubsystem = 32;
    private const int CategoryStructure = 65;
    private const int CategoryStructureModule = 66;
    private const int CategoryFighter = 87;

    private const int GroupBooster = 303;
    private const int GroupShipModifiers = 1306;
    private const int GroupCargoContainer = 12;
    private const int GroupSecureCargoContainer = 340;
    private const int GroupAuditLogSecureContainer = 448;
    private const int GroupFreightContainer = 649;

    private const int EffectOnline = 16;
    private const int EffectCategoryActive = 1;
    private const int EffectCategoryTarget = 2;
    private const int EffectLoPower = 11;
    private const int EffectHiPower = 12;
    private const int EffectMedPower = 13;
    private const int EffectRigSlot = 2663;
    private const int EffectServiceSlot = 6306;

    private const int AttributeSquadronSize = 2215;

    /// <summary>Works out what a type is. See <see cref="EsfFormat.Kind"/> for each rule.</summary>
    /// <param name="categoryId">The <c>categoryID</c> of the type's group, in <c>groups.jsonl</c>.</param>
    /// <param name="groupId">The type's <c>groupID</c>, in <c>types.jsonl</c>.</param>
    /// <param name="effectIds">The <c>effectID</c>s in the type's <c>dogmaEffects</c>, in <c>typeDogma.jsonl</c>.</param>
    public static Kind Kind(int categoryId, int groupId, IEnumerable<int> effectIds)
    {
        ArgumentNullException.ThrowIfNull(effectIds);
        return groupId switch
        {
            GroupShipModifiers => EsfFormat.Kind.Mode,
            GroupCargoContainer or GroupSecureCargoContainer or GroupAuditLogSecureContainer or GroupFreightContainer =>
                EsfFormat.Kind.Container,
            GroupBooster => EsfFormat.Kind.Booster,
            _ => categoryId switch
            {
                CategoryShip or CategoryStructure => EsfFormat.Kind.Hull,
                CategoryModule or CategoryStructureModule => SlotKind(effectIds),
                CategorySubsystem => EsfFormat.Kind.Sub,
                CategoryDrone => EsfFormat.Kind.Drone,
                CategoryFighter => EsfFormat.Kind.Fighter,
                CategoryCharge => EsfFormat.Kind.Charge,
                CategoryImplant => EsfFormat.Kind.Implant,
                _ => EsfFormat.Kind.Other,
            },
        };
    }

    /// <summary>
    /// Works out whether a type runs when it is turned on (§6.4), for <see cref="TypeRecord.Active"/>.
    /// </summary>
    /// <remarks>
    /// A type runs when one of its effects is in category 1 (active) or 2 (target).
    /// The SDE puts the <c>online</c> effect in category 1 too, but going online is not
    /// running, so that one does not count.
    /// </remarks>
    /// <param name="effects">
    /// Each <c>effectID</c> in the type's <c>dogmaEffects</c> (in <c>typeDogma.jsonl</c>), mapped
    /// to that effect's <c>effectCategoryID</c> (in <c>dogmaEffects.jsonl</c>).
    /// </param>
    public static bool Active(IReadOnlyDictionary<int, int> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return effects.Any(e =>
            e.Key != EffectOnline && e.Value is EffectCategoryActive or EffectCategoryTarget);
    }

    /// <summary>
    /// Reads how many fighters make up a full squadron, for <see cref="TypeRecord.SquadronSize"/>.
    /// </summary>
    /// <param name="attributes">
    /// Each <c>attributeID</c> in the type's <c>dogmaAttributes</c> (in <c>typeDogma.jsonl</c>), mapped to its
    /// <c>value</c>.
    /// </param>
    /// <returns>
    /// The squadron size, or <see langword="null"/> when the type has no <c>fighterSquadronMaxSize</c> (2215),
    /// which only fighters have.
    /// </returns>
    public static int? SquadronSize(IReadOnlyDictionary<int, double> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        return attributes.TryGetValue(AttributeSquadronSize, out var size) ? (int)size : null;
    }

    /// <summary>Finds which hull a tactical mode belongs to, for <see cref="ICatalog.Modes"/>.</summary>
    /// <remarks>
    /// The SDE does not link a mode to its hull. Instead, a mode's name starts with
    /// its hull's name: "Hecate Sharpshooter Mode" belongs to the Hecate. This tries
    /// the longest start of <paramref name="modeName"/> first, a whole word at a time.
    /// </remarks>
    /// <param name="modeName">The mode's English name, like "Hecate Sharpshooter Mode".</param>
    /// <param name="shipNames">A <see cref="NameIndex"/> of every type in category <c>Ship</c> (6).</param>
    /// <returns>The hull's type ID, or <see langword="null"/> when no ship name matches.</returns>
    public static int? ModeHull(string modeName, IReadOnlyDictionary<string, int> shipNames)
    {
        ArgumentNullException.ThrowIfNull(modeName);
        ArgumentNullException.ThrowIfNull(shipNames);
        var words = modeName.Split(' ');
        for (var n = words.Length - 1; n >= 1; n--)
        {
            if (shipNames.TryGetValue(Esf.Fold(string.Join(' ', words, 0, n)), out var hull))
            {
                return hull;
            }
        }
        return null;
    }

    /// <summary>
    /// Builds the name store that <see cref="ICatalog.TypeByName"/> and <see cref="ICatalog.AttributeByName"/>
    /// look names up in.
    /// </summary>
    /// <remarks>
    /// Give it every type, or every attribute, including unpublished ones. Each name is
    /// case-folded with <see cref="Esf.Fold"/>, the same way the library does with the names in a fit.
    /// Where names are shared, the published one wins, then the lowest ID (§4.2).
    /// In the SDE, a type or attribute without <c>published</c> counts as not published.
    /// </remarks>
    /// <param name="entries">Every type, or every attribute.</param>
    /// <param name="id">The ID of an entry.</param>
    /// <param name="name">The name of an entry.</param>
    /// <param name="published">Whether an entry is published.</param>
    /// <returns>A dictionary from case-folded name to ID.</returns>
    public static IReadOnlyDictionary<string, int> NameIndex<T>(
        IEnumerable<T> entries,
        Func<T, int> id,
        Func<T, string> name,
        Func<T, bool> published)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(published);
        var index = new Dictionary<string, int>();
        foreach (var entry in entries.OrderBy(e => !published(e)).ThenBy(id))
        {
            index.TryAdd(Esf.Fold(name(entry)), id(entry));
        }
        return index;
    }

    private static Kind SlotKind(IEnumerable<int> effectIds)
    {
        foreach (var effectId in effectIds)
        {
            switch (effectId)
            {
                case EffectHiPower:
                    return EsfFormat.Kind.High;
                case EffectMedPower:
                    return EsfFormat.Kind.Mid;
                case EffectLoPower:
                    return EsfFormat.Kind.Low;
                case EffectRigSlot:
                    return EsfFormat.Kind.Rig;
                case EffectServiceSlot:
                    return EsfFormat.Kind.Svc;
                default:
                    break;
            }
        }
        return EsfFormat.Kind.Other;
    }
}
