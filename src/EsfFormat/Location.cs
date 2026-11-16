namespace EveShipFit.EsfFormat;

/// <summary>Where an entry goes, when not where its type goes by default (esf/1 spec §5).</summary>
/// <remarks>
/// <para>
/// An item goes where its <see cref="Kind"/> says, so most entries leave this <see langword="null"/>.
/// Set it to store an item somewhere else, like a spare Damage Control II in <see cref="Cargo"/>.
/// So a drone with no location is out in space, and a fighter is in a launch tube.
/// Set <see cref="Bay"/> to keep either one in its bay.
/// </para>
/// <para>
/// The racks, <see cref="High"/> to <see cref="Svc"/>, are for empty slots, and for an item put in a given
/// <see cref="Entry.Slot"/>. Otherwise, an item never names its rack.
/// The holds, <see cref="Ammo"/> to <see cref="Subsystem"/>, only make sense on a hull that has that hold, but the
/// library does not check that.
/// </para>
/// </remarks>
public enum Location
{
    /// <summary>The high slot rack.</summary>
    High = 1,

    /// <summary>The mid slot rack.</summary>
    Mid = 2,

    /// <summary>The low slot rack.</summary>
    Low = 3,

    /// <summary>The rig rack.</summary>
    Rig = 4,

    /// <summary>The subsystem rack. Not the same as <see cref="Subsystem"/>, which is a hold.</summary>
    Sub = 5,

    /// <summary>The structure service rack.</summary>
    Svc = 6,

    /// <summary>The cargo hold.</summary>
    Cargo = 7,

    /// <summary>The drone bay or fighter bay, for drones and fighters that are not out in space or in a tube.</summary>
    Bay = 8,

    /// <summary>The Ammo Hold.</summary>
    Ammo = 9,

    /// <summary>The Booster Hold.</summary>
    Booster = 10,

    /// <summary>The Command Center Hold.</summary>
    Command = 11,

    /// <summary>The Corpse Hold.</summary>
    Corpse = 12,

    /// <summary>The Mobile Depot Hold.</summary>
    Depot = 13,

    /// <summary>The Expedition Hold.</summary>
    Expedition = 14,

    /// <summary>The Fleet Hangar.</summary>
    Fleet = 15,

    /// <summary>The Frigate Escape Bay.</summary>
    Frigate = 16,

    /// <summary>The Fuel Bay.</summary>
    Fuel = 17,

    /// <summary>The Gas Hold.</summary>
    Gas = 18,

    /// <summary>The Ice Hold.</summary>
    Ice = 19,

    /// <summary>The Infrastructure Hold.</summary>
    Infrastructure = 20,

    /// <summary>The Ship Maintenance Bay.</summary>
    Maintenance = 21,

    /// <summary>The Mineral Hold.</summary>
    Mineral = 22,

    /// <summary>The Mining Hold.</summary>
    Mining = 23,

    /// <summary>The Moon Material Output Bay.</summary>
    Moon = 24,

    /// <summary>The Planetary Commodities Hold.</summary>
    Planetary = 25,

    /// <summary>The Quafe Hold.</summary>
    Quafe = 26,

    /// <summary>The Subsystem Hold, for spare subsystems. Not the same as <see cref="Sub"/>, which is the rack.</summary>
    Subsystem = 27,
}
