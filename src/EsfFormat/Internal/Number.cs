using System.Globalization;
using System.Numerics;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>Floats as decimals.</summary>
internal static class Number
{
    /// <summary>The shortest decimal, without exponent, that reads back as the same float.</summary>
    public static string Format(double value)
    {
        if (value == 0)
        {
            return "0";
        }
        var (digits, exponent) = Shortest(Math.Abs(value));
        var sign = value < 0 ? "-" : "";
        if (exponent >= 0)
        {
            return sign + digits + new string('0', exponent);
        }
        var point = digits.Length + exponent;
        if (point > 0)
        {
            return $"{sign}{digits[..point]}.{digits[point..]}";
        }
        return $"{sign}0.{new string('0', -point)}{digits}";
    }

    /// <summary>The fewest digits that read back as a positive, finite float: value = digits × 10^exponent.</summary>
    public static (string Digits, int Exponent) Shortest(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var e = text.IndexOf('E', StringComparison.Ordinal);
        var exponent = e < 0 ? 0 : int.Parse(text.AsSpan(e + 1), CultureInfo.InvariantCulture);
        var mantissa = e < 0 ? text : text[..e];
        var point = mantissa.IndexOf('.', StringComparison.Ordinal);
        if (point >= 0)
        {
            exponent -= mantissa.Length - point - 1;
            mantissa = mantissa.Remove(point, 1);
        }
        var digits = mantissa.TrimStart('0');
        var trimmed = digits.TrimEnd('0');
        return (trimmed, exponent + digits.Length - trimmed.Length);
    }

    /// <summary>How many charges fit in an item (§6.5): capacity over volume, both as their shortest decimals.</summary>
    public static BigInteger? FullLoad(TypeRecord item, TypeRecord charge)
    {
        if (item.Capacity <= 0 || charge.Volume <= 0)
        {
            return null;
        }
        var (capacityDigits, capacityExponent) = Shortest(item.Capacity);
        var (volumeDigits, volumeExponent) = Shortest(charge.Volume);
        var capacity = BigInteger.Parse(capacityDigits, CultureInfo.InvariantCulture);
        var volume = BigInteger.Parse(volumeDigits, CultureInfo.InvariantCulture);
        var shift = capacityExponent - volumeExponent;
        if (shift >= 0)
        {
            capacity *= BigInteger.Pow(10, shift);
        }
        else
        {
            volume *= BigInteger.Pow(10, -shift);
        }
        return capacity / volume;
    }
}
