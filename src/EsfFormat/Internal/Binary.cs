using System.Buffers.Binary;
using System.Text;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>The binary form (SPEC §10).</summary>
internal static class Binary
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Encode(IEnumerable<EsfFit> fits)
    {
        var document = new WireWriter();
        var fitWriter = new WireWriter();
        var entryWriter = new WireWriter();
        foreach (var fit in fits)
        {
            fitWriter.Clear();
            fitWriter.UInt(1, 1);
            if (fit.Hull is { } hull)
            {
                fitWriter.UInt(2, (ulong)hull);
            }
            if (!string.IsNullOrEmpty(fit.Name))
            {
                fitWriter.Record(3, Encoding.UTF8.GetBytes(fit.Name));
            }
            if (fit.Mode is { } mode)
            {
                fitWriter.UInt(4, (ulong)mode);
            }
            foreach (var entry in fit.Entries)
            {
                entryWriter.Clear();
                EncodeEntry(entryWriter, entry);
                fitWriter.Record(5, entryWriter.Written);
            }
            document.Record(1, fitWriter.Written);
        }
        return document.Written.ToArray();
    }

    public static List<EsfFit> Decode(ReadOnlySpan<byte> data)
    {
        var fits = new List<EsfFit>();
        var reader = new WireReader(data);
        while (!reader.Done)
        {
            var field = reader.Next();
            if (field.Number != 1)
            {
                continue;
            }
            if (field.Wire != WireType.Len)
            {
                throw new EsfException("binary: Document.fits has the wrong wire type");
            }
            fits.Add(DecodeFit(field.Bytes));
        }
        if (fits.Count == 0)
        {
            throw new EsfException("binary: a document holds at least one fit");
        }
        return fits;
    }

    private static void EncodeEntry(WireWriter writer, Entry entry)
    {
        if (entry.Type is { } type)
        {
            writer.UInt(1, (ulong)type);
        }
        if (entry.Count is { } count)
        {
            writer.UInt(2, count);
        }
        if (!string.IsNullOrEmpty(entry.FitName))
        {
            writer.Record(3, Encoding.UTF8.GetBytes(entry.FitName));
        }
        if (entry.Charge is { } charge)
        {
            writer.UInt(4, (ulong)charge);
        }
        if (entry.ChargeCount is { } chargeCount)
        {
            writer.UInt(5, chargeCount);
        }
        if (entry.Mutaplasmid is { } mutaplasmid)
        {
            writer.UInt(6, (ulong)mutaplasmid);
        }
        if (entry.Overrides.Count > 0)
        {
            writer.PackedUInts(7, entry.Overrides.Keys);
            writer.PackedDoubles(8, entry.Overrides.Values.ToList());
        }
        if (entry.State is { } state)
        {
            writer.UInt(9, (ulong)state);
        }
        if (entry.Location is { } location)
        {
            writer.UInt(10, (ulong)location);
        }
    }

    private static EsfFit DecodeFit(ReadOnlySpan<byte> data)
    {
        ulong version = 0;
        int? hull = null;
        string? name = null;
        int? mode = null;
        var entries = new List<Entry>();
        var reader = new WireReader(data);
        while (!reader.Done)
        {
            var field = reader.Next();
            switch (field.Number, field.Wire)
            {
                case (1, WireType.Varint):
                    version = UInt32(field.Varint, "Fit.version");
                    break;
                case (2, WireType.Varint):
                    hull = Id(field.Varint, "Fit.hull");
                    break;
                case (3, WireType.Len):
                    name = String(field.Bytes, "Fit.name");
                    break;
                case (4, WireType.Varint):
                    mode = Id(field.Varint, "Fit.tactical_mode");
                    break;
                case (5, WireType.Len):
                    entries.Add(DecodeEntry(field.Bytes));
                    break;
                default:
                    throw NotInSchema("Fit");
            }
        }
        if (version != 1)
        {
            throw new EsfException($"binary: unsupported Fit.version {version}");
        }
        return new EsfFit { Hull = hull, Name = name, Mode = mode, Entries = entries };
    }

    private static Entry DecodeEntry(ReadOnlySpan<byte> data)
    {
        int? type = null, charge = null, mutaplasmid = null;
        uint? count = null, chargeCount = null;
        string? fitName = null;
        State? state = null;
        Location? location = null;
        var attributes = new List<int>();
        var values = new List<double>();
        var reader = new WireReader(data);
        while (!reader.Done)
        {
            var field = reader.Next();
            switch (field.Number, field.Wire)
            {
                case (1, WireType.Varint):
                    type = Id(field.Varint, "Entry.type");
                    break;
                case (2, WireType.Varint):
                    count = UInt32(field.Varint, "Entry.count");
                    break;
                case (3, WireType.Len):
                    fitName = String(field.Bytes, "Entry.fit_name");
                    break;
                case (4, WireType.Varint):
                    charge = Id(field.Varint, "Entry.charge");
                    break;
                case (5, WireType.Varint):
                    chargeCount = UInt32(field.Varint, "Entry.charge_count");
                    break;
                case (6, WireType.Varint):
                    mutaplasmid = Id(field.Varint, "Entry.mutaplasmid");
                    break;
                case (7, WireType.Varint):
                    attributes.Add(Id(field.Varint, "Entry.override_attributes"));
                    break;
                case (7, WireType.Len):
                    attributes.AddRange(
                        WireReader.UnpackUInts(field.Bytes).Select(v => Id(v, "Entry.override_attributes")));
                    break;
                case (8, WireType.I64):
                    values.Add(BinaryPrimitives.ReadDoubleLittleEndian(field.Bytes));
                    break;
                case (8, WireType.Len):
                    values.AddRange(WireReader.UnpackDoubles(field.Bytes));
                    break;
                case (9, WireType.Varint):
                    state = Known<State>(field.Varint, (ulong)State.Heat, "State");
                    break;
                case (10, WireType.Varint):
                    location = Known<Location>(field.Varint, (ulong)Location.Subsystem, "Location");
                    break;
                default:
                    throw NotInSchema("Entry");
            }
        }
        if (attributes.Count != values.Count)
        {
            throw new EsfException("binary: override fields have different lengths");
        }
        var overrides = new Dictionary<int, double>(attributes.Count);
        for (var i = 0; i < attributes.Count; i++)
        {
            if (!overrides.TryAdd(attributes[i], values[i]))
            {
                throw new EsfException("binary: an attribute is overridden twice");
            }
        }
        return new Entry(type)
        {
            Count = count,
            FitName = fitName,
            Charge = charge,
            ChargeCount = chargeCount,
            Mutaplasmid = mutaplasmid,
            Overrides = overrides,
            State = state,
            Location = location,
        };
    }

    private static uint UInt32(ulong value, string what) =>
        value <= uint.MaxValue ? (uint)value : throw new EsfException($"binary: {what} is out of range");

    private static int Id(ulong value, string what) =>
        value <= int.MaxValue ? (int)value : throw new EsfException($"binary: {what} is out of range");

    private static string? String(ReadOnlySpan<byte> payload, string what)
    {
        try
        {
            var value = StrictUtf8.GetString(payload);
            return value.Length > 0 ? value : null;
        }
        catch (DecoderFallbackException)
        {
            throw new EsfException($"binary: {what} is not valid UTF-8");
        }
    }

    private static T? Known<T>(ulong value, ulong last, string what)
        where T : struct, Enum => value switch
        {
            0 => null,
            _ when value <= last => (T)Enum.ToObject(typeof(T), value),
            _ => throw new EsfException($"binary: unknown {what} value {value}"),
        };

    private static EsfException NotInSchema(string what) =>
        new($"binary: {what} has a field that is not in the schema");
}
