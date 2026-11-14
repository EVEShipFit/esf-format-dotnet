using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EveShipFit.EsfFormat;

namespace Example;

/// <summary>A Catalog read from the official SDE in JSONL, unpacked into a directory.</summary>
public sealed class SdeJsonlCatalog : ICatalog
{
    private sealed record Named(int Id, string Name, bool Published);

    private sealed record Dogma(List<int> Effects, int? SquadronSize, Dictionary<int, double> Rollable);

    private sealed record Mutaplasmid(int Id, HashSet<int> Bases, List<int> Attributes);

    private readonly Dictionary<int, TypeRecord> types = [];
    private readonly Dictionary<int, AttributeRecord> attributes = [];
    private readonly IReadOnlyDictionary<string, int> typeNames;
    private readonly IReadOnlyDictionary<string, int> attributeNames;
    private readonly Dictionary<int, List<TypeRecord>> modes = [];
    private readonly Dictionary<int, List<MutaplasmidRecord>> mutaplasmids = [];

    public SdeJsonlCatalog(string dir)
    {
        IEnumerable<JsonElement> Rows(string name) =>
            File.ReadLines(Path.Combine(dir, name)).Select(line => JsonSerializer.Deserialize<JsonElement>(line));

        var categoryOfGroup = Rows("groups.jsonl").ToDictionary(Key, g => g.GetProperty("categoryID").GetInt32());

        var effectCategory = Rows("dogmaEffects.jsonl").ToDictionary(
            Key,
            e => e.TryGetProperty("effectCategoryID", out var category) ? category.GetInt32() : 0);

        var mutaplasmidRows = Rows("dynamicItemAttributes.jsonl")
            .Select(m => new Mutaplasmid(
                Key(m),
                m.GetProperty("inputOutputMapping").EnumerateArray()
                    .SelectMany(io => io.GetProperty("applicableTypes").EnumerateArray())
                    .Select(t => t.GetInt32())
                    .ToHashSet(),
                m.GetProperty("attributeIDs").EnumerateArray().Select(Key).ToList()))
            .ToList();

        var rollable = mutaplasmidRows.SelectMany(m => m.Attributes).ToHashSet();
        var dogma = new Dictionary<int, Dogma>();
        foreach (var row in Rows("typeDogma.jsonl"))
        {
            var effects = Array(row, "dogmaEffects").Select(e => e.GetProperty("effectID").GetInt32()).ToList();
            var values = new Dictionary<int, double>();
            foreach (var a in Array(row, "dogmaAttributes"))
            {
                values[a.GetProperty("attributeID").GetInt32()] = a.GetProperty("value").GetDouble();
            }
            dogma[Key(row)] = new Dogma(
                effects,
                Sde.SquadronSize(values),
                values.Where(v => rollable.Contains(v.Key)).ToDictionary());
        }

        var typeRows = new List<Named>();
        var ships = new List<Named>();
        foreach (var t in Rows("types.jsonl"))
        {
            if (!t.TryGetProperty("name", out var names) || !names.TryGetProperty("en", out var en))
            {
                continue;
            }
            var id = Key(t);
            var name = en.GetString()!;
            var groupId = t.GetProperty("groupID").GetInt32();
            var categoryId = categoryOfGroup[groupId];
            var d = dogma.GetValueOrDefault(id);
            var effects = d?.Effects ?? [];
            types[id] = new TypeRecord(
                id,
                name,
                Sde.Kind(categoryId, groupId, effects),
                Sde.Active(effects.Distinct().ToDictionary(e => e, e => effectCategory.GetValueOrDefault(e))),
                Double(t, "capacity"),
                Double(t, "volume"),
                d?.SquadronSize);
            var named = new Named(id, name, Published(t));
            typeRows.Add(named);
            if (categoryId == 6)
            {
                ships.Add(named);
            }
        }
        typeNames = Sde.NameIndex(typeRows, n => n.Id, n => n.Name, n => n.Published);

        var attributeRows = new List<Named>();
        var defaults = new Dictionary<int, double>();
        foreach (var a in Rows("dogmaAttributes.jsonl"))
        {
            var id = Key(a);
            var name = a.GetProperty("name").GetString()!;
            attributes[id] = new AttributeRecord(id, name);
            attributeRows.Add(new Named(id, name, Published(a)));
            defaults[id] = Double(a, "defaultValue");
        }
        attributeNames = Sde.NameIndex(attributeRows, n => n.Id, n => n.Name, n => n.Published);

        var shipNames = Sde.NameIndex(ships, n => n.Id, n => n.Name, n => n.Published);
        foreach (var mode in types.Values.Where(t => t.Kind == Kind.Mode))
        {
            if (Sde.ModeHull(mode.Name, shipNames) is { } hull)
            {
                Add(modes, hull, mode);
            }
        }

        foreach (var m in mutaplasmidRows.OrderBy(m => m.Id))
        {
            foreach (var baseId in m.Bases)
            {
                var values = dogma.GetValueOrDefault(baseId)?.Rollable ?? [];
                var rolls = m.Attributes
                    .Select(a => (a, values.TryGetValue(a, out var v) ? v : defaults.GetValueOrDefault(a)))
                    .ToList();
                Add(mutaplasmids, baseId, new MutaplasmidRecord(types[m.Id], rolls));
            }
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

    private static int Key(JsonElement row) => row.GetProperty("_key").GetInt32();

    private static bool Published(JsonElement row) => row.TryGetProperty("published", out var p) && p.GetBoolean();

    private static double Double(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) ? value.GetDouble() : 0;

    private static List<JsonElement> Array(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) ? [.. value.EnumerateArray()] : [];

    private static void Add<T>(Dictionary<int, List<T>> map, int key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }
        list.Add(value);
    }
}
