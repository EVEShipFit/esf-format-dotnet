namespace EveShipFit.EsfFormat;

/// <summary>
/// Where the library looks up game data. You write this, on top of your copy of the SDE.
/// </summary>
/// <remarks>
/// <para>
/// The library holds no game data itself. Whenever it meets a name or an ID in a fit,
/// it asks the catalog what that is. <see cref="Sde"/> has helpers for the parts that take more
/// than a lookup.
/// </para>
/// <para>
/// The § numbers point to sections of the
/// <see href="https://github.com/EVEShipFit/esf-format/blob/main/SPEC.md">esf/1 spec</see>.
/// </para>
/// </remarks>
public interface ICatalog
{
    /// <summary>Finds a type by its name.</summary>
    /// <remarks>
    /// <para>
    /// The name comes from a fit, so a user may have typed it in any case.
    /// The library already case-folded it with <see cref="Esf.Fold"/>, so "Rifter", "RIFTER"
    /// and "rifter" all reach you as "rifter". Case folding is like lowercase, but gives the
    /// same result in every language, so use <see cref="Esf.Fold"/>, not <c>ToLowerInvariant()</c>.
    /// Store your type names the same way, by calling <see cref="Esf.Fold"/> on each one,
    /// and look this name up in that store.
    /// </para>
    /// <para>
    /// Include unpublished types too. Some names belong to more than one type.
    /// Then return the published one; if that is still more than one, the lowest ID.
    /// <see cref="Sde.NameIndex"/> builds this store for you.
    /// </para>
    /// <para>Return <see langword="null"/> if no type has this name.</para>
    /// </remarks>
    TypeRecord? TypeByName(string foldedName);

    /// <summary>Finds a type by its ID. Return <see langword="null"/> if there is no type with this ID.</summary>
    TypeRecord? TypeById(int typeId);

    /// <summary>Finds an attribute by its name, like "speedfactor".</summary>
    /// <remarks>
    /// <para>
    /// This works the same way as <see cref="TypeByName"/>: the name is already case-folded with
    /// <see cref="Esf.Fold"/>, unpublished attributes count too, and where names are shared, the
    /// published one wins, then the lowest ID. <see cref="Sde.NameIndex"/> builds this store for you.
    /// </para>
    /// <para>Return <see langword="null"/> if no attribute has this name.</para>
    /// </remarks>
    AttributeRecord? AttributeByName(string foldedName);

    /// <summary>Finds an attribute by its ID. Return <see langword="null"/> if there is no attribute with this ID.</summary>
    AttributeRecord? AttributeById(int attributeId);

    /// <summary>The tactical modes a hull can be in, like the Hecate's Defense, Propulsion and Sharpshooter Mode.</summary>
    /// <remarks>
    /// <para>
    /// Return an empty list for a hull without tactical modes. In the SDE, modes are the types
    /// of kind <see cref="Kind.Mode"/>; <see cref="Sde.ModeHull"/> tells you which hull each one belongs to.
    /// </para>
    /// <para>Leave this out and no hull has tactical modes.</para>
    /// </remarks>
    IReadOnlyList<TypeRecord> Modes(int hullId) => [];

    /// <summary>
    /// The mutaplasmids that can be applied to a type. See <see cref="MutaplasmidRecord"/> for where they come from.
    /// </summary>
    /// <remarks>
    /// <para>Return an empty list for a type no mutaplasmid applies to.</para>
    /// <para>Leave this out and no type has mutaplasmids.</para>
    /// </remarks>
    IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => [];
}
