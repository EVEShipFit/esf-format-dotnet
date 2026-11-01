using EveShipFit.EsfFormat.Internal;
using static EveShipFit.EsfFormat.Tests.Fixtures;

namespace EveShipFit.EsfFormat.Tests;

public class InternalTest
{
    public static TheoryData<double, string> Numbers() => new()
    {
        { 1e22, "10000000000000000000000" },
        { 1.5e-7, "0.00000015" },
        { -0.0, "0" },
        { 0.1, "0.1" },
        { Math.Pow(2, -24), "0.00000005960464477539063" },
        { 0.3, "0.3" },
        { 123.456, "123.456" },
        { -2.5, "-2.5" },
        { 100, "100" },
        { 9007199254740992, "9007199254740992" },
        { 5e-324, "0." + new string('0', 323) + "5" },
        { double.MaxValue, "17976931348623157" + new string('0', 292) },
    };

    [Theory]
    [MemberData(nameof(Numbers))]
    public void NumberFormat(double value, string text) => Assert.Equal(text, Number.Format(value));

    public static TheoryData<double, double, string?> FullLoads() => new()
    {
        { 0.3, 0.1, "3" },
        { 10, 0.1, "100" },
        { 400, 0.0125, "32000" },
        { 1, 3, "0" },
        { 1, Math.Pow(2, -24), "16777215" },
        { 1000, 1e5, "0" },
        { 0.5, 0.25, "2" },
        { 1e300, 1e-300, "1" + new string('0', 600) },
        { 0, 1, null },
        { 1, 0, null },
    };

    [Theory]
    [MemberData(nameof(FullLoads))]
    public void FullLoad(double capacity, double volume, string? load)
    {
        var item = new TypeRecord(1, "Item", Kind.High, true, capacity, 0);
        var charge = new TypeRecord(2, "Charge", Kind.Charge, false, 0, volume);
        Assert.Equal(load, Number.FullLoad(item, charge)?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("Damage Control II", false)]
    [InlineData("", true)]
    [InlineData("-x", true)]
    [InlineData("%x", true)]
    [InlineData("3x Damage Control", true)]
    [InlineData("Damage  Control", true)]
    [InlineData("Damage @Control", true)]
    [InlineData("Damage Control ", true)]
    public void NeedsQuotes(string name, bool quoted) => Assert.Equal(quoted, Canonical.NeedsQuotes(name));

    [Theory]
    [InlineData("Decayed Damage Control", new[] { "Gravid Damage Control" }, "Decayed")]
    [InlineData("A B", new[] { "A B C" }, "A B")]
    [InlineData("A B C", new[] { "A B" }, "A B C")]
    [InlineData("A", new string[0], "A")]
    public void ShortestUnique(string name, string[] others, string unique) =>
        Assert.Equal(unique, Names.ShortestUnique(name, others));

    [Theory]
    [InlineData("a", "b", -1)]
    [InlineData("ab", "a", 1)]
    [InlineData("a", "a", 0)]
    [InlineData("�", "\U00010000", -1)]
    [InlineData("\U00010000", "", 1)]
    [InlineData("퟿", "\U00010000", -1)]
    public void CompareCodePoints(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(Names.CompareCodePoints(a, b)));

    public static TheoryData<byte[], string> InvalidBinary() => new()
    {
        { [0x0A], "cut short" },
        { [0x0A, 0x80], "cut short" },
        { [0x0A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F], "cut short" },
        { [0x10, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01], "varint is too long" },
        { [0x80, 0x80, 0x80, 0x80, 0x80, 0x01], "bad key 34359738368" },
        { [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01], "bad key 18446744073709551615" },
        { [0x13, 0x1B, 0x14, 0x1C], "unmatched end group" },
        { [0x13, 0x1B, 0x24], "unmatched end group" },
        { [0x14], "unmatched end group" },
        { [0x0A, 0x02, 0x22, 0x00], "Fit has a field that is not in the schema" },
        { [0x0A, 0x04, 0x2A, 0x02, 0x60, 0x01], "Entry has a field that is not in the schema" },
        { [0x0A, 0x07, 0x2A, 0x05, 0x42, 0x03, 0, 0, 0], "packed doubles cut short" },
        { [0x0A, 0x05, 0x2A, 0x03, 0x1A, 0x01, 0xFF], "Entry.fit_name is not valid UTF-8" },
        { [0x0A, 0x06, 0x10, 0x80, 0x80, 0x80, 0x80, 0x08], "Fit.hull is out of range" },
        { [0x0A, 0x06, 0x08, 0x80, 0x80, 0x80, 0x80, 0x10], "Fit.version is out of range" },
        { [0x0A, 0x04, 0x2A, 0x02, 0x48, 0x04], "unknown State value 4" },
        { [0x0A, 0x04, 0x2A, 0x02, 0x50, 0x1C], "unknown Location value 28" },
    };

    [Theory]
    [MemberData(nameof(InvalidBinary))]
    public void LoadInvalidBinary(byte[] data, string message) =>
        Assert.Contains(message, Assert.Throws<EsfException>(() => Esf.Load(data, Catalog)).Message, StringComparison.Ordinal);

    [Theory]
    [InlineData(new byte[] { 0x11, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x15, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x18, 0x01 })]
    [InlineData(new byte[] { 0x22, 0x01, 0x00 })]
    [InlineData(new byte[] { 0x13, 0x18, 0x01, 0x21, 0, 0, 0, 0, 0, 0, 0, 0, 0x25, 0, 0, 0, 0, 0x2A, 0x00, 0x14 })]
    public void BinarySkipsUnknownDocumentFields(byte[] unknown)
    {
        var fit = Esf.SaveBinary([Fit()], Catalog);
        Assert.Equal(Rifter, Esf.Load([.. unknown, .. fit], Catalog).Single().Hull);
    }

    [Fact]
    public void BinaryUnpackedOverrides()
    {
        byte[] entry = [0x08, 0x80, 0x10, 0x38, 0x32, 0x41, 0, 0, 0, 0, 0, 0, 0xF0, 0x3F];
        byte[] fit = [0x08, 0x01, 0x10, 0xCB, 0x04, 0x2A, (byte)entry.Length, .. entry];
        var loaded = Esf.Load([0x0A, (byte)fit.Length, .. fit], Catalog).Single();
        Assert.Equal(new Dictionary<int, double> { [50] = 1 }, loaded.Entries.Single().Overrides);
    }
}
