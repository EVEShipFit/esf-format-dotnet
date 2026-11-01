using System.Text;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>Name matching rules (SPEC §6.10, §6.1, §6.6).</summary>
internal static class Names
{
    /// <summary>Unicode simple case folding.</summary>
    public static string Fold(string s)
    {
        if (Ascii.IsValid(s))
        {
            return s.ToLowerInvariant();
        }
        var folded = new StringBuilder(s.Length);
        foreach (var rune in s.EnumerateRunes())
        {
            folded.Append(new Rune(CaseFolding.Fold(rune.Value)));
        }
        return folded.ToString();
    }

    /// <summary>Whether <paramref name="prefix"/> is <paramref name="name"/> with zero or more whole words dropped from the end.</summary>
    public static bool IsWordPrefix(string prefix, string name) => IsFoldedWordPrefix(Fold(prefix), Fold(name));

    /// <summary>The fewest leading words of <paramref name="name"/> that are a word prefix of no name in <paramref name="others"/>.</summary>
    public static string ShortestUnique(string name, IEnumerable<string> others)
    {
        var folded = others.Select(Fold).ToList();
        var end = -1;
        do
        {
            end = name.IndexOf(' ', end + 1);
            var candidate = end < 0 ? name : name[..end];
            var foldedCandidate = Fold(candidate);
            if (!folded.Exists(other => IsFoldedWordPrefix(foldedCandidate, other)))
            {
                return candidate;
            }
        } while (end >= 0);
        return name;
    }

    /// <summary>Strings in Unicode code point order, as UTF-8 bytes sort.</summary>
    public static int CompareCodePoints(string? a, string? b)
    {
        var length = Math.Min(a!.Length, b!.Length);
        for (var i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                return CodePointOrder(a[i]) - CodePointOrder(b[i]);
            }
        }
        return a.Length - b.Length;
    }

    public static readonly IComparer<string> CodePoints = Comparer<string>.Create(CompareCodePoints);

    private static bool IsFoldedWordPrefix(string prefix, string name) =>
        name.StartsWith(prefix, StringComparison.Ordinal)
        && (name.Length == prefix.Length || name[prefix.Length] == ' ');

    private static int CodePointOrder(char c) => c switch
    {
        >= '' => c - 0x800,
        >= '\uD800' => c + 0x2000,
        _ => c,
    };
}
