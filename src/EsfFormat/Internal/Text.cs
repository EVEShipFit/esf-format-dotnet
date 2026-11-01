using System.Globalization;
using System.Text.RegularExpressions;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>One line of a fit, before its names are resolved.</summary>
internal abstract class Line(int number)
{
    public int Number { get; } = number;

    public uint? Count { get; init; }

    /// <summary>The slot the line pins.</summary>
    public uint? Index { get; set; }
}

internal sealed class EmptyLine(int number, Location rack) : Line(number)
{
    public Location Rack { get; } = rack;
}

internal sealed class ItemLine(int number, string name) : Line(number)
{
    public string Name { get; } = name;

    public string? FitName { get; set; }

    public string? Charge { get; set; }

    public uint? ChargeCount { get; set; }

    public string? Mutaplasmid { get; set; }

    public List<(string Name, string Value)> Overrides { get; set; } = [];

    public State? State { get; set; }

    public Location? Location { get; set; }
}

internal sealed class FitBlock(int number, int hullLine)
{
    public int Number { get; } = number;

    public int HullLine { get; } = hullLine;

    public string? Hull { get; set; }

    public string? FitName { get; set; }

    public string? Mode { get; set; }

    public List<Line> Lines { get; } = [];
}

/// <summary>Reading the text form into lines, without a Catalog (SPEC §4.1, grammar.ebnf).</summary>
internal static partial class Text
{
    public static bool IsSigil(int c) => c is ':' or '+' or '{' or '!' or '@' or '/' or '"';

    public static bool IsCount(ReadOnlySpan<char> token) =>
        token.Length > 1 && token[^1] == 'x' && !token[..^1].ContainsAnyExceptInRange('0', '9');

    /// <summary>Split a text document into fits; other blocks are skipped.</summary>
    public static List<FitBlock> Parse(string text)
    {
        var lines = text.Split('\n');
        var count = lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;

        var fits = new List<FitBlock>();
        FitBlock? current = null;
        int? header = null;
        var inBlock = false;
        for (var i = 0; i < count; i++)
        {
            var number = i + 1;
            var raw = lines[i].EndsWith('\r') ? lines[i][..^1] : lines[i];
            if (raw.Contains('\r', StringComparison.Ordinal))
            {
                throw new EsfException("a CR must be followed by an LF", number);
            }

            var content = raw.AsSpan().TrimStart(' ');
            if (content.StartsWith("%"))
            {
                var match = HeaderPattern().Match(raw);
                if (!match.Success)
                {
                    throw new EsfException("malformed header", number);
                }
                if (header is not null)
                {
                    throw new EsfException("a fit needs a hull line", header);
                }
                inBlock = true;
                current = null;
                if (match.Groups[1].ValueSpan is "esf")
                {
                    if (match.Groups[2].ValueSpan is not "1")
                    {
                        throw new EsfException($"unsupported version esf/{match.Groups[2].Value}", number);
                    }
                    header = number;
                }
                continue;
            }

            if (!inBlock)
            {
                throw new EsfException("a document starts with a header", number);
            }
            if (content.IsEmpty || content.StartsWith("//"))
            {
                continue;
            }

            var cursor = new Cursor(number, raw);
            if (header is not null)
            {
                current = cursor.Hull(header.Value);
                fits.Add(current);
                header = null;
            }
            else
            {
                current?.Lines.Add(cursor.Entry());
            }
        }

        if (header is not null)
        {
            throw new EsfException("a fit needs a hull line", header);
        }
        if (fits.Count == 0)
        {
            throw new EsfException("a document holds at least one fit");
        }
        return fits;
    }

    [GeneratedRegex(@"\A *%([a-z][a-z0-9-]*)/([0-9]+) *\z")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"\G!(off|on|heat)")]
    private static partial Regex StatePattern();

    [GeneratedRegex(@"\G@([a-z]+)([0-9]*)")]
    private static partial Regex LocationPattern();

    [GeneratedRegex(@"\G([A-Za-z][A-Za-z0-9]*) +(-?[0-9]+(?:\.[0-9]+)?)")]
    private static partial Regex OverridePattern();

    private sealed class Cursor(int number, string s)
    {
        private const int End = -1;

        private int i;

        public FitBlock Hull(int header)
        {
            var block = new FitBlock(header, number);
            SkipSpaces();
            if (Token() is "-")
            {
                i++;
                if (NextElement())
                {
                    if (Peek() != '"')
                    {
                        throw Error("a hull line of '-' takes only a fit name");
                    }
                    block.FitName = Quoted();
                }
                Finish();
                return block;
            }

            if (IsCount(Token()))
            {
                throw Error("the hull line cannot have a count");
            }
            block.Hull = TypeName();
            var more = NextElement();
            if (more && Peek() == '"')
            {
                block.FitName = Quoted();
                more = NextElement();
            }
            if (more)
            {
                if (Peek() != '/')
                {
                    throw Error($"unexpected '{Token()}' on the hull line");
                }
                i++;
                block.Mode = TypeName();
            }
            Finish();
            return block;
        }

        public Line Entry()
        {
            SkipSpaces();
            var count = Count();

            if (Token() is "-")
            {
                i++;
                if (!NextElement() || Peek() != '@')
                {
                    throw Error("an empty slot must name its rack");
                }
                var (rack, index) = Location();
                if (!rack.ToPlace().IsRack())
                {
                    throw Error("an empty slot must name a rack");
                }
                if (index is not null && count is not null)
                {
                    throw Error("a count and a pinned slot are mutually exclusive");
                }
                Finish();
                return new EmptyLine(number, rack) { Count = count, Index = index };
            }

            var line = new ItemLine(number, TypeName()) { Count = count };
            var more = NextElement();

            if (more && Peek() == '"')
            {
                line.FitName = Quoted();
                if (NextElement())
                {
                    if (Peek() != '@')
                    {
                        throw Error("a reference takes only a count and a location");
                    }
                    var (location, index) = Location();
                    if (index is not null || !location.ToPlace().IsStored())
                    {
                        throw Error("a reference can only be stored");
                    }
                    line.Location = location;
                    Finish();
                }
                return line;
            }

            var stage = 0;
            while (more)
            {
                var c = Peek();
                if (c == ':' && stage < 1)
                {
                    i++;
                    line.ChargeCount = Count();
                    line.Charge = TypeName();
                    stage = 1;
                }
                else if (c == '+' && stage < 2)
                {
                    i++;
                    line.Mutaplasmid = TypeName();
                    stage = 2;
                }
                else if (c == '{' && stage < 3)
                {
                    line.Overrides = Overrides();
                    stage = 3;
                }
                else if (c == '!' && stage < 4)
                {
                    var match = StatePattern().Match(s, i);
                    if (!match.Success)
                    {
                        throw Error($"unknown state '{Token()}'");
                    }
                    line.State = Places.ParseState(match.Groups[1].Value);
                    i += match.Length;
                    stage = 4;
                }
                else if (c == '@' && stage < 5)
                {
                    var (location, index) = Location();
                    if (index is null && !location.ToPlace().IsStored())
                    {
                        throw Error("an item names a rack only to pin a slot, which needs an index");
                    }
                    if (index is not null && count is not null)
                    {
                        throw Error("a count and a pinned slot are mutually exclusive");
                    }
                    line.Location = location;
                    line.Index = index;
                    stage = 5;
                }
                else
                {
                    throw Error($"unexpected '{Token()}'");
                }
                more = NextElement();
            }
            return line;
        }

        private EsfException Error(string message) => new(message, number);

        private int Peek() => i < s.Length ? s[i] : End;

        private ReadOnlySpan<char> Token()
        {
            var rest = s.AsSpan(i);
            var end = rest.IndexOf(' ');
            return end < 0 ? rest : rest[..end];
        }

        private bool SkipSpaces()
        {
            var start = i;
            while (Peek() == ' ')
            {
                i++;
            }
            return i > start;
        }

        /// <summary>Step over the separating space; false at the end of the line or a comment.</summary>
        private bool NextElement()
        {
            var spaced = SkipSpaces();
            if (i >= s.Length)
            {
                return false;
            }
            if (s.AsSpan(i).StartsWith("//"))
            {
                if (!spaced)
                {
                    throw Error("a comment needs a space before it");
                }
                i = s.Length;
                return false;
            }
            if (!spaced)
            {
                throw Error($"expected a space before '{Token()}'");
            }
            return true;
        }

        private uint Positive(ReadOnlySpan<char> digits, string what)
        {
            digits = digits.TrimStart('0');
            if (digits.IsEmpty)
            {
                throw Error($"{what} must be at least 1");
            }
            if (digits.Length > 10 || !uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                throw Error($"{what} is too large");
            }
            return value;
        }

        private void Finish()
        {
            if (NextElement())
            {
                throw Error($"unexpected '{Token()}'");
            }
        }

        private uint? Count()
        {
            var token = Token();
            if (!IsCount(token))
            {
                return null;
            }
            i += token.Length;
            if (!NextElement())
            {
                throw Error("a count must be followed by a type name");
            }
            return Positive(token[..^1], "a count");
        }

        private string Quoted()
        {
            var value = new System.Text.StringBuilder();
            var from = i + 1;
            while (true)
            {
                var quote = s.IndexOf('"', from);
                if (quote < 0)
                {
                    throw Error("unterminated quoted string");
                }
                value.Append(s, from, quote - from);
                if (quote + 1 < s.Length && s[quote + 1] == '"')
                {
                    value.Append('"');
                    from = quote + 2;
                    continue;
                }
                i = quote + 1;
                break;
            }
            if (value.Length == 0)
            {
                throw Error("a quoted string cannot be empty");
            }
            return value.ToString();
        }

        private string TypeName()
        {
            var c = Peek();
            if (c == '"')
            {
                return Quoted();
            }
            if (c is End or ' ' || IsSigil(c))
            {
                throw Error("expected a type name");
            }
            if (c is '-' or '%')
            {
                throw Error($"a name starting with '{(char)c}' must be quoted");
            }
            var start = i;
            while (true)
            {
                i += Token().Length;
                var end = i;
                SkipSpaces();
                c = Peek();
                if (i == end || c is End or ' ' || IsSigil(c))
                {
                    i = end;
                    var name = s[start..end];
                    return name.Contains("  ", StringComparison.Ordinal)
                        ? string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        : name;
                }
            }
        }

        private (Location Location, uint? Index) Location()
        {
            var match = LocationPattern().Match(s, i);
            if (!match.Success)
            {
                throw Error($"unknown location '{Token()}'");
            }
            i += match.Length;
            var name = match.Groups[1].Value;
            var index = match.Groups[2].ValueSpan;
            var known = Places.TryParseLocation(name, out var location);
            if (!index.IsEmpty)
            {
                if (!known || !location.ToPlace().IsRack())
                {
                    throw Error($"unknown rack '{name}'");
                }
                return (location, Positive(index, "a slot number"));
            }
            if (!known)
            {
                throw Error($"unknown location '{name}'");
            }
            return (location, null);
        }

        private List<(string Name, string Value)> Overrides()
        {
            i++;
            SkipSpaces();
            var pairs = new List<(string, string)>();
            while (true)
            {
                var match = OverridePattern().Match(s, i);
                if (!match.Success)
                {
                    throw Error("malformed override; expected 'attribute value'");
                }
                pairs.Add((match.Groups[1].Value, match.Groups[2].Value));
                i += match.Length;
                SkipSpaces();
                if (Peek() == ',')
                {
                    i++;
                    SkipSpaces();
                    continue;
                }
                if (Peek() == '}')
                {
                    i++;
                    return pairs;
                }
                throw Error("malformed overrides; expected ',' or '}'");
            }
        }
    }
}
