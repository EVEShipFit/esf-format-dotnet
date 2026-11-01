namespace EveShipFit.EsfFormat;

/// <summary>A broken rule of esf/1, and where: a line of text, or a fit and entry, counting from 1.</summary>
public sealed class EsfException : Exception
{
    internal EsfException(string reason, int? lineNumber = null, int? fitNumber = null, int? entryNumber = null)
        : base(Where(lineNumber, fitNumber, entryNumber) + reason)
    {
        Reason = reason;
        LineNumber = lineNumber;
        FitNumber = fitNumber;
        EntryNumber = entryNumber;
    }

    public string Reason { get; }

    public int? LineNumber { get; }

    public int? FitNumber { get; }

    public int? EntryNumber { get; }

    private static string Where(int? line, int? fit, int? entry) => (line, fit, entry) switch
    {
        (_, not null, null) => $"fit {fit}: ",
        (_, not null, not null) => $"fit {fit}, entry {entry}: ",
        (not null, null, _) => $"line {line}: ",
        _ => "",
    };
}
