using System.Globalization;
using System.Text;

namespace EveShipFit.EsfFormat.Internal;

internal sealed class CanonLine(TypeRecord? type)
{
    private string? body;

    public TypeRecord? Type { get; } = type;

    public long? Count { get; set; }

    public string? FitName { get; init; }

    public TypeRecord? Charge { get; init; }

    public uint? ChargeCount { get; init; }

    public MutaplasmidRecord? Mutaplasmid { get; init; }

    public string? MutaplasmidName { get; init; }

    public List<(AttributeRecord Attribute, double Value)> Overrides { get; init; } = [];

    public State? State { get; init; }

    public Location? Location { get; init; }

    /// <summary>The line without its count.</summary>
    public string Body => body ??= WriteBody(new StringBuilder()).ToString();

    public string Text => Count is null ? Body : $"{Count}x {Body}";

    private StringBuilder WriteBody(StringBuilder builder)
    {
        builder.Append(Type is null ? "-" : Canonical.QuoteType(Type.Name));
        if (FitName is not null)
        {
            builder.Append(' ').Append(Canonical.Quote(FitName));
        }
        if (Charge is not null)
        {
            builder.Append(" :");
            if (ChargeCount is not null)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{ChargeCount}x ");
            }
            builder.Append(Canonical.QuoteType(Charge.Name));
        }
        if (MutaplasmidName is not null)
        {
            builder.Append(" +").Append(Canonical.QuoteType(MutaplasmidName));
        }
        if (Overrides.Count > 0)
        {
            builder.Append(" {");
            builder.AppendJoin(", ", Overrides.Select(o => $"{o.Attribute.Name} {Number.Format(o.Value)}"));
            builder.Append('}');
        }
        if (State is not null)
        {
            builder.Append(" !").Append(State.Value.Name());
        }
        if (Location is not null)
        {
            builder.Append(" @").Append(Location.Value.Name());
        }
        return builder;
    }
}

internal sealed class CanonFit(TypeRecord? hull, string? name, TypeRecord? mode, string? modeText, List<List<CanonLine>> groups)
{
    public TypeRecord? Hull { get; } = hull;

    public string? Name { get; } = name;

    public TypeRecord? Mode { get; } = mode;

    public IEnumerable<CanonLine> Lines => groups.SelectMany(group => group);

    public void Write(StringBuilder builder)
    {
        builder.Append("%esf/1\n").Append(Hull is null ? "-" : Canonical.QuoteType(Hull.Name));
        if (Name is not null)
        {
            builder.Append(' ').Append(Canonical.Quote(Name));
        }
        if (modeText is not null)
        {
            builder.Append(" /").Append(Canonical.QuoteType(modeText));
        }
        builder.Append('\n');
        foreach (var group in groups)
        {
            builder.Append('\n');
            foreach (var line in group)
            {
                builder.Append(line.Text).Append('\n');
            }
        }
    }
}

/// <summary>Canonical form (SPEC §8).</summary>
internal static class Canonical
{
    public static string Quote(string s) => $"\"{s.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public static bool NeedsQuotes(string name)
    {
        var words = name.Split(' ');
        return words[0] is "" or ['-' or '%', ..]
            || Text.IsCount(words[0])
            || Array.Exists(words, w => w.Length == 0 || Text.IsSigil(w[0]));
    }

    public static string QuoteType(string name) => NeedsQuotes(name) ? Quote(name) : name;

    public static CanonFit Fit(Fit fit, ICatalog catalog)
    {
        var groups = new Dictionary<Place, List<Item>>();
        foreach (var part in fit.Parts)
        {
            if (part is not Item item || item.Place.IsRack())
            {
                continue;
            }
            var group = item.Place == Place.Bay ? Places.Default(item.Type.Kind)!.Value : item.Place;
            if (!groups.TryGetValue(group, out var list))
            {
                groups[group] = list = [];
            }
            list.Add(item);
        }

        var chunks = new List<List<CanonLine>>();
        foreach (var group in Places.Groups)
        {
            var chunk = group.IsRack() ? Rack(fit, group, catalog) : Group(fit, group, catalog, groups);
            if (chunk.Count > 0)
            {
                chunks.Add(chunk);
            }
        }

        string? modeText = null;
        if (fit.Hull is not null && fit.Mode is not null)
        {
            var others = catalog.Modes(fit.Hull.Id).Select(m => Resolver.ModeName(fit.Hull, m)).ToList();
            var own = Resolver.ModeName(fit.Hull, fit.Mode);
            others.Remove(own);
            modeText = Names.ShortestUnique(own, others);
        }

        return new CanonFit(fit.Hull, fit.Name, fit.Mode, modeText, chunks);
    }

    public static string Render(List<CanonFit> fits)
    {
        var builder = new StringBuilder();
        foreach (var fit in fits)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }
            fit.Write(builder);
        }
        return builder.ToString();
    }

    public static EsfFit ToModel(CanonFit fit) => new()
    {
        Hull = fit.Hull?.Id,
        Name = fit.Name,
        Mode = fit.Mode?.Id,
        Entries = fit.Lines.Select(line => new Entry(line.Type?.Id)
        {
            Count = (uint?)line.Count,
            FitName = line.FitName,
            Charge = line.Charge?.Id,
            ChargeCount = line.ChargeCount,
            Mutaplasmid = line.Mutaplasmid?.Type.Id,
            Overrides = line.Overrides.ToDictionary(o => o.Attribute.Id, o => o.Value),
            State = line.State,
            Location = line.Location,
        }).ToList(),
    };

    /// <summary>The state an item has when its line names none; null is active.</summary>
    private static State? DefaultState(Item item) =>
        item.Place.IsPlugged() || !item.Type.Active ? State.On : null;

    private static CanonLine Line(Item item, Fit fit, ICatalog catalog)
    {
        var line = item.Line;

        uint? chargeCount = null;
        if (item.Charge is not null && line.ChargeCount is { } count && count != Number.FullLoad(item.Type, item.Charge))
        {
            chargeCount = count;
        }

        var overrides = new Dictionary<int, (AttributeRecord Attribute, double Value)>();
        foreach (var (attribute, value) in item.Overrides)
        {
            overrides[attribute.Id] = (attribute, value == 0 ? 0 : value);
        }
        string? mutaplasmidName = null;
        if (item.Mutaplasmid is not null)
        {
            var own = item.Mutaplasmid.Type.Name;
            var others = catalog.Mutaplasmids(item.Type.Id).Select(m => m.Type.Name).ToList();
            others.Remove(own);
            mutaplasmidName = Names.ShortestUnique(own, others);
            foreach (var (attributeId, baseValue) in item.Mutaplasmid.Rolls)
            {
                if (!overrides.ContainsKey(attributeId))
                {
                    var attribute = catalog.AttributeById(attributeId)
                        ?? throw new KeyNotFoundException($"the catalog has no attribute {attributeId}");
                    overrides[attributeId] = (attribute, baseValue);
                }
            }
        }

        var place = item.Place;
        return new CanonLine(item.Type)
        {
            FitName = item.Reference?.Name,
            Charge = item.Charge,
            ChargeCount = chargeCount,
            Mutaplasmid = item.Mutaplasmid,
            MutaplasmidName = mutaplasmidName,
            Overrides = overrides.Values
                .OrderBy(o => Names.Fold(o.Attribute.Name), Names.CodePoints)
                .ThenBy(o => o.Attribute.Name, Names.CodePoints)
                .ToList(),
            State = line.State is { } state && state != DefaultState(item) ? state : null,
            Location = place != Places.Default(item.Type.Kind, fit.CargoOnly) && !place.IsRack()
                ? place.ToLocation()
                : null,
        };
    }

    /// <summary>Merge adjacent identical repeatable lines into one count.</summary>
    private static List<CanonLine> Collapse(IEnumerable<(bool Repeatable, CanonLine Line)> lines)
    {
        var merged = new List<(bool Repeatable, CanonLine Line)>();
        foreach (var (repeatable, line) in lines)
        {
            if (merged.Count > 0 && repeatable && merged[^1].Repeatable && merged[^1].Line.Body == line.Body)
            {
                var previous = merged[^1].Line;
                previous.Count = (previous.Count ?? 1) + (line.Count ?? 1);
                if (previous.Count > uint.MaxValue)
                {
                    throw new EsfException($"lines collapse into a count of {previous.Count}, which is too large");
                }
                continue;
            }
            merged.Add((repeatable, line));
        }
        return merged.ConvertAll(o =>
        {
            if (o.Line.Count == 1)
            {
                o.Line.Count = null;
            }
            return o.Line;
        });
    }

    private static List<CanonLine> Group(Fit fit, Place group, ICatalog catalog, Dictionary<Place, List<Item>> groups)
    {
        if (!groups.TryGetValue(group, out var items))
        {
            return [];
        }
        return group switch
        {
            Place.Drones => [.. Sorted(InPlace(items, Place.Drones), fit, catalog), .. Bay(items, fit, catalog)],
            Place.Fighters => [.. Fighters(items, fit, catalog), .. Bay(items, fit, catalog)],
            _ => Sorted(items, fit, catalog),
        };
    }

    private static List<CanonLine> Rack(Fit fit, Place rack, ICatalog catalog)
    {
        if (!fit.Racks.TryGetValue(rack, out var runs))
        {
            return [];
        }
        var used = runs.FindLastIndex(run => run.Item is not null) + 1;
        return Collapse(runs.Take(used).Select(run =>
        {
            var line = run.Item is null
                ? new CanonLine(null) { Location = rack.ToLocation() }
                : Line(run.Item, fit, catalog);
            line.Count = run.Length;
            return (true, line);
        }));
    }

    private static List<CanonLine> Sorted(IEnumerable<Item> items, Fit fit, ICatalog catalog)
    {
        var lines = items.Select(item =>
        {
            var line = Line(item, fit, catalog);
            var repeatable = !item.Stored;
            var count = item.Line.Count ?? 1;
            line.Count = repeatable || count != 1 ? count : null;
            return (Key: Names.Fold(item.Type.Name), Text: repeatable ? line.Body : line.Text, Repeatable: repeatable, Line: line);
        });
        return Collapse(lines
            .OrderBy(l => l.Key, Names.CodePoints)
            .ThenBy(l => l.Text, Names.CodePoints)
            .Select(l => (l.Repeatable, l.Line)));
    }

    private static IEnumerable<Item> InPlace(List<Item> items, Place place) => items.Where(i => i.Place == place);

    private static List<CanonLine> Bay(List<Item> items, Fit fit, ICatalog catalog) =>
        Sorted(InPlace(items, Place.Bay), fit, catalog);

    private static IEnumerable<CanonLine> Fighters(List<Item> items, Fit fit, ICatalog catalog) =>
        InPlace(items, Place.Fighters).Select(item =>
        {
            var line = Line(item, fit, catalog);
            if (item.Line.Count is { } count && count != item.Type.SquadronSize)
            {
                line.Count = count;
            }
            return line;
        });
}
