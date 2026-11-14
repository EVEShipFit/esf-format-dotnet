using System.Text.Json;

namespace EveShipFit.EsfFormat.Tests;

/// <summary>A Catalog read from the esf-format test catalog.</summary>
public sealed class JsonCatalog : ICatalog
{
    private readonly Dictionary<int, TypeRecord> types = [];
    private readonly Dictionary<int, AttributeRecord> attributes = [];
    private readonly IReadOnlyDictionary<string, int> typeNames;
    private readonly IReadOnlyDictionary<string, int> attributeNames;
    private readonly Dictionary<int, List<TypeRecord>> modes = [];
    private readonly Dictionary<int, List<MutaplasmidRecord>> mutaplasmids = [];

    public JsonCatalog(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        foreach (var t in root.GetProperty("types").EnumerateArray())
        {
            var id = t.GetProperty("id").GetInt32();
            types[id] = new TypeRecord(
                id,
                t.GetProperty("name").GetString()!,
                Enum.Parse<Kind>(t.GetProperty("kind").GetString()!, ignoreCase: true),
                t.GetProperty("active").GetBoolean(),
                t.GetProperty("capacity").GetDouble(),
                t.GetProperty("volume").GetDouble(),
                t.TryGetProperty("squadron_size", out var size) ? size.GetInt32() : null);
        }
        foreach (var a in root.GetProperty("attributes").EnumerateArray())
        {
            var id = a.GetProperty("id").GetInt32();
            attributes[id] = new AttributeRecord(id, a.GetProperty("name").GetString()!);
        }
        typeNames = NameIndex(root.GetProperty("types"));
        attributeNames = NameIndex(root.GetProperty("attributes"));
        foreach (var m in root.GetProperty("modes").EnumerateArray())
        {
            modes[m.GetProperty("hull").GetInt32()] =
                m.GetProperty("modes").EnumerateArray().Select(id => types[id.GetInt32()]).ToList();
        }
        foreach (var m in root.GetProperty("mutaplasmids").EnumerateArray())
        {
            var rolls = m.GetProperty("rolls").EnumerateArray()
                .Select(r => (r[0].GetInt32(), r[1].GetDouble()))
                .ToList();
            var record = new MutaplasmidRecord(types[m.GetProperty("mutaplasmid").GetInt32()], rolls);
            var baseId = m.GetProperty("base").GetInt32();
            if (!mutaplasmids.TryGetValue(baseId, out var list))
            {
                mutaplasmids[baseId] = list = [];
            }
            list.Add(record);
        }
    }

    public TypeRecord? TypeByName(string foldedName) =>
        typeNames.TryGetValue(foldedName, out var id) ? types[id] : null;

    public TypeRecord? TypeById(int typeId) => types.GetValueOrDefault(typeId);

    public AttributeRecord? AttributeByName(string foldedName) =>
        attributeNames.TryGetValue(foldedName, out var id) ? attributes[id] : null;

    public AttributeRecord? AttributeById(int attributeId) => attributes.GetValueOrDefault(attributeId);

    public IReadOnlyList<TypeRecord> Modes(int hullId) => modes.GetValueOrDefault(hullId) ?? [];

    public IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => mutaplasmids.GetValueOrDefault(typeId) ?? [];

    private static IReadOnlyDictionary<string, int> NameIndex(JsonElement entries) =>
        Sde.NameIndex(
            entries.EnumerateArray(),
            e => e.GetProperty("id").GetInt32(),
            e => e.GetProperty("name").GetString()!,
            e => e.GetProperty("published").GetBoolean());
}
