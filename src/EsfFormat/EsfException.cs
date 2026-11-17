namespace EveShipFit.EsfFormat;

/// <summary>An esf/1 error, and where: a line of text, or a fit and entry, counting from 1.</summary>
/// <remarks>
/// Every error is one of three kinds. Catch <see cref="EsfFormatException"/>, <see cref="EsfCatalogException"/> or
/// <see cref="EsfRuleException"/> to handle one kind, or this class to handle them all.
/// </remarks>
public abstract class EsfException : Exception
{
    private protected EsfException(string reason, int? lineNumber, int? fitNumber, int? entryNumber)
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

    internal abstract EsfException At(int fit, int? entry);

    private static string Where(int? line, int? fit, int? entry) => (line, fit, entry) switch
    {
        (_, not null, null) => $"fit {fit}: ",
        (_, not null, not null) => $"fit {fit}, entry {entry}: ",
        (not null, null, _) => $"line {line}: ",
        _ => "",
    };
}

/// <summary>The data is not a well-formed esf/1 document.</summary>
/// <remarks>
/// The text does not follow the grammar, the binary form or base64url is broken, or a fit name cannot be written.
/// </remarks>
public sealed class EsfFormatException : EsfException
{
    internal EsfFormatException(string reason, int? lineNumber = null, int? fitNumber = null, int? entryNumber = null)
        : base(reason, lineNumber, fitNumber, entryNumber)
    {
    }

    internal override EsfException At(int fit, int? entry) => new EsfFormatException(Reason, fitNumber: fit, entryNumber: entry);
}

/// <summary>The catalog does not know a name or ID, or a name matches more than one type.</summary>
/// <remarks>This often means the catalog is older than the fit.</remarks>
public sealed class EsfCatalogException : EsfException
{
    internal EsfCatalogException(string reason, int? lineNumber = null, int? fitNumber = null, int? entryNumber = null)
        : base(reason, lineNumber, fitNumber, entryNumber)
    {
    }

    internal override EsfException At(int fit, int? entry) => new EsfCatalogException(Reason, fitNumber: fit, entryNumber: entry);
}

/// <summary>The fit breaks a rule of esf/1.</summary>
/// <remarks>
/// The document is well formed and the catalog knows every type, but the fit is not valid. For example, a module is
/// in the wrong rack.
/// </remarks>
public sealed class EsfRuleException : EsfException
{
    internal EsfRuleException(string reason, int? lineNumber = null, int? fitNumber = null, int? entryNumber = null)
        : base(reason, lineNumber, fitNumber, entryNumber)
    {
    }

    internal override EsfException At(int fit, int? entry) => new EsfRuleException(Reason, fitNumber: fit, entryNumber: entry);
}
