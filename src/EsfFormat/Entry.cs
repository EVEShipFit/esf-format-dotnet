using System.Collections.ObjectModel;

namespace EveShipFit.EsfFormat;

/// <summary>One line of a fit; a <see langword="null"/> type is an empty slot.</summary>
public sealed record Entry(int? Type)
{
    public uint? Count { get; init; }

    public string? FitName { get; init; }

    public int? Charge { get; init; }

    public uint? ChargeCount { get; init; }

    public int? Mutaplasmid { get; init; }

    public IReadOnlyDictionary<int, double> Overrides { get; init; } = ReadOnlyDictionary<int, double>.Empty;

    public State? State { get; init; }

    public Location? Location { get; init; }

    public bool Equals(Entry? other) =>
        other is not null
        && Type == other.Type
        && Count == other.Count
        && FitName == other.FitName
        && Charge == other.Charge
        && ChargeCount == other.ChargeCount
        && Mutaplasmid == other.Mutaplasmid
        && State == other.State
        && Location == other.Location
        && Overrides.Count == other.Overrides.Count
        && Overrides.All(o => other.Overrides.TryGetValue(o.Key, out var value) && value.Equals(o.Value));

    public override int GetHashCode() =>
        HashCode.Combine(Type, Count, FitName, Charge, ChargeCount, Mutaplasmid, State, Location);
}
