namespace EveShipFit.EsfFormat;

/// <summary>Whether an item is offline, online or running, when that is not its default (esf/1 spec §6.4).</summary>
/// <remarks>
/// By default, a fitted or deployed item runs when its type is <see cref="TypeRecord.Active"/>, and is
/// online otherwise. An implant or booster is online by default, and only takes <see cref="Off"/>.
/// A stored item, in cargo, a bay or a hold, has no state at all.
/// </remarks>
public enum State
{
    /// <summary>Offline: fitted, but not using powergrid or CPU.</summary>
    Off = 1,

    /// <summary>Online, but not running, like a gun that is not shooting.</summary>
    On = 2,

    /// <summary>Overloaded, which also means running.</summary>
    Heat = 3,
}
