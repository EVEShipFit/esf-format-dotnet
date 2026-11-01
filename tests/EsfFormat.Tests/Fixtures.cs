namespace EveShipFit.EsfFormat.Tests;

internal static class Fixtures
{
    public const int Rifter = 587;
    public const int Autocannon = 2889;
    public const int Emp = 21898;
    public const int ShieldExtender = 3831;
    public const int Scrambler = 448;
    public const int DamageControl = 2048;
    public const int Hobgoblin = 2456;
    public const int DecayedDamageControlMutaplasmid = 52224;
    public const int Avatar = 11567;
    public const int AvatarUnpublished = 50097;
    public const int SmallSecureContainer = 3467;

    /// <summary>The esf-format checkout with the test cases.</summary>
    public static readonly string Root = FindRoot();

    public static readonly JsonCatalog Catalog = new(Path.Combine(Root, "catalog.json"));

    public static EsfFit Fit(params Entry[] entries) => new() { Hull = Rifter, Entries = entries };

    public static EsfFit Named(string name, params Entry[] entries) => Fit(entries) with { Name = name };

    private static string FindRoot()
    {
        if (Environment.GetEnvironmentVariable("ESF_CATALOG") is { Length: > 0 } catalog)
        {
            return Path.GetDirectoryName(Path.GetFullPath(catalog))!;
        }
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var tests = Path.Combine(dir.FullName, "esf-format", "tests");
            if (File.Exists(Path.Combine(tests, "catalog.json")))
            {
                return tests;
            }
        }
        throw new InvalidOperationException("no esf-format checkout found; set ESF_CATALOG");
    }
}
