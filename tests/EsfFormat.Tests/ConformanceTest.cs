namespace EveShipFit.EsfFormat.Tests;

/// <summary>The esf-format test cases, the way its runner checks them.</summary>
public class ConformanceTest
{
    public static TheoryData<string> Cases()
    {
        var cases = new TheoryData<string>();
        foreach (var kind in new[] { "valid", "invalid" })
        {
            foreach (var path in Directory.GetDirectories(Path.Combine(Fixtures.Root, kind)).Order(StringComparer.Ordinal))
            {
                cases.Add($"{kind}/{Path.GetFileName(path)}");
            }
        }
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case(string name)
    {
        var path = Path.Combine(Fixtures.Root, name);
        var catalog = Fixtures.Catalog;
        var input = File.Exists(Path.Combine(path, "input.esf"))
            ? File.ReadAllBytes(Path.Combine(path, "input.esf"))
            : Esf.FromBase64Url(File.ReadAllText(Path.Combine(path, "input.b64")));
        if (name.StartsWith("invalid/", StringComparison.Ordinal))
        {
            Assert.Throws<EsfException>(() => Esf.Load(input, catalog));
            return;
        }

        var canonical = File.ReadAllText(Path.Combine(path, "canonical.esf"));
        var binary = Esf.FromBase64Url(File.ReadAllText(Path.Combine(path, "canonical.b64")));
        var fits = Esf.Load(input, catalog);
        Assert.Equal(canonical, Esf.Save(fits, catalog));
        Assert.Equal(canonical, Esf.Save(Esf.Load(canonical, catalog), catalog));
        Assert.Equal(Convert.ToHexString(binary), Convert.ToHexString(Esf.SaveBinary(fits, catalog)));
        Assert.Equal(canonical, Esf.Save(Esf.Load(binary, catalog), catalog));
    }
}
