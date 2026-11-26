namespace EveShipFit.EsfFormat;

/// <summary>A whole document: its fits, and the data that is not a fit.</summary>
/// <remarks>
/// <see cref="Esf.LoadDocument(ReadOnlySpan{byte}, ICatalog)"/> returns it. <see cref="Fits"/> holds the same fits
/// that <see cref="Esf.Load(ReadOnlySpan{byte}, ICatalog)"/> returns for the document.
/// </remarks>
public sealed record EsfDocument
{
    /// <summary>The fits, in canonical form.</summary>
    public IReadOnlyList<EsfFit> Fits { get; init; } = [];

    /// <summary>The blocks and binary fields that are not fits, in the order they appear in the document.</summary>
    /// <remarks>Empty when the document holds only fits.</remarks>
    public IReadOnlyList<Extra> Extra { get; init; } = [];

    public bool Equals(EsfDocument? other) =>
        other is not null && Fits.SequenceEqual(other.Fits) && Extra.SequenceEqual(other.Extra);

    public override int GetHashCode() => HashCode.Combine(Fits.Count, Extra.Count);
}

/// <summary>Data in a document that is not a fit: a <see cref="TextExtra"/> or a <see cref="BinaryExtra"/>.</summary>
/// <remarks>
/// Other formats, like a fleet, can share a document with fits. esf/1 readers skip that data;
/// <see cref="Esf.LoadDocument(ReadOnlySpan{byte}, ICatalog)"/> hands it back as these.
/// </remarks>
public abstract record Extra
{
    private protected Extra()
    {
    }
}

/// <summary>A block of a text document whose header is not <c>%esf/N</c>, like <c>%fleet/1</c>.</summary>
/// <param name="Name">The name in the header, like "fleet".</param>
/// <param name="Version">The version in the header, like 1.</param>
/// <param name="Text">
/// The block after its header line, exactly as written: up to the next header line or the end of the document.
/// Line endings and blank lines are kept. It can be empty.
/// </param>
public sealed record TextExtra(string Name, uint Version, string Text) : Extra;

/// <summary>A field of a binary document other than the fits, like one that holds a fleet.</summary>
/// <remarks>
/// Only fields that hold bytes (Protocol Buffers wire type 2) come back. Other fields are skipped.
/// To write one, append <see cref="Esf.BinaryField"/> to what <see cref="Esf.SaveBinary"/> returns.
/// </remarks>
/// <param name="Field">The field number in the <c>Document</c> message, at least 2.</param>
/// <param name="Bytes">The content of the field.</param>
public sealed record BinaryExtra(uint Field, byte[] Bytes) : Extra
{
    public bool Equals(BinaryExtra? other) =>
        other is not null && Field == other.Field && Bytes.AsSpan().SequenceEqual(other.Bytes);

    public override int GetHashCode() => HashCode.Combine(Field, Bytes.Length);
}
