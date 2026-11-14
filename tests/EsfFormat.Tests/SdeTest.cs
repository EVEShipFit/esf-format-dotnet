namespace EveShipFit.EsfFormat.Tests;

public class SdeTest
{
    private sealed record Named(int Id, string Name, bool Published);

    public static TheoryData<string, int, int, int[], Kind> Kinds() => new()
    {
        { "Rifter", 6, 25, [5779], Kind.Hull },
        { "Astrahus", 65, 1657, [6565], Kind.Hull },
        { "Small Secure Container", 2, 340, [649], Kind.Container },
        { "Hecate Sharpshooter Mode", 7, 1306, [6010], Kind.Mode },
        { "Tengu Defensive - Covert Reconfiguration", 32, 954, [896], Kind.Sub },
        { "200mm AutoCannon II", 7, 55, [16, 12, 34], Kind.High },
        { "Medium Shield Extender II", 7, 38, [13, 16], Kind.Mid },
        { "Damage Control II", 7, 60, [11, 16], Kind.Low },
        { "Standup Ballistic Control System I", 66, 1429, [11, 16], Kind.Low },
        { "Small Core Defense Field Extender I", 7, 774, [446, 2663], Kind.Rig },
        { "Standup Cloning Center I", 66, 1321, [16, 6306], Kind.Svc },
        { "module without a slot", 7, 1, [16], Kind.Other },
        { "Hobgoblin II", 18, 100, [10], Kind.Drone },
        { "Templar II", 87, 1652, [6431], Kind.Fighter },
        { "Republic Fleet EMP S", 8, 83, [596], Kind.Charge },
        { "Genolution Core Augmentation CA-1", 20, 1730, [302], Kind.Implant },
        { "Strong Blue Pill Booster", 20, 303, [2737], Kind.Booster },
        { "Veldspar", 25, 462, [], Kind.Other },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void KindOfAType(string name, int categoryId, int groupId, int[] effectIds, Kind expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, Sde.Kind(categoryId, groupId, effectIds));
    }

    [Fact]
    public void ActiveNeedsAnActiveOrTargetEffect()
    {
        Assert.False(Sde.Active(new Dictionary<int, int>()));
        Assert.False(Sde.Active(new Dictionary<int, int> { [16] = 1, [2302] = 0 }));
        Assert.True(Sde.Active(new Dictionary<int, int> { [16] = 1, [4] = 1 }));
        Assert.True(Sde.Active(new Dictionary<int, int> { [16] = 1, [3174] = 2 }));
    }

    [Fact]
    public void SquadronSize()
    {
        Assert.Equal(6, Sde.SquadronSize(new Dictionary<int, double> { [9] = 100.0, [2215] = 6.0 }));
        Assert.Null(Sde.SquadronSize(new Dictionary<int, double> { [9] = 240.0 }));
    }

    [Fact]
    public void ModeHullMatchesTheLongestShipName()
    {
        var ships = new Dictionary<string, int> { ["hecate"] = 35683, ["hecate navy"] = 1 };
        Assert.Equal(35683, Sde.ModeHull("Hecate Sharpshooter Mode", ships));
        Assert.Equal(1, Sde.ModeHull("Hecate Navy Defense Mode", ships));
        Assert.Null(Sde.ModeHull("Hecate", ships));
        Assert.Null(Sde.ModeHull("Rifter Defense Mode", ships));
    }

    [Fact]
    public void NameIndexFoldsNamesAndPicksPublishedThenLowestId()
    {
        Named[] entries =
        [
            new(50097, "Avatar", false),
            new(11567, "Avatar", true),
            new(30, "Twin", false),
            new(20, "Twin", false),
            new(587, "Rifter", true),
        ];
        var index = Sde.NameIndex(entries, e => e.Id, e => e.Name, e => e.Published);
        Assert.Equal(
            new Dictionary<string, int> { ["avatar"] = 11567, ["twin"] = 20, ["rifter"] = 587 },
            index);
    }
}
