namespace EveShipFit.EsfFormat;

/// <summary>One type, like "Rifter" or "200mm AutoCannon II", as the library needs it.</summary>
/// <remarks>
/// With the SDE, most fields come from <c>types.jsonl</c>. <see cref="Kind"/>, <see cref="Active"/>
/// and <see cref="SquadronSize"/> also need <c>groups.jsonl</c>, <c>typeDogma.jsonl</c> and
/// <c>dogmaEffects.jsonl</c>, through the helpers in <see cref="Sde"/>.
/// Include every type in <c>types.jsonl</c>. A type with no row in <c>typeDogma.jsonl</c> has no effects
/// and no dogma attributes: give the helpers an empty collection or dictionary.
/// </remarks>
/// <param name="Id">The type ID, like 587 for the Rifter. In the SDE this is <c>_key</c>.</param>
/// <param name="Name">The English name, like "Rifter". In the SDE this is <c>name.en</c>.</param>
/// <param name="Kind">What it is, which decides where it goes in a fit. Use <see cref="Sde.Kind"/> to work this out.</param>
/// <param name="Active">
/// Whether the type runs when it is turned on, like a gun or a shield booster.
/// A plate or a damage control only goes online, so it is <see langword="false"/>.
/// Use <see cref="Sde.Active"/> to work this out from the type's effects.
/// </param>
/// <param name="Capacity">
/// How much the type holds, in m³; for a module, how much ammunition fits in it.
/// In the SDE this is <c>capacity</c>. Use 0 when the type has none.
/// </param>
/// <param name="Volume">How much room one of it takes, in m³. In the SDE this is <c>volume</c>. Use 0 when the type has none.</param>
/// <param name="SquadronSize">
/// For a fighter, how many make up a full squadron. <see langword="null"/> for anything else.
/// In the SDE this is the dogma attribute <c>fighterSquadronMaxSize</c>; <see cref="Sde.SquadronSize"/> reads it for you.
/// </param>
public sealed record TypeRecord(
    int Id,
    string Name,
    Kind Kind,
    bool Active,
    double Capacity,
    double Volume,
    int? SquadronSize = null);

/// <summary>One dogma attribute, like "speedFactor", as the library needs it.</summary>
/// <remarks>With the SDE, both fields come from <c>dogmaAttributes.jsonl</c>.</remarks>
/// <param name="Id">The attribute ID, like 20 for speedFactor. In the SDE this is <c>_key</c>.</param>
/// <param name="Name">The name, like "speedFactor". In the SDE this is <c>name</c>; it is not translated.</param>
public sealed record AttributeRecord(int Id, string Name);

/// <summary>
/// One mutaplasmid that can be applied to a base type, like "Unstable 5MN Microwarpdrive Mutaplasmid".
/// </summary>
/// <remarks>
/// With the SDE, these come from <c>dynamicItemAttributes.jsonl</c>. Each entry there is one mutaplasmid.
/// Its <c>inputOutputMapping</c> is a list; the base types it applies to are all the <c>applicableTypes</c>
/// across that list.
/// </remarks>
/// <param name="Type">The mutaplasmid's own type: the type whose ID is the entry's <c>_key</c>.</param>
/// <param name="Rolls">
/// The dogma attributes the mutaplasmid changes, each with the base type's value before the change.
/// The attribute IDs are the <c>_key</c> of each item in the entry's <c>attributeIDs</c>; its <c>min</c> and
/// <c>max</c> are not needed. Each value is the base type's value of that attribute in <c>typeDogma.jsonl</c>.
/// When the base type does not have it, use the attribute's <c>defaultValue</c> in
/// <c>dogmaAttributes.jsonl</c>, or 0 when that is missing too.
/// </param>
public sealed record MutaplasmidRecord(TypeRecord Type, IReadOnlyList<(int AttributeId, double BaseValue)> Rolls);
