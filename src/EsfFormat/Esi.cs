namespace EveShipFit.EsfFormat;

/// <summary>Helpers to turn an ESI <c>location_flag</c> into an <see cref="Entry.Location"/> and <see cref="Entry.Slot"/>.</summary>
/// <remarks>
/// <para>
/// ESI gives a <c>location_flag</c> for each item in a ship, in the assets and fittings endpoints.
/// Use both helpers on the same flag:
/// <code>new Entry(typeId) { Location = Esi.Location(flag) ?? Location.Cargo, Slot = Esi.Slot(flag) }</code>
/// </para>
/// <para>
/// Only the flags of things inside a ship are known. For any other flag, like <c>Hangar</c>, both return
/// <see langword="null"/>. The example above puts those items in the cargo hold.
/// </para>
/// </remarks>
public static class Esi
{
    private static readonly (string Prefix, EsfFormat.Location Location, int Size)[] Racks =
    [
        ("HiSlot", EsfFormat.Location.High, 8),
        ("MedSlot", EsfFormat.Location.Mid, 8),
        ("LoSlot", EsfFormat.Location.Low, 8),
        ("RigSlot", EsfFormat.Location.Rig, 8),
        ("SubSystemSlot", EsfFormat.Location.Sub, 8),
        ("ServiceSlot", EsfFormat.Location.Svc, 8),
        ("FighterTube", EsfFormat.Location.Tube, 5),
    ];

    private static readonly Dictionary<string, (EsfFormat.Location Location, uint Slot)> Slots = Racks
        .SelectMany(r => Enumerable.Range(0, r.Size).Select(i => (Flag: r.Prefix + i, r.Location, Slot: (uint)i + 1)))
        .ToDictionary(s => s.Flag, s => (s.Location, s.Slot), StringComparer.Ordinal);

    private static readonly Dictionary<string, EsfFormat.Location> Holds = new(StringComparer.Ordinal)
    {
        ["Cargo"] = EsfFormat.Location.Cargo,
        ["DroneBay"] = EsfFormat.Location.Bay,
        ["FighterBay"] = EsfFormat.Location.Bay,
        ["SpecializedAmmoHold"] = EsfFormat.Location.Ammo,
        ["BoosterBay"] = EsfFormat.Location.Booster,
        ["SpecializedCommandCenterHold"] = EsfFormat.Location.Command,
        ["CorpseBay"] = EsfFormat.Location.Corpse,
        ["MobileDepotHold"] = EsfFormat.Location.Depot,
        ["ExpeditionHold"] = EsfFormat.Location.Expedition,
        ["FleetHangar"] = EsfFormat.Location.Fleet,
        ["FrigateEscapeBay"] = EsfFormat.Location.Frigate,
        ["SpecializedFuelBay"] = EsfFormat.Location.Fuel,
        ["SpecializedGasHold"] = EsfFormat.Location.Gas,
        ["SpecializedIceHold"] = EsfFormat.Location.Ice,
        ["InfrastructureHangar"] = EsfFormat.Location.Infrastructure,
        ["ShipHangar"] = EsfFormat.Location.Maintenance,
        ["SpecializedMineralHold"] = EsfFormat.Location.Mineral,
        ["SpecializedOreHold"] = EsfFormat.Location.Mining,
        ["MoonMaterialBay"] = EsfFormat.Location.Moon,
        ["SpecializedPlanetaryCommoditiesHold"] = EsfFormat.Location.Planetary,
        ["QuafeBay"] = EsfFormat.Location.Quafe,
        ["SubSystemBay"] = EsfFormat.Location.Subsystem,
    };

    /// <summary>Gives the <see cref="EsfFormat.Location"/> for an ESI <c>location_flag</c>.</summary>
    /// <remarks>
    /// A slot flag gives its rack: <c>HiSlot2</c> gives <see cref="EsfFormat.Location.High"/>, and <c>FighterTube0</c>
    /// gives <see cref="EsfFormat.Location.Tube"/>. <c>DroneBay</c> and <c>FighterBay</c> give
    /// <see cref="EsfFormat.Location.Bay"/>. In a fitting from ESI, <c>DroneBay</c> also holds the drones that are out
    /// in space. Leave <see cref="Entry.Location"/> <see langword="null"/> for those.
    /// </remarks>
    /// <para>Returns <see langword="null"/> for a flag that is not inside a ship, like <c>Hangar</c>.</para>
    public static Location? Location(string flag)
    {
        ArgumentNullException.ThrowIfNull(flag);
        if (Slots.TryGetValue(flag, out var slot))
        {
            return slot.Location;
        }

        return Holds.TryGetValue(flag, out var hold) ? hold : null;
    }

    /// <summary>Gives the <see cref="Entry.Slot"/> for an ESI <c>location_flag</c>, counting from 1.</summary>
    /// <remarks>
    /// ESI counts slots from 0, so <c>HiSlot2</c> is slot 3, and <c>FighterTube0</c> is tube 1.
    /// Returns <see langword="null"/> for a flag that is not a slot, like <c>Cargo</c> or <c>Hangar</c>.
    /// </remarks>
    public static uint? Slot(string flag)
    {
        ArgumentNullException.ThrowIfNull(flag);
        return Slots.TryGetValue(flag, out var slot) ? slot.Slot : null;
    }
}
