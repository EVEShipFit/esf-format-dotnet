namespace EveShipFit.EsfFormat.Internal;

/// <summary>Unicode simple case folding of one code point.</summary>
internal static partial class CaseFolding
{
    public static int Fold(int codePoint)
    {
        var at = From.BinarySearch(codePoint);
        return at >= 0 ? To[at] : codePoint;
    }
}
