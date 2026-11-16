namespace EveShipFit.EsfFormat.Tests;

public class EsiTest
{
    [Theory]
    [InlineData("HiSlot0", Location.High, 1u)]
    [InlineData("MedSlot7", Location.Mid, 8u)]
    [InlineData("LoSlot2", Location.Low, 3u)]
    [InlineData("RigSlot1", Location.Rig, 2u)]
    [InlineData("SubSystemSlot3", Location.Sub, 4u)]
    [InlineData("ServiceSlot5", Location.Svc, 6u)]
    [InlineData("FighterTube4", Location.Tube, 5u)]
    public void Slots(string flag, Location location, uint slot)
    {
        Assert.Equal(location, Esi.Location(flag));
        Assert.Equal(slot, Esi.Slot(flag));
    }

    [Fact]
    public void EveryHold()
    {
        string[] flags =
        [
            "Cargo", "DroneBay", "SpecializedAmmoHold", "BoosterBay", "SpecializedCommandCenterHold",
            "CorpseBay", "MobileDepotHold", "ExpeditionHold", "FleetHangar", "FrigateEscapeBay",
            "SpecializedFuelBay", "SpecializedGasHold", "SpecializedIceHold", "InfrastructureHangar",
            "ShipHangar", "SpecializedMineralHold", "SpecializedOreHold", "MoonMaterialBay",
            "SpecializedPlanetaryCommoditiesHold", "QuafeBay", "SubSystemBay",
        ];
        var holds = Enum.GetValues<Location>().Where(l => l >= Location.Cargo && l != Location.Tube);
        Assert.Equal(holds.Cast<Location?>(), flags.Select(Esi.Location));
        Assert.All(flags, f => Assert.Null(Esi.Slot(f)));
        Assert.Equal(Location.Bay, Esi.Location("FighterBay"));
    }

    [Theory]
    [InlineData("Hangar")]
    [InlineData("AutoFit")]
    [InlineData("HiSlot8")]
    [InlineData("FighterTube5")]
    [InlineData("hislot0")]
    [InlineData("HiSlot01")]
    [InlineData("HiSlot0\n")]
    [InlineData("toString")]
    [InlineData("")]
    public void Unknown(string flag)
    {
        Assert.Null(Esi.Location(flag));
        Assert.Null(Esi.Slot(flag));
    }
}
