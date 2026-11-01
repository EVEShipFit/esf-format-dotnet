using System.Buffers;
using System.Buffers.Binary;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>The Protocol Buffers wire types.</summary>
internal enum WireType
{
    Varint = 0,
    I64 = 1,
    Len = 2,
    StartGroup = 3,
    EndGroup = 4,
    I32 = 5,
}

/// <summary>Writes Protocol Buffers records.</summary>
internal sealed class WireWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    public ReadOnlySpan<byte> Written => buffer.WrittenSpan;

    public void Clear() => buffer.Clear();

    public void UInt(int field, ulong value)
    {
        Key(field, WireType.Varint);
        Varint(value);
    }

    public void Record(int field, ReadOnlySpan<byte> payload)
    {
        Key(field, WireType.Len);
        Varint((ulong)payload.Length);
        buffer.Write(payload);
    }

    public void PackedUInts(int field, IEnumerable<int> values)
    {
        var packed = new WireWriter();
        foreach (var value in values)
        {
            packed.Varint((ulong)value);
        }
        Record(field, packed.Written);
    }

    public void PackedDoubles(int field, IReadOnlyCollection<double> values)
    {
        Key(field, WireType.Len);
        Varint((ulong)values.Count * 8);
        foreach (var value in values)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(buffer.GetSpan(8), value);
            buffer.Advance(8);
        }
    }

    private void Key(int field, WireType wire) => Varint((ulong)field << 3 | (ulong)wire);

    private void Varint(ulong value)
    {
        var span = buffer.GetSpan(10);
        var n = 0;
        while (value > 0x7F)
        {
            span[n++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[n++] = (byte)value;
        buffer.Advance(n);
    }
}

/// <summary>One record of a message: a varint, or the bytes of any other wire type but a group.</summary>
internal readonly ref struct WireField(int number, WireType wire, ulong varint, ReadOnlySpan<byte> bytes)
{
    public int Number { get; } = number;

    public WireType Wire { get; } = wire;

    public ulong Varint { get; } = varint;

    public ReadOnlySpan<byte> Bytes { get; } = bytes;
}

/// <summary>Reads the records of one Protocol Buffers message.</summary>
internal ref struct WireReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> data = data;
    private int pos;

    public readonly bool Done => pos >= data.Length;

    public static List<ulong> UnpackUInts(ReadOnlySpan<byte> payload)
    {
        var reader = new WireReader(payload);
        var values = new List<ulong>();
        while (!reader.Done)
        {
            values.Add(reader.Varint());
        }
        return values;
    }

    public static double[] UnpackDoubles(ReadOnlySpan<byte> payload)
    {
        if (payload.Length % 8 != 0)
        {
            throw new EsfException("binary: not valid Protocol Buffers (packed doubles cut short)");
        }
        var values = new double[payload.Length / 8];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadDoubleLittleEndian(payload[(i * 8)..]);
        }
        return values;
    }

    /// <summary>The next record; groups are skipped and read as no bytes.</summary>
    public WireField Next()
    {
        var (field, wire) = Key();
        return wire switch
        {
            WireType.Varint => new WireField(field, wire, Varint(), default),
            WireType.I64 => new WireField(field, wire, 0, Take(8)),
            WireType.I32 => new WireField(field, wire, 0, Take(4)),
            WireType.Len => new WireField(field, wire, 0, Take(Varint())),
            WireType.StartGroup => SkipGroup(field),
            _ => throw UnmatchedEndGroup(),
        };
    }

    private static EsfException UnmatchedEndGroup() =>
        new("binary: not valid Protocol Buffers (unmatched end group)");

    private ReadOnlySpan<byte> Take(ulong n)
    {
        if (n > (ulong)(data.Length - pos))
        {
            throw new EsfException("binary: not valid Protocol Buffers (cut short)");
        }
        var taken = data.Slice(pos, (int)n);
        pos += (int)n;
        return taken;
    }

    private ulong Varint()
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            var b = Take(1)[0];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return value;
            }
        }
        throw new EsfException("binary: not valid Protocol Buffers (varint is too long)");
    }

    private (int Field, WireType Wire) Key()
    {
        var key = Varint();
        var (field, wire) = (key >> 3, key & 7);
        if (field == 0 || key > uint.MaxValue || wire > (ulong)WireType.I32)
        {
            throw new EsfException($"binary: not valid Protocol Buffers (bad key {key})");
        }
        return ((int)field, (WireType)wire);
    }

    private WireField SkipGroup(int field)
    {
        var open = new Stack<int>([field]);
        while (open.Count > 0)
        {
            var (inner, wire) = Key();
            if (wire == WireType.StartGroup)
            {
                open.Push(inner);
            }
            else if (wire == WireType.EndGroup)
            {
                if (open.Pop() != inner)
                {
                    throw UnmatchedEndGroup();
                }
            }
            else
            {
                SkipValue(wire);
            }
        }
        return new WireField(field, WireType.StartGroup, 0, default);
    }

    private void SkipValue(WireType wire)
    {
        _ = wire switch
        {
            WireType.Varint => Varint(),
            WireType.I64 => (ulong)Take(8).Length,
            WireType.I32 => (ulong)Take(4).Length,
            _ => (ulong)Take(Varint()).Length,
        };
    }
}
