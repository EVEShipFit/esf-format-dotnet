using System.Globalization;
using System.Text;
using System.Text.Unicode;
using EveShipFit.EsfFormat.Internal;

namespace EveShipFit.EsfFormat;

/// <summary>Read and write esf/1 (https://github.com/EVEShipFit/esf-format).</summary>
public static class Esf
{
    private const uint MaxField = (1u << 29) - 1;

    /// <summary>Whether a document is text, by its first character (§10).</summary>
    public static bool IsText(ReadOnlySpan<byte> data) => data is [(byte)'%' or (byte)' ', ..];

    /// <summary>Whether a document is text, by its first character (§10).</summary>
    public static bool IsText(string data) => data is ['%' or ' ', ..];

    /// <summary>Read a text or binary document, in canonical form.</summary>
    /// <exception cref="EsfException">The document breaks a rule of esf/1.</exception>
    public static IReadOnlyList<EsfFit> Load(ReadOnlySpan<byte> data, ICatalog catalog) => Read(data, catalog, null);

    /// <summary>Read a text or binary document, in canonical form.</summary>
    /// <exception cref="EsfException">The document breaks a rule of esf/1.</exception>
    public static IReadOnlyList<EsfFit> Load(string data, ICatalog catalog) => Load(Encode(data), catalog);

    /// <summary>Read a text or binary document: its fits, and the data that is not a fit.</summary>
    /// <remarks>
    /// <para>
    /// The fits are the same as <see cref="Load(ReadOnlySpan{byte}, ICatalog)"/> returns. Load skips other
    /// data; this method returns it in <see cref="EsfDocument.Extra"/>, in document order.
    /// </para>
    /// <para>
    /// In a text document, that is each block whose header is not <c>%esf/N</c>, as a <see cref="TextExtra"/>.
    /// In a binary document, it is each field of <c>Document</c> other than the fits that holds bytes, as a
    /// <see cref="BinaryExtra"/>. A binary field of any other wire type is still skipped.
    /// </para>
    /// <para>A <c>%esf/N</c> header with a version other than 1 is an error, as with Load.</para>
    /// <para>
    /// The version of another block must fit in a <see cref="uint"/>, so <c>%fleet/99999999999</c> is an error here,
    /// while Load skips that block. Leading zeros are allowed: <c>%fleet/007</c> is version 7.
    /// </para>
    /// </remarks>
    /// <exception cref="EsfException">
    /// The document breaks a rule of esf/1, or the version in a block's header is more than <see cref="uint.MaxValue"/>.
    /// </exception>
    public static EsfDocument LoadDocument(ReadOnlySpan<byte> data, ICatalog catalog)
    {
        var extra = new List<Extra>();
        var fits = Read(data, catalog, extra);
        return new EsfDocument { Fits = fits, Extra = extra };
    }

    /// <summary>Read a text or binary document: its fits, and the data that is not a fit.</summary>
    /// <remarks>See <see cref="LoadDocument(ReadOnlySpan{byte}, ICatalog)"/>.</remarks>
    /// <exception cref="EsfException">
    /// The document breaks a rule of esf/1, or the version in a block's header is more than <see cref="uint.MaxValue"/>.
    /// </exception>
    public static EsfDocument LoadDocument(string data, ICatalog catalog) => LoadDocument(Encode(data), catalog);

    /// <summary>Write fits as canonical text (§8).</summary>
    /// <exception cref="EsfException">A fit breaks a rule of esf/1.</exception>
    public static string Save(IReadOnlyList<EsfFit> fits, ICatalog catalog) =>
        Internal.Canonical.Render(Checked(fits, catalog));

    /// <summary>Write fits in the binary form (§10).</summary>
    /// <exception cref="EsfException">A fit breaks a rule of esf/1.</exception>
    public static byte[] SaveBinary(IReadOnlyList<EsfFit> fits, ICatalog catalog) =>
        Binary.Encode(Checked(fits, catalog).Select(Internal.Canonical.ToModel));

    /// <summary>One field of a binary document, to append to what <see cref="SaveBinary"/> returns.</summary>
    /// <remarks>
    /// <para>
    /// A binary document is a list of fields, and two documents joined end to end are one document. So
    /// <c>[.. Esf.SaveBinary(fits, catalog), .. Esf.BinaryField(2, bytes)]</c> is a document that holds the fits and
    /// this field. <see cref="LoadDocument(ReadOnlySpan{byte}, ICatalog)"/> reads it back as a
    /// <see cref="BinaryExtra"/> with the same <paramref name="field"/> and <paramref name="bytes"/>.
    /// </para>
    /// <para>Field 1 holds the fits, so pick a number that esf/1 does not use.</para>
    /// </remarks>
    /// <param name="field">The field number: at least 2, and at most 536,870,911, the largest Protocol Buffers allows.</param>
    /// <param name="bytes">The content of the field.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="field"/> is 0, 1 or more than 536,870,911.</exception>
    public static byte[] BinaryField(uint field, ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(field, 2u);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(field, MaxField);
        return Binary.Field((int)field, bytes);
    }

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
            throw new EsfFormatException("not valid base64url");
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

    private static List<EsfFit> Read(ReadOnlySpan<byte> data, ICatalog catalog, List<Extra>? extra)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (data.IsEmpty)
        {
            throw new EsfFormatException("the document is empty");
        }
        List<CanonFit> fits;
        if (IsText(data))
        {
            var others = extra is null ? null : new List<OtherBlock>();
            fits = Canonical(Text.Parse(Decode(data), others), catalog);
            extra?.AddRange(others!.Select(TextExtra));
        }
        else
        {
            var others = extra is null ? null : new List<BinaryExtra>();
            fits = Checked(Binary.Decode(data, others), catalog);
            extra?.AddRange(others!);
        }
        return fits.ConvertAll(Internal.Canonical.ToModel);
    }

    private static TextExtra TextExtra(OtherBlock block) =>
        uint.TryParse(block.Version, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? new TextExtra(block.Name, version, block.Text)
            : throw new EsfFormatException($"the version of %{block.Name} is too large", block.Line);

    private static ReadOnlySpan<byte> Encode(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var bytes = new byte[Encoding.UTF8.GetMaxByteCount(data.Length)];
        if (Utf8.FromUtf16(data, bytes, out _, out var written, replaceInvalidSequences: false) != System.Buffers.OperationStatus.Done)
        {
            throw new EsfFormatException("not valid UTF-8");
        }
        return bytes.AsSpan(0, written);
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
            throw new EsfFormatException($"not valid UTF-8 at byte {read}");
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
            throw e.At(fit, entry);
        }
    }
}
