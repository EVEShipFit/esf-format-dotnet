using System.Text;
using static EveShipFit.EsfFormat.Tests.Fixtures;

namespace EveShipFit.EsfFormat.Tests;

public class ApiTest
{
    private static readonly JsonCatalog Catalog = Fixtures.Catalog;

    [Fact]
    public void SaveCanonical()
    {
        var rifter = Named(
            "Shield Buffer",
            new Entry(DamageControl),
            new Entry(Autocannon) { Charge = Emp },
            new Entry(ShieldExtender),
            new Entry(Autocannon) { Charge = Emp },
            new Entry(null) { Location = Location.High },
            new Entry(Scrambler) { State = State.Off },
            new Entry(Hobgoblin) { Count = 2 },
            new Entry(Emp) { Count = 100, Location = Location.Cargo });
        Assert.Equal(
            """
            %esf/1
            Rifter "Shield Buffer"

            2x 200mm AutoCannon II :Republic Fleet EMP S

            Medium Shield Extender II
            Warp Scrambler II !off

            Damage Control II

            2x Hobgoblin II

            100x Republic Fleet EMP S

            """.ReplaceLineEndings("\n"),
            Esf.Save([rifter], Catalog));
    }

    [Fact]
    public void LoadCanonical()
    {
        var rifter = Fit(new Entry(Autocannon), new Entry(Autocannon), new Entry(Emp) { Location = Location.Cargo });
        Assert.Equal(
            [Fit(new Entry(Autocannon) { Count = 2 }, new Entry(Emp))],
            Esf.Load(Esf.Save([rifter], Catalog), Catalog));
    }

    [Fact]
    public void LoadText() =>
        Assert.Equal([new EsfFit { Hull = Rifter }], Esf.Load("%esf/1\nRifter\n", Catalog));

    [Fact]
    public void LoadBytes() =>
        Assert.Equal([new EsfFit { Hull = Rifter }], Esf.Load("%esf/1\nRifter\n"u8, Catalog));

    [Fact]
    public void MutaplasmidRolls()
    {
        var entry = new Entry(DamageControl)
        {
            Mutaplasmid = DecayedDamageControlMutaplasmid,
            Overrides = new Dictionary<int, double> { [50] = 31.5 },
        };
        var loaded = Esf.Load(Esf.Save([Fit(entry)], Catalog), Catalog).Single();
        Assert.Equal(
            [new(50, 31.5), new(974, 0.6), new(975, 0.6), new(976, 0.6), new(977, 0.6)],
            loaded.Entries[0].Overrides.ToList());
    }

    [Fact]
    public void BinaryRoundTrip()
    {
        EsfFit[] fits =
        [
            Fit(new Entry(SmallSecureContainer) { FitName = "Loot", Location = Location.Cargo }),
            new EsfFit { Hull = SmallSecureContainer, Name = "Loot", Entries = [new Entry(Emp) { Count = 10 }] },
        ];
        Assert.Equal(Esf.Load(Esf.Save(fits, Catalog), Catalog), Esf.Load(Esf.SaveBinary(fits, Catalog), Catalog));
    }

    public static TheoryData<string, EsfFit[], string> InvalidFits() => new()
    {
        { "no-fits", [], "at least one fit" },
        { "empty-no-rack", [Fit(new Entry(null))], "an empty slot must name its rack" },
        { "zero-count", [Fit(new Entry(Autocannon) { Count = 0 })], "at least 1" },
        { "zero-charge-count", [Fit(new Entry(Autocannon) { Charge = Emp, ChargeCount = 0 })], "at least 1" },
        { "unknown-type", [Fit(new Entry(999_999_999))], "unknown item type ID" },
        { "unknown-hull", [new EsfFit { Hull = 999_999_999 }], "unknown hull type ID" },
        { "unknown-charge", [Fit(new Entry(Autocannon) { Charge = 999_999_999 })], "unknown charge type ID" },
        {
            "unknown-attribute",
            [Fit(new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [999_999] = 1 } })],
            "unknown attribute ID"
        },
        { "rack-on-item", [Fit(new Entry(Autocannon) { Location = Location.High })], "only for an empty slot" },
        { "charge-count-alone", [Fit(new Entry(Autocannon) { ChargeCount = 10 })], "without a charge" },
        {
            "override-nan",
            [Fit(new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [50] = double.NaN } })],
            "not finite"
        },
        {
            "override-infinite",
            [Fit(new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [50] = double.PositiveInfinity } })],
            "not finite"
        },
        {
            "id-shadowed",
            [Fit(new Entry(AvatarUnpublished) { Location = Location.Cargo })],
            "not what its name resolves to"
        },
        { "contains-itself", [Named("Buffer", new Entry(Rifter) { FitName = "Buffer" })], "cannot contain itself" },
        { "name-newline", [Named("Two\nlines")], "CR or LF" },
        { "name-lone-surrogate", [Named("\uD800")], "not valid UTF-8" },
        { "fit-name-lone-surrogate", [Fit(new Entry(Rifter) { FitName = "\uDC00" })], "not valid UTF-8" },
    };

    [Theory]
    [MemberData(nameof(InvalidFits))]
    public void SaveInvalid(string name, EsfFit[] fits, string message)
    {
        Assert.NotEmpty(name);
        Assert.Contains(message, Assert.Throws<EsfException>(() => Esf.Save(fits, Catalog)).Message, StringComparison.Ordinal);
        Assert.Contains(message, Assert.Throws<EsfException>(() => Esf.SaveBinary(fits, Catalog)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HullIdShadowed()
    {
        var error = Assert.Throws<EsfException>(() => Esf.Save([new EsfFit { Hull = AvatarUnpublished }], Catalog));
        Assert.Equal($"fit 1: hull ID {AvatarUnpublished} is not what its name resolves to ({Avatar})", error.Message);
    }

    [Fact]
    public void OverrideIdShadowed()
    {
        var catalog = new RenamedAttribute(Catalog, 974, "cpu");
        var fit = Fit(new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [974] = 1 } });
        var error = Assert.Throws<EsfException>(() => Esf.Save([fit], catalog));
        Assert.Equal("fit 1, entry 1: attribute IDs [974] resolve to [50]", error.Message);
    }

    [Fact]
    public void ErrorLocation()
    {
        var error = Assert.Throws<EsfException>(
            () => Esf.Save([Fit(), Fit(new Entry(Autocannon), new Entry(999_999_999))], Catalog));
        Assert.Equal((2, 2, null), (error.FitNumber, error.EntryNumber, error.LineNumber));
        Assert.StartsWith("fit 2, entry 2: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorLocationOfAFit()
    {
        var error = Assert.Throws<EsfException>(
            () => Esf.Save([Fit(), new EsfFit { Hull = Rifter, Mode = 999_999_999 }], Catalog));
        Assert.Equal((2, null), (error.FitNumber, error.EntryNumber));
        Assert.StartsWith("fit 2: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorLocationOfAResolvedFit()
    {
        var error = Assert.Throws<EsfException>(() => Esf.Save([Fit(), new EsfFit { Hull = Emp }], Catalog));
        Assert.Equal("fit 2: 'Republic Fleet EMP S' is not a ship, structure or container", error.Message);
    }

    [Fact]
    public void ErrorLocationOfALine()
    {
        var error = Assert.Throws<EsfException>(() => Esf.Load("%esf/1\nRifter\nNot A Type\n", Catalog));
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("unknown type 'Not A Type'", error.Reason);
        Assert.Equal("line 3: unknown type 'Not A Type'", error.Message);
    }

    [Fact]
    public void ErrorWithoutLocation()
    {
        var error = Assert.Throws<EsfException>(() => Esf.Load(""u8, Catalog));
        Assert.Equal("the document is empty", error.Message);
    }

    [Fact]
    public void EmptyNameIsNoName() =>
        Assert.Equal("%esf/1\nRifter\n", Esf.Save([Named("")], Catalog));

    [Fact]
    public void NameZeroIsAName() =>
        Assert.Equal("%esf/1\nRifter \"0\"\n", Esf.Save([Named("0")], Catalog));

    [Fact]
    public void SharedNamePublishedWins() =>
        Assert.EndsWith(
            "Avatar\n",
            Esf.Save([Fit(new Entry(Avatar) { Location = Location.Cargo })], Catalog),
            StringComparison.Ordinal);

    public static TheoryData<byte[], string> InvalidDocuments() => new()
    {
        { [], "the document is empty" },
        { [.. "%esf/1\nRifter"u8, 0xED, 0xA0, 0x80, (byte)'\n'], "not valid UTF-8 at byte 13" },
        { [0x10, 0x01], "at least one fit" },
        {
            Encoding.UTF8.GetBytes(
                "%esf/1\nRifter\n4294967295x Damage Control II\n4294967295x Damage Control II\n"),
            "too large"
        },
    };

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void LoadInvalid(byte[] data, string message) =>
        Assert.Contains(message, Assert.Throws<EsfException>(() => Esf.Load(data, Catalog)).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData("99999999999x Damage Control II")]
    [InlineData("4294967296x Damage Control II")]
    [InlineData("0000000004294967296x Damage Control II")]
    public void CountTooLarge(string line) =>
        Assert.Equal(
            "line 3: a count is too large",
            Assert.Throws<EsfException>(() => Esf.Load($"%esf/1\nRifter\n{line}\n", Catalog)).Message);

    [Fact]
    public void BinaryEmptyNameIsNoName()
    {
        byte[] fit = [0x08, 0x01, 0x10, 0xCB, 0x04, 0x1A, 0x00];
        Assert.Equal([new EsfFit { Hull = Rifter }], Esf.Load([0x0A, (byte)fit.Length, .. fit], Catalog));
    }

    [Fact]
    public void LoadLoneSurrogate() =>
        Assert.Equal(
            "not valid UTF-8",
            Assert.Throws<EsfException>(() => Esf.Load("%esf/1\nRifter\uD800\n", Catalog)).Message);

    [Theory]
    [InlineData("Hecate /Defense", "no tactical modes")]
    [InlineData("Rifter\nDamage Control II +Decayed", "no mutaplasmid")]
    public void CatalogWithoutOptionalLookups(string lines, string message) =>
        Assert.Contains(
            message,
            Assert.Throws<EsfException>(() => Esf.Load($"%esf/1\n{lines}\n", new MinimalCatalog(Catalog))).Message,
            StringComparison.Ordinal);

    [Fact]
    public void CatalogMissingRollAttribute()
    {
        var error = Assert.Throws<KeyNotFoundException>(
            () => Esf.Load("%esf/1\nRifter\nDamage Control II +Decayed\n", new CatalogWithoutRollAttributes(Catalog)));
        Assert.Contains("no attribute", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Base64Url()
    {
        var data = Esf.SaveBinary([Fit(new Entry(Autocannon))], Catalog);
        var url = Esf.ToBase64Url(data);
        Assert.DoesNotContain('=', url);
        Assert.Equal(data, Esf.FromBase64Url(url));
        Assert.Equal([0xFB, 0xFF], Esf.FromBase64Url(" -_8\n"));
        Assert.Equal("-_8", Esf.ToBase64Url([0xFB, 0xFF]));
        Assert.Empty(Esf.FromBase64Url(""));
    }

    [Theory]
    [InlineData("not base64url!")]
    [InlineData("AA==")]
    [InlineData("AAAAA")]
    public void InvalidBase64Url(string encoded) =>
        Assert.Throws<EsfException>(() => Esf.FromBase64Url(encoded));

    [Fact]
    public void IsText()
    {
        Assert.True(Esf.IsText("%esf/1\n"));
        Assert.True(Esf.IsText(" %esf/1\n"));
        Assert.True(Esf.IsText("%esf/1\n"u8));
        Assert.True(Esf.IsText(" %esf/1\n"u8));
        Assert.False(Esf.IsText([0x0A, 0x02]));
        Assert.False(Esf.IsText("\n"));
        Assert.False(Esf.IsText(""));
        Assert.False(Esf.IsText(""u8));
    }

    [Theory]
    [InlineData("Damage Control II", "damage control ii")]
    [InlineData("ÄRGER Σς", "ärger σσ")]
    [InlineData("STRASSE ẞ", "strasse ß")]
    [InlineData("İ K Ω", "İ k ω")]
    [InlineData("\U00010400", "\U00010428")]
    [InlineData("ᏸ ꭰ", "Ᏸ Ꭰ")]
    public void Fold(string name, string folded) => Assert.Equal(folded, Esf.Fold(name));

    [Fact]
    public void Equality()
    {
        var entry = new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [50] = 1, [974] = 2 } };
        var same = new Entry(DamageControl) { Overrides = new Dictionary<int, double> { [974] = 2, [50] = 1 } };
        Assert.Equal(entry, same);
        Assert.Equal(entry.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(entry, same with { Overrides = new Dictionary<int, double> { [50] = 1, [974] = 3 } });
        Assert.NotEqual(entry, same with { Overrides = new Dictionary<int, double> { [50] = 1 } });
        Assert.NotEqual(entry, same with { Overrides = new Dictionary<int, double> { [50] = 1, [975] = 2 } });
        Assert.NotEqual(entry, same with { State = State.Off });
        Assert.False(entry.Equals(null));

        var fit = Fit(entry);
        Assert.Equal(fit, Fit(same));
        Assert.Equal(fit.GetHashCode(), Fit(same).GetHashCode());
        Assert.NotEqual(fit, Fit(entry, entry));
        Assert.NotEqual(fit, Fit(entry) with { Name = "Other" });
        Assert.False(fit.Equals(null));
    }

    [Fact]
    public void NullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Esf.Load((string)null!, Catalog));
        Assert.Throws<ArgumentNullException>(() => Esf.Load("%esf/1\nRifter\n"u8, null!));
        Assert.Throws<ArgumentNullException>(() => Esf.Save(null!, Catalog));
        Assert.Throws<ArgumentNullException>(() => Esf.Save([], null!));
        Assert.Throws<ArgumentNullException>(() => Esf.FromBase64Url(null!));
        Assert.Throws<ArgumentNullException>(() => Esf.Fold(null!));
    }

    [Fact]
    public void ManyFits()
    {
        var fits = Enumerable.Range(0, 5000)
            .Select(n => Named($"Fit {n}", new Entry(SmallSecureContainer) { FitName = $"Box {n}", Location = Location.Cargo }))
            .Concat(Enumerable.Range(0, 5000).Select(n => new EsfFit
            {
                Hull = SmallSecureContainer,
                Name = $"Box {n}",
                Entries = [new Entry(Emp) { Count = 10 }],
            }))
            .ToList();
        var text = Esf.Save(fits, Catalog);
        Assert.Equal(10000, Esf.Load(text, Catalog).Count);
        Assert.Equal(10000, Esf.Load(Esf.SaveBinary(fits, Catalog), Catalog).Count);
    }
}

internal class MinimalCatalog(ICatalog catalog) : ICatalog
{
    protected ICatalog Inner { get; } = catalog;

    public TypeRecord? TypeByName(string folded) => Inner.TypeByName(folded);

    public TypeRecord? TypeById(int typeId) => Inner.TypeById(typeId);

    public AttributeRecord? AttributeByName(string folded) => Inner.AttributeByName(folded);

    public virtual AttributeRecord? AttributeById(int attributeId) => Inner.AttributeById(attributeId);
}

internal sealed class CatalogWithoutRollAttributes(ICatalog catalog) : MinimalCatalog(catalog), ICatalog
{
    public override AttributeRecord? AttributeById(int attributeId) => null;

    public IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => Inner.Mutaplasmids(typeId);
}

/// <summary>A Catalog where one attribute ID has the name of another.</summary>
internal sealed class RenamedAttribute(ICatalog catalog, int id, string name) : ICatalog
{
    public TypeRecord? TypeByName(string folded) => catalog.TypeByName(folded);

    public TypeRecord? TypeById(int typeId) => catalog.TypeById(typeId);

    public AttributeRecord? AttributeByName(string folded) => catalog.AttributeByName(folded);

    public AttributeRecord? AttributeById(int attributeId) =>
        attributeId == id ? new AttributeRecord(id, name) : catalog.AttributeById(attributeId);
}
