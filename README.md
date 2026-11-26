# esf-format

[![NuGet](https://img.shields.io/nuget/v/EveShipFit.EsfFormat.svg)](https://www.nuget.org/packages/EveShipFit.EsfFormat)
[![CI](https://github.com/EVEShipFit/esf-format-dotnet/actions/workflows/test.yml/badge.svg)](https://github.com/EVEShipFit/esf-format-dotnet/actions/workflows/test.yml)

[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white)](https://discord.gg/S5V5BkvNf7)

Read and write [esf/1](https://github.com/EVEShipFit/esf-format), the
plain-text format for EVE Online ship fits. Text, canonical and binary forms.
No dependencies. .NET 8 or later.

```
dotnet add package EveShipFit.EsfFormat
```

## Usage

```csharp
using EveShipFit.EsfFormat;

var fit = new EsfFit
{
    Hull = 587, // Rifter
    Name = "Shield Buffer",
    Entries =
    [
        new Entry(2889) { Count = 3, Charge = 21898 }, // 3 guns, a full load of ammo each
        new Entry(null) { Location = Location.High }, // an empty high slot
        new Entry(448) { State = State.Off }, // an offline module
        new Entry(2048), // goes in its own rack
        new Entry(2456) { Count = 5 }, // 5 drones
        new Entry(21898) { Count = 1000, Location = Location.Cargo },
    ],
};

string text = Esf.Save([fit], catalog); // canonical text
byte[] data = Esf.SaveBinary([fit], catalog); // binary
IReadOnlyList<EsfFit> fits = Esf.Load(data, catalog); // text or binary

string url = Esf.ToBase64Url(data); // the binary form in a URL
data = Esf.FromBase64Url(url);
```

`Save()` and `SaveBinary()` take entries in any order, and check every rule
of the spec. `Load()` returns fits in canonical form. All three throw
`EsfException` when the data or a fit is not valid. It says where: `LineNumber` for text,
or `FitNumber` and `EntryNumber` (counting from 1) for fits.

Catch a subclass to handle one kind of error:

| Exception | When |
|---|---|
| `EsfFormatException` | The data is not a well-formed document: broken text, binary or base64url, or a fit name that cannot be written. |
| `EsfCatalogException` | The catalog does not know a name or ID, or a name matches more than one type. |
| `EsfRuleException` | The fit breaks a rule, like a module in the wrong rack. |

## Other data in a document

Other formats, like a fleet, can share a document with fits. `Load()` skips
that data. `LoadDocument()` hands it back:

```csharp
EsfDocument document = Esf.LoadDocument(data, catalog);
IReadOnlyList<EsfFit> fits = document.Fits; // what Load() returns
foreach (var extra in document.Extra) // in document order
{
    switch (extra)
    {
        case TextExtra text: // a text block like %fleet/1
            Console.WriteLine($"{text.Name}/{text.Version}: {text.Text}");
            break;
        case BinaryExtra binary: // a binary Document field other than the fits
            Console.WriteLine($"field {binary.Field}: {binary.Bytes.Length} bytes");
            break;
    }
}
```

`TextExtra.Text` is the block after its header line, exactly as written.
`BinaryExtra` is only for fields that hold bytes; other fields are skipped.

To save other data with fits, append it yourself. In text, add your block after
the output of `Save()`. In binary, add `Esf.BinaryField()` after the output of
`SaveBinary()`:

```csharp
byte[] data = [.. Esf.SaveBinary(fits, catalog), .. Esf.BinaryField(2, fleetBytes)];
```

## Fits

An `EsfFit` is one fit: `Hull`, `Name`, tactical `Mode` and its `Entries`.
`Hull` is `null` for a fit without a ship. A document can hold more than one
fit.

Each `Entry` is one line of the fit. `null` means the default:

| property | what it is | `null` means |
| --- | --- | --- |
| `Type` | The type ID. | An empty slot. |
| `Count` | How many. | One; for fighters, a full squadron. |
| `FitName` | The name of another fit in the list, which this ship or container carries. | Carries no fit. |
| `Charge` | The charge loaded. | |
| `ChargeCount` | How many charges. | A full load. |
| `Mutaplasmid` | The mutaplasmid applied. | |
| `Overrides` | Dogma attribute values by attribute ID. | |
| `State` | `State.Off`, `On` or `Heat`. | Active if the type can be, else on. |
| `Location` | `Cargo`, `Bay` or another hold; a rack for an empty slot, or with `Slot`. | Where the type goes by default. |
| `Slot` | The slot in the `Location` rack, counting from 1. Not with `Count`, except for a fighter in a `Tube`; the item must go in that rack, and each slot is pinned once. `Load()` never sets it: it returns the rack in slot order, with empty slots between. | The next free slot. |

`EsfFit` and `Entry` are records, and compare by value.

## The Catalog

The library holds no game data. You give it an `ICatalog`: an object that
looks up types and dogma attributes in your copy of the SDE.

```csharp
public sealed class MyCatalog : ICatalog
{
    public TypeRecord? TypeByName(string foldedName) => ...;
    public TypeRecord? TypeById(int typeId) => ...;
    public AttributeRecord? AttributeByName(string foldedName) => ...;
    public AttributeRecord? AttributeById(int attributeId) => ...;

    // Optional. Without them, no hull has tactical modes and no type has mutaplasmids.
    public IReadOnlyList<TypeRecord> Modes(int hullId) => ...;
    public IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => ...;
}
```

Each method and property says what to return and where in the SDE it comes
from. `Sde` has helpers for the parts that take more than a lookup:

```csharp
var typeNames = Sde.NameIndex(types, t => t.Id, t => t.Name.En, t => t.Published);

// effects: effectID -> effectCategoryID; attributes: dogma attributeID -> value

new TypeRecord(
    t.Id,
    t.Name.En,
    Sde.Kind(group.CategoryId, t.GroupId, effects.Keys),
    Sde.Active(effects),
    t.Capacity ?? 0,
    t.Volume ?? 0,
    Sde.SquadronSize(attributes));
```

[examples/SdeJsonlCatalog.cs](https://github.com/EVEShipFit/esf-format-dotnet/blob/main/examples/SdeJsonlCatalog.cs)
is a complete example on the official SDE in JSONL: copy it into your project.

## ESI

ESI gives each item in a ship a `location_flag`. `Esi` turns it into a
location and a slot:

```csharp
new Entry(typeId) { Location = Esi.Location(flag) ?? Location.Cargo, Slot = Esi.Slot(flag) }
```

`HiSlot2` gives `High` and slot 3, and `ShipHangar` gives `Maintenance`.
A flag that is not inside a ship, like `Hangar`, gives `null`; the example
puts those items in the cargo hold.

Only a ship, structure or container can be a fit's hull. Check
`catalog.TypeById(typeId)?.Kind.CanBeHull() == true` before you use a type as one.

## Tests

The test cases live in [esf-format](https://github.com/EVEShipFit/esf-format):

```
git clone --branch load-document https://github.com/EVEShipFit/esf-format
dotnet build tests/EsfFormat.Program -c Release
uv run --project esf-format/tools esf-test -- dotnet tests/EsfFormat.Program/bin/Release/net10.0/esf-program.dll
dotnet format --verify-no-changes
dotnet test
```
