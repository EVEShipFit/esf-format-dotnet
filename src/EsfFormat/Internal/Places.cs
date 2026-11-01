namespace EveShipFit.EsfFormat.Internal;

/// <summary>Where an item is: a location, or where its kind of type goes by default (§5).</summary>
internal enum Place
{
    High = Location.High,
    Mid = Location.Mid,
    Low = Location.Low,
    Rig = Location.Rig,
    Sub = Location.Sub,
    Svc = Location.Svc,
    Cargo = Location.Cargo,
    Bay = Location.Bay,
    Ammo = Location.Ammo,
    Booster = Location.Booster,
    Command = Location.Command,
    Corpse = Location.Corpse,
    Depot = Location.Depot,
    Expedition = Location.Expedition,
    Fleet = Location.Fleet,
    Frigate = Location.Frigate,
    Fuel = Location.Fuel,
    Gas = Location.Gas,
    Ice = Location.Ice,
    Infrastructure = Location.Infrastructure,
    Maintenance = Location.Maintenance,
    Mineral = Location.Mineral,
    Mining = Location.Mining,
    Moon = Location.Moon,
    Planetary = Location.Planetary,
    Quafe = Location.Quafe,
    Subsystem = Location.Subsystem,
    Drones,
    Fighters,
    Implants,
    Boosters,
}

internal static class Places
{
    public static readonly Place[] Racks = [Place.Sub, Place.High, Place.Mid, Place.Low, Place.Rig, Place.Svc];

    /// <summary>The groups of a canonical fit, in order (§8).</summary>
    public static readonly Place[] Groups =
    [
        .. Racks,
        Place.Drones,
        Place.Fighters,
        Place.Cargo,
        .. Enumerable.Range((int)Place.Ammo, Place.Subsystem - Place.Ammo + 1).Select(p => (Place)p),
        Place.Implants,
        Place.Boosters,
    ];

    private static readonly Dictionary<Location, string> LocationNames =
        Enum.GetValues<Location>().ToDictionary(l => l, l => l.ToString().ToLowerInvariant());

    private static readonly Dictionary<State, string> StateNames =
        Enum.GetValues<State>().ToDictionary(s => s, s => s.ToString().ToLowerInvariant());

    private static readonly Dictionary<string, Location> Locations = LocationNames.ToDictionary(p => p.Value, p => p.Key);

    private static readonly Dictionary<string, State> States = StateNames.ToDictionary(p => p.Value, p => p.Key);

    public static bool IsRack(this Place place) => place <= Place.Svc;

    public static bool IsStored(this Place place) => place is >= Place.Cargo and <= Place.Subsystem;

    public static bool IsPlugged(this Place place) => place is Place.Implants or Place.Boosters;

    public static Place ToPlace(this Location location) => (Place)location;

    public static Location ToLocation(this Place place) => (Location)place;

    /// <summary>Where a kind of type goes in a ship, if it has a place of its own.</summary>
    public static Place? Default(Kind kind) => kind switch
    {
        Kind.Sub => Place.Sub,
        Kind.High => Place.High,
        Kind.Mid => Place.Mid,
        Kind.Low => Place.Low,
        Kind.Rig => Place.Rig,
        Kind.Svc => Place.Svc,
        Kind.Drone => Place.Drones,
        Kind.Fighter => Place.Fighters,
        Kind.Implant => Place.Implants,
        Kind.Booster => Place.Boosters,
        _ => null,
    };

    public static Place Default(Kind kind, bool cargoOnly) => cargoOnly ? Place.Cargo : Default(kind) ?? Place.Cargo;

    public static string Name(this Location location) => LocationNames[location];

    public static string Name(this State state) => StateNames[state];

    public static bool TryParseLocation(string name, out Location location) => Locations.TryGetValue(name, out location);

    public static State ParseState(string name) => States[name];
}
