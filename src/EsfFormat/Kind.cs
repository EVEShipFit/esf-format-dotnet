namespace EveShipFit.EsfFormat;

/// <summary>What a type is, which decides where it goes in a fit (esf/1 spec §5).</summary>
/// <remarks>
/// Check the group rules first (<see cref="Mode"/>, <see cref="Container"/>, <see cref="Booster"/>), then the
/// category rules. <see cref="Sde.Kind"/> works this out from the SDE for you.
/// </remarks>
public enum Kind
{
    /// <summary>A ship or a structure: SDE category <c>Ship</c> (6) or <c>Structure</c> (65).</summary>
    Hull,

    /// <summary>A container that can carry another fit, like a Small Secure Container.</summary>
    /// <remarks>
    /// SDE group <c>Cargo Container</c> (12), <c>Secure Cargo Container</c> (340),
    /// <c>Audit Log Secure Container</c> (448) or <c>Freight Container</c> (649).
    /// </remarks>
    Container,

    /// <summary>A tactical mode, like the Hecate's Sharpshooter Mode: SDE group <c>Ship Modifiers</c> (1306).</summary>
    Mode,

    /// <summary>A subsystem, like on a Tengu: SDE category <c>Subsystem</c> (32).</summary>
    Sub,

    /// <summary>
    /// A high slot module, like a gun: SDE category <c>Module</c> (7) or <c>Structure Module</c> (66), with effect
    /// <c>hiPower</c> (12).
    /// </summary>
    High,

    /// <summary>
    /// A mid slot module, like a shield extender: category <c>Module</c> or <c>Structure Module</c>, with effect
    /// <c>medPower</c> (13).
    /// </summary>
    Mid,

    /// <summary>
    /// A low slot module, like a damage control: category <c>Module</c> or <c>Structure Module</c>, with effect
    /// <c>loPower</c> (11).
    /// </summary>
    Low,

    /// <summary>A rig: category <c>Module</c> or <c>Structure Module</c>, with effect <c>rigSlot</c> (2663).</summary>
    Rig,

    /// <summary>
    /// A structure service module: category <c>Module</c> or <c>Structure Module</c>, with effect
    /// <c>serviceSlot</c> (6306).
    /// </summary>
    Svc,

    /// <summary>A drone: SDE category <c>Drone</c> (18).</summary>
    Drone,

    /// <summary>A fighter: SDE category <c>Fighter</c> (87).</summary>
    Fighter,

    /// <summary>Ammunition, a script or anything else loaded into a module: SDE category <c>Charge</c> (8).</summary>
    Charge,

    /// <summary>An implant: SDE category <c>Implant</c> (20), except the boosters in it.</summary>
    Implant,

    /// <summary>A booster, also called a drug: SDE group <c>Booster</c> (303), inside category <c>Implant</c>.</summary>
    Booster,

    /// <summary>Anything else, like ore or a module without a slot. It goes in the cargo.</summary>
    Other,
}

/// <summary>Helpers for <see cref="Kind"/>.</summary>
public static class KindExtensions
{
    /// <summary>
    /// Whether a type of this kind can be the <see cref="EsfFit.Hull"/> of a fit: true for
    /// <see cref="Kind.Hull"/> and <see cref="Kind.Container"/> only.
    /// </summary>
    public static bool CanBeHull(this Kind kind) => kind is Kind.Hull or Kind.Container;
}
