using System.Text;
using System.Text.Unicode;
using EveShipFit.EsfFormat.Internal;

namespace EveShipFit.EsfFormat;

/// <summary>Read and write esf/1 (https://github.com/EVEShipFit/esf-format).</summary>
public static class Esf
{
    /// <summary>Whether a document is text, by its first character (§10).</summary>
    public static bool IsText(ReadOnlySpan<byte> data) => data is [(byte)'%' or (byte)' ', ..];

    /// <summary>Whether a document is text, by its first character (§10).</summary>
    public static bool IsText(string data) => data is ['%' or ' ', ..];

    /// <summary>Read a text or binary document, in canonical form.</summary>
    /// <exception cref="EsfException">The document breaks a rule of esf/1.</exception>
    public static IReadOnlyList<EsfFit> Load(ReadOnlySpan<byte> data, ICatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (data.IsEmpty)
        {
            throw new EsfException("the document is empty");
        }
        var fits = IsText(data)
            ? Canonical(Text.Parse(Decode(data)), catalog)
            : Checked(Binary.Decode(data), catalog);
        return fits.ConvertAll(Internal.Canonical.ToModel);
    }

    /// <summary>Read a text or binary document, in canonical form.</summary>
    /// <exception cref="EsfException">The document breaks a rule of esf/1.</exception>
    public static IReadOnlyList<EsfFit> Load(string data, ICatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(data);
        var bytes = new byte[Encoding.UTF8.GetMaxByteCount(data.Length)];
        if (Utf8.FromUtf16(data, bytes, out _, out var written, replaceInvalidSequences: false) != System.Buffers.OperationStatus.Done)
        {
            throw new EsfException("not valid UTF-8");
        }
        return Load(bytes.AsSpan(0, written), catalog);
    }

    /// <summary>Write fits as canonical text (§8).</summary>
    /// <exception cref="EsfException">A fit breaks a rule of esf/1.</exception>
    public static string Save(IReadOnlyList<EsfFit> fits, ICatalog catalog) =>
        Internal.Canonical.Render(Checked(fits, catalog));

    /// <summary>Write fits in the binary form (§10).</summary>
    /// <exception cref="EsfException">A fit breaks a rule of esf/1.</exception>
    public static byte[] SaveBinary(IReadOnlyList<EsfFit> fits, ICatalog catalog) =>
        Binary.Encode(Checked(fits, catalog).Select(Internal.Canonical.ToModel));

    /// <summary>The binary form as base64url without padding, for URLs (§10.4).</summary>
    public static string ToBase64Url(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The binary form from base64url without padding (§10.4).</summary>
    /// <exception cref="EsfException">The text is not base64url.</exception>
    public static byte[] FromBase64Url(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        encoded = encoded.Trim();
        if (encoded.Length % 4 == 1 || !IsBase64Url(encoded))
        {
            throw new EsfException("not valid base64url");
        }
        var padded = encoded.Replace('-', '+').Replace('_', '/') + new string('=', -encoded.Length & 3);
        return Convert.FromBase64String(padded);
    }

    /// <summary>Unicode simple case folding, for indexing a Catalog by name (§6.10).</summary>
    public static string Fold(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Names.Fold(name);
    }

    private static bool IsBase64Url(string encoded)
    {
        foreach (var c in encoded)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))
            {
                return false;
            }
        }
        return true;
    }

    private static string Decode(ReadOnlySpan<byte> data)
    {
        var chars = new char[data.Length];
        var status = Utf8.ToUtf16(data, chars, out var read, out var written, replaceInvalidSequences: false);
        if (status != System.Buffers.OperationStatus.Done)
        {
            throw new EsfException($"not valid UTF-8 at byte {read}");
        }
        return new string(chars, 0, written);
    }

    private static List<CanonFit> Canonical(List<FitBlock> blocks, ICatalog catalog, IReadOnlyList<EsfFit>? given = null)
    {
        var fits = Resolver.Resolve(blocks, catalog);
        if (given is not null)
        {
            Source.Verify(fits, given);
        }
        return fits.ConvertAll(fit => Internal.Canonical.Fit(fit, catalog));
    }

    private static List<CanonFit> Checked(IReadOnlyList<EsfFit> fits, ICatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(fits);
        ArgumentNullException.ThrowIfNull(catalog);
        var (text, where) = Source.Write(fits, catalog);
        try
        {
            return Canonical(Text.Parse(text), catalog, fits);
        }
        catch (EsfException e) when (e.LineNumber is { } line)
        {
            var (fit, entry) = where[line - 1];
            throw new EsfException(e.Reason, fitNumber: fit, entryNumber: entry);
        }
    }
}
