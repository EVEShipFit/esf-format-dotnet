using System.Collections.ObjectModel;

namespace EveShipFit.EsfFormat;

/// <summary>One line of a fit: an item, an empty slot, or another fit carried by this one.</summary>
/// <remarks>
/// Only <see cref="Type"/> is needed; every other property is <see langword="null"/> or empty when it is the default.
/// Entries can be in any order, except within a rack: there, the order is the slot order,
/// unless an entry sets <see cref="Slot"/>.
/// </remarks>
/// <param name="Type">
/// The type ID of the item, like 2889 for a 200mm AutoCannon II.
/// <see langword="null"/> for an empty slot. An empty slot must set <see cref="Location"/> to its rack,
/// like <see cref="EsfFormat.Location.High"/>.
/// </param>
public sealed record Entry(int? Type)
{
    /// <summary>How many there are. <see langword="null"/> means one.</summary>
    /// <remarks>
    /// What a count means depends on where the item goes (esf/1 spec §6.2). For a fitted
    /// module or a deployed drone, it is that many of the same line: 3 guns take 3 slots.
    /// For a stored item, it is the size of one stack. For a fighter, it is how many are
    /// in the squadron; <see langword="null"/> means a full squadron. A plugged-in implant or booster takes
    /// no count.
    /// </remarks>
    public uint? Count { get; init; }

    /// <summary>The name of another fit in the same list, which this entry carries.</summary>
    /// <remarks>
    /// Only for a ship, structure or container. The other fit must have this entry's
    /// <see cref="Type"/> as its <see cref="EsfFit.Hull"/> and this as its <see cref="EsfFit.Name"/>, ignoring case.
    /// For example, a Heron with fit name "Scout" and <see cref="EsfFormat.Location.Frigate"/> carries that fit in the
    /// Frigate Escape Bay. <see langword="null"/> means an unfitted ship or an empty container.
    /// </remarks>
    public string? FitName { get; init; }

    /// <summary>
    /// The type ID of the charge loaded into this item, like ammunition or a script. <see langword="null"/> for none.
    /// </summary>
    public int? Charge { get; init; }

    /// <summary>How many of <see cref="Charge"/> are loaded into each item.</summary>
    /// <remarks>
    /// <see langword="null"/> means a full load: the item's capacity divided by the charge's volume, rounded
    /// down. Only set together with <see cref="Charge"/>.
    /// </remarks>
    public uint? ChargeCount { get; init; }

    /// <summary>The type ID of the mutaplasmid applied to this item, which makes it abyssal.</summary>
    /// <remarks>
    /// <see cref="Type"/> is then the base type the mutaplasmid applies to, and <see cref="Overrides"/> holds the
    /// rolled values. <see langword="null"/> for a normal item.
    /// </remarks>
    public int? Mutaplasmid { get; init; }

    /// <summary>
    /// Attribute values that replace the type's own, by attribute ID, like 20 (speedFactor) to 524.0.
    /// </summary>
    /// <remarks>
    /// Each value is the new base value, not a change on top of it. Attributes not listed
    /// keep the type's value. Empty for none.
    /// </remarks>
    public IReadOnlyDictionary<int, double> Overrides { get; init; } = ReadOnlyDictionary<int, double>.Empty;

    /// <summary>
    /// Whether the item is offline, online or running. <see langword="null"/> means its default;
    /// see <see cref="EsfFormat.State"/>.
    /// </summary>
    public State? State { get; init; }

    /// <summary>
    /// Where the item goes. <see langword="null"/> means where its type goes by default;
    /// see <see cref="EsfFormat.Location"/>.
    /// </summary>
    public Location? Location { get; init; }

    /// <summary>
    /// The slot in the <see cref="Location"/> rack, counting from 1, like 3 for the third high slot or the third
    /// launch tube.
    /// </summary>
    /// <remarks>
    /// Set <see cref="Location"/> to the rack too, and the item must go in that rack.
    /// <see langword="null"/> means the next free slot. A slot cannot be combined with <see cref="Count"/>,
    /// except for a fighter in a <see cref="EsfFormat.Location.Tube"/>, where the count is the size of the squadron.
    /// Each slot in a rack can be pinned once.
    /// Loading a fit never sets this: it gives the rack in slot order, with empty slots in between.
    /// </remarks>
    public uint? Slot { get; init; }

    public bool Equals(Entry? other) =>
        other is not null
        && Type == other.Type
        && Count == other.Count
        && FitName == other.FitName
        && Charge == other.Charge
        && ChargeCount == other.ChargeCount
        && Mutaplasmid == other.Mutaplasmid
        && State == other.State
        && Location == other.Location
        && Slot == other.Slot
        && Overrides.Count == other.Overrides.Count
        && Overrides.All(o => other.Overrides.TryGetValue(o.Key, out var value) && value.Equals(o.Value));

    public override int GetHashCode() =>
        HashCode.Combine(HashCode.Combine(Type, Count, FitName, Charge, ChargeCount, Mutaplasmid, State, Location), Slot);
}
