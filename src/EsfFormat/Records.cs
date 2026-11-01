namespace EveShipFit.EsfFormat;

/// <summary>A type in the Catalog.</summary>
public sealed record TypeRecord(
    int Id,
    string Name,
    Kind Kind,
    bool Active,
    double Capacity,
    double Volume,
    int? SquadronSize = null);

/// <summary>An attribute in the Catalog.</summary>
public sealed record AttributeRecord(int Id, string Name);

/// <summary>A mutaplasmid for one base type, with the base type's value of each attribute it rolls.</summary>
public sealed record MutaplasmidRecord(TypeRecord Type, IReadOnlyList<(int AttributeId, double BaseValue)> Rolls);
