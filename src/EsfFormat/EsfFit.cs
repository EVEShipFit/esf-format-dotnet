namespace EveShipFit.EsfFormat;

/// <summary>A fit: its hull and its entries.</summary>
public sealed record EsfFit
{
    public int? Hull { get; init; }

    public string? Name { get; init; }

    public int? Mode { get; init; }

    public IReadOnlyList<Entry> Entries { get; init; } = [];

    public bool Equals(EsfFit? other) =>
        other is not null
        && Hull == other.Hull
        && Name == other.Name
        && Mode == other.Mode
        && Entries.SequenceEqual(other.Entries);

    public override int GetHashCode() => HashCode.Combine(Hull, Name, Mode, Entries.Count);
}
