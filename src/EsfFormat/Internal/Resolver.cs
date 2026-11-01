namespace EveShipFit.EsfFormat.Internal;

/// <summary>A line of a fit, resolved: an item or an empty slot.</summary>
internal abstract class Part
{
    public abstract Line Line { get; }

    public abstract Place Place { get; }
}

internal sealed class EmptySlot(EmptyLine line) : Part
{
    public override EmptyLine Line { get; } = line;

    public override Place Place => Line.Rack.ToPlace();
}

internal sealed class Item(ItemLine line, TypeRecord type, Place place) : Part
{
    public override ItemLine Line { get; } = line;

    public override Place Place => place;

    public TypeRecord Type { get; } = type;

    public TypeRecord? Charge { get; set; }

    public MutaplasmidRecord? Mutaplasmid { get; set; }

    public List<(AttributeRecord Attribute, double Value)> Overrides { get; } = [];

    /// <summary>The fit this ship or container carries.</summary>
    public Fit? Reference { get; set; }

    public bool Stored => place.IsStored();

    public void Move(Place to) => place = to;
}

/// <summary>Consecutive slots of a rack holding the same item; no item for empty slots.</summary>
internal readonly record struct Run(long Start, long Length, Item? Item);

internal sealed class Fit(FitBlock block, TypeRecord? hull, TypeRecord? mode, bool cargoOnly)
{
    public FitBlock Block { get; } = block;

    public TypeRecord? Hull { get; } = hull;

    public TypeRecord? Mode { get; } = mode;

    public bool CargoOnly { get; } = cargoOnly;

    public string? Name => Block.FitName;

    public List<Part> Parts { get; } = [];

    public Dictionary<Place, List<Run>> Racks { get; } = [];
}

/// <summary>Resolving names against the Catalog, and the semantic rules (SPEC §4.2, §5, §6).</summary>
internal static class Resolver
{
    public static List<Fit> Resolve(List<FitBlock> blocks, ICatalog catalog)
    {
        var fits = blocks.ConvertAll(block => ResolveHull(block, catalog));
        var named = new Dictionary<(int Hull, string Name), List<Fit>>();
        foreach (var fit in fits)
        {
            if (fit.Hull is not null && fit.Name is not null)
            {
                var key = (fit.Hull.Id, Names.Fold(fit.Name));
                if (!named.TryGetValue(key, out var list))
                {
                    named[key] = list = [];
                }
                list.Add(fit);
            }
        }

        foreach (var fit in fits)
        {
            var plugged = new HashSet<int>();
            foreach (var line in fit.Block.Lines)
            {
                var part = ResolveLine(line, fit, named, catalog);
                if (part is Item { Place: var place } item && place.IsPlugged() && !plugged.Add(item.Type.Id))
                {
                    throw new EsfException($"'{item.Type.Name}' is plugged in twice", line.Number);
                }
                fit.Parts.Add(part);
            }
        }

        CheckCycles(fits);

        foreach (var fit in fits)
        {
            foreach (var rack in fit.Parts.Where(p => p.Place.IsRack()).GroupBy(p => p.Place))
            {
                fit.Racks[rack.Key] = Layout(rack.ToList());
            }
        }
        return fits;
    }

    /// <summary>A tactical mode's name without the hull's name.</summary>
    public static string ModeName(TypeRecord hull, TypeRecord mode) =>
        mode.Name.AsSpan(Math.Min(hull.Name.Length, mode.Name.Length)).TrimStart(' ').ToString();

    private static TypeRecord LookUp(ICatalog catalog, string name, int line) =>
        catalog.TypeByName(Names.Fold(name)) ?? throw new EsfException($"unknown type '{name}'", line);

    private static T Pick<T>(List<T> candidates, string given, string what, int line, Func<T, string> name) =>
        candidates.Count switch
        {
            0 => throw new EsfException($"no {what} matches '{given}'", line),
            1 => candidates[0],
            _ => throw new EsfException(
                $"'{given}' matches more than one {what}: {string.Join(", ", candidates.Select(c => $"'{name(c)}'"))}",
                line),
        };

    private static Fit ResolveHull(FitBlock block, ICatalog catalog)
    {
        if (block.Hull is null)
        {
            return new Fit(block, null, null, true);
        }

        var hull = LookUp(catalog, block.Hull, block.HullLine);
        if (hull.Kind is not (Kind.Hull or Kind.Container))
        {
            throw new EsfException($"'{hull.Name}' is not a ship, structure or container", block.HullLine);
        }

        TypeRecord? mode = null;
        if (block.Mode is not null)
        {
            var modes = catalog.Modes(hull.Id);
            if (modes.Count == 0)
            {
                throw new EsfException($"'{hull.Name}' has no tactical modes", block.HullLine);
            }
            var matches = modes
                .Where(m => Names.IsWordPrefix(block.Mode, m.Name) || Names.IsWordPrefix(block.Mode, ModeName(hull, m)))
                .ToList();
            mode = Pick(matches, block.Mode, $"tactical mode of '{hull.Name}'", block.HullLine, m => m.Name);
        }

        return new Fit(block, hull, mode, hull.Kind == Kind.Container);
    }

    private static Part ResolveLine(
        Line text, Fit fit, Dictionary<(int, string), List<Fit>> named, ICatalog catalog)
    {
        var n = text.Number;
        if (text is EmptyLine empty)
        {
            if (fit.CargoOnly)
            {
                throw new EsfException("a fit without a ship has no slots to keep empty", n);
            }
            return new EmptySlot(empty);
        }

        var line = (ItemLine)text;
        var type = LookUp(catalog, line.Name, n);
        var kind = type.Kind;
        if (kind == Kind.Mode)
        {
            throw new EsfException("a tactical mode is only written on the hull line", n);
        }

        var item = new Item(line, type, Places.Default(kind, fit.CargoOnly));

        if (line.FitName is not null)
        {
            if (kind is not (Kind.Hull or Kind.Container))
            {
                throw new EsfException($"'{type.Name}' cannot take a fit name", n);
            }
            var matches = named.GetValueOrDefault((type.Id, Names.Fold(line.FitName)));
            if (matches is not { Count: 1 })
            {
                var what = matches is null ? "no fit" : "more than one fit";
                throw new EsfException($"{what} matches {type.Name} '{line.FitName}'", n);
            }
            item.Reference = matches[0];
        }

        if (line.Location is { } location)
        {
            var place = location.ToPlace();
            if (fit.CargoOnly && location != Location.Cargo)
            {
                throw new EsfException("a fit without a ship stores everything in cargo", n);
            }
            if (line.Index is not null && Places.Default(kind) != place)
            {
                throw new EsfException($"'{type.Name}' does not go in the {location.Name()} rack", n);
            }
            if (location == Location.Bay && kind is not (Kind.Drone or Kind.Fighter))
            {
                throw new EsfException("@bay is only for drones and fighters", n);
            }
            item.Move(place);
        }

        if (item.Place.IsPlugged() && line.Count is not null)
        {
            throw new EsfException("a plugged-in implant or booster takes no count", n);
        }

        if (line.State is { } state)
        {
            if (item.Stored)
            {
                throw new EsfException("a stored item has no state", n);
            }
            if (item.Place.IsPlugged() && state != State.Off)
            {
                throw new EsfException("an implant or booster takes !off only", n);
            }
        }

        if (line.Charge is not null)
        {
            item.Charge = LookUp(catalog, line.Charge, n);
            if (item.Charge.Kind != Kind.Charge)
            {
                throw new EsfException($"'{item.Charge.Name}' is not a charge", n);
            }
        }

        if (line.Mutaplasmid is not null)
        {
            var mutaplasmids = catalog.Mutaplasmids(type.Id)
                .Where(m => Names.IsWordPrefix(line.Mutaplasmid, m.Type.Name))
                .ToList();
            item.Mutaplasmid = Pick(
                mutaplasmids, line.Mutaplasmid, $"mutaplasmid for '{type.Name}'", n, m => m.Type.Name);
        }

        foreach (var (name, value) in line.Overrides)
        {
            var attribute = catalog.AttributeByName(Names.Fold(name))
                ?? throw new EsfException($"unknown attribute '{name}'", n);
            if (item.Overrides.Exists(o => o.Attribute.Id == attribute.Id))
            {
                throw new EsfException($"attribute '{attribute.Name}' is overridden twice", n);
            }
            var number = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (!double.IsFinite(number))
            {
                throw new EsfException($"value '{value}' is out of range", n);
            }
            item.Overrides.Add((attribute, number));
        }

        return item;
    }

    /// <summary>A fit cannot contain itself, directly or through other fits.</summary>
    private static void CheckCycles(List<Fit> fits)
    {
        var done = new HashSet<Fit>();
        var path = new HashSet<Fit>();
        var stack = new Stack<(Fit Fit, int Next)>();
        foreach (var start in fits)
        {
            if (done.Contains(start))
            {
                continue;
            }
            path.Add(start);
            stack.Push((start, 0));
            while (stack.TryPop(out var top))
            {
                var (fit, next) = top;
                var at = fit.Parts.FindIndex(next, p => p is Item { Reference: not null });
                if (at < 0)
                {
                    path.Remove(fit);
                    done.Add(fit);
                    continue;
                }
                stack.Push((fit, at + 1));
                var item = (Item)fit.Parts[at];
                var reference = item.Reference!;
                if (path.Contains(reference))
                {
                    throw new EsfException("a fit cannot contain itself", item.Line.Number);
                }
                if (!done.Contains(reference))
                {
                    path.Add(reference);
                    stack.Push((reference, 0));
                }
            }
        }
    }

    /// <summary>Assign slots in one rack (§6.3).</summary>
    private static List<Run> Layout(List<Part> parts)
    {
        var pinned = new Dictionary<long, Part>();
        foreach (var part in parts)
        {
            if (part.Line.Index is { } index && !pinned.TryAdd(index, part))
            {
                throw new EsfException($"slot {index} is pinned twice", part.Line.Number);
            }
        }

        var pins = pinned.Keys.Order().ToList();
        var runs = pinned.Select(p => new Run(p.Key, 1, p.Value as Item)).ToList();
        long pos = 1;
        foreach (var part in parts)
        {
            if (part.Line.Index is not null)
            {
                continue;
            }
            long remaining = part.Line.Count ?? 1;
            while (remaining > 0)
            {
                while (pinned.ContainsKey(pos))
                {
                    pos++;
                }
                var at = ~pins.BinarySearch(pos);
                var room = at < pins.Count ? pins[at] - pos : remaining;
                var take = Math.Min(remaining, room);
                runs.Add(new Run(pos, take, part as Item));
                pos += take;
                remaining -= take;
            }
        }
        runs.Sort((a, b) => a.Start.CompareTo(b.Start));

        var gapless = new List<Run>(runs.Count);
        pos = 1;
        foreach (var run in runs)
        {
            if (run.Start > pos)
            {
                gapless.Add(new Run(pos, run.Start - pos, null));
            }
            gapless.Add(run);
            pos = run.Start + run.Length;
        }
        return gapless;
    }
}
