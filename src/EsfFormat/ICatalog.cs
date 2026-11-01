namespace EveShipFit.EsfFormat;

/// <summary>Types and attributes, by folded name (§6.10) or by ID.</summary>
public interface ICatalog
{
    TypeRecord? TypeByName(string folded);

    TypeRecord? TypeById(int typeId);

    AttributeRecord? AttributeByName(string folded);

    AttributeRecord? AttributeById(int attributeId);

    /// <summary>The tactical modes of a hull.</summary>
    IReadOnlyList<TypeRecord> Modes(int hullId) => [];

    /// <summary>The mutaplasmids that apply to a type.</summary>
    IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => [];
}
