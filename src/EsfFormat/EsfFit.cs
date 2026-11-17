namespace EveShipFit.EsfFormat;

/// <summary>One fit: a ship and everything on it, or a plain list of items.</summary>
/// <remarks>
/// A list of fits is one document. A fit that another fit carries, through <see cref="Entry.FitName"/>,
/// exists only there. Any other fit stands on its own.
/// </remarks>
public sealed record EsfFit
{
    /// <summary>The type ID of the ship, structure or container, like 587 for the Rifter.</summary>
    /// <remarks>
    /// Only a type whose <see cref="KindExtensions.CanBeHull"/> is true can be a hull.
    /// <see langword="null"/> for a fit without a ship, like the items in a contract: then every entry is
    /// stored in cargo, and the fit has no slots and no <see cref="Mode"/>.
    /// </remarks>
    public int? Hull { get; init; }

    /// <summary>The name of the fit, like "Shield Buffer". <see langword="null"/> for no name.</summary>
    public string? Name { get; init; }

    /// <summary>The type ID of the tactical mode the ship is in, like the Hecate's Sharpshooter Mode.</summary>
    /// <remarks>
    /// Only for a hull that has tactical modes; see <see cref="ICatalog.Modes"/>. <see langword="null"/> for none.
    /// </remarks>
    public int? Mode { get; init; }

    /// <summary>The lines of the fit: items, empty slots and fits it carries.</summary>
    public IReadOnlyList<Entry> Entries { get; init; } = [];

    public bool Equals(EsfFit? other) =>
        other is not null
        && Hull == other.Hull
        && Name == other.Name
        && Mode == other.Mode
        && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => HashCode.Combine(Hull, Name, Mode, Entries.Count);
}
