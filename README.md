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
`EsfException` when a rule is broken. It says where: `LineNumber` for text,
or `FitNumber` and `EntryNumber` (counting from 1) for fits.

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
| `Overrides` | Attribute values by attribute ID. | |
| `State` | `State.Off`, `On` or `Heat`. | Active if the type can be, else on. |
| `Location` | `Cargo`, `Bay` or another hold; a rack only for an empty slot. | Where the type goes by default. |

`EsfFit` and `Entry` are records, and compare by value.

## The Catalog

The library holds no game data. You give it an `ICatalog`: an object that
looks up types and attributes in your copy of the SDE.

```csharp
public sealed class MyCatalog : ICatalog
{
    public TypeRecord? TypeByName(string folded) => ...;
    public TypeRecord? TypeById(int typeId) => ...;
    public AttributeRecord? AttributeByName(string folded) => ...;
    public AttributeRecord? AttributeById(int attributeId) => ...;

    // Optional. Without them, no hull has tactical modes and no type has mutaplasmids.
    public IReadOnlyList<TypeRecord> Modes(int hullId) => ...;
    public IReadOnlyList<MutaplasmidRecord> Mutaplasmids(int typeId) => ...;
}
```

Names are looked up folded: index them with `Esf.Fold(name)`. Where
names are shared, published wins, then the lowest ID (§4.2).
[tests/EsfFormat.Tests/JsonCatalog.cs](https://github.com/EVEShipFit/esf-format-dotnet/blob/main/tests/EsfFormat.Tests/JsonCatalog.cs)
is a complete example.

## Tests

The test cases live in [esf-format](https://github.com/EVEShipFit/esf-format):

```
git clone --branch v1.0 https://github.com/EVEShipFit/esf-format
dotnet build tests/EsfFormat.Program -c Release
uv run --project esf-format/tools esf-test -- dotnet tests/EsfFormat.Program/bin/Release/net10.0/esf-program.dll
dotnet format --verify-no-changes
dotnet test
```
