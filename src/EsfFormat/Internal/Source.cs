using System.Globalization;
using System.Text;

namespace EveShipFit.EsfFormat.Internal;

/// <summary>Fits as the text they stand for, and the check that their IDs match it (SPEC §10.2).</summary>
internal static class Source
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The text document the fits stand for, and the fit and entry each line is.</summary>
    public static (string Text, List<(int Fit, int? Entry)> Where) Write(IReadOnlyList<EsfFit> fits, ICatalog catalog)
    {
        if (fits.Count == 0)
        {
            throw new EsfFormatException("a document holds at least one fit");
        }
        var text = new StringBuilder();
        var where = new List<(int, int?)>();
        for (var n = 1; n <= fits.Count; n++)
        {
            var fit = fits[n - 1];
            try
            {
                text.Append("%esf/1\n");
                Hull(text, fit, catalog);
            }
            catch (EsfException e)
            {
                throw e.At(n, null);
            }
            where.Add((n, null));
            where.Add((n, null));
            for (var m = 1; m <= fit.Entries.Count; m++)
            {
                try
                {
                    Entry(text, fit.Entries[m - 1], catalog);
                }
                catch (EsfException e)
                {
                    throw e.At(n, m);
                }
                where.Add((n, m));
            }
        }
        return (text.ToString(), where);
    }

    /// <summary>Check that every ID is the one its name resolves to.</summary>
    public static void Verify(List<Fit> resolved, IReadOnlyList<EsfFit> fits)
    {
        for (var n = 1; n <= fits.Count; n++)
        {
            var (fit, given) = (resolved[n - 1], fits[n - 1]);
            try
            {
                Same(fit.Hull?.Id, given.Hull, "hull");
                Same(fit.Mode?.Id, given.Mode, "tactical mode");
            }
            catch (EsfException e)
            {
                throw e.At(n, null);
            }
            for (var m = 1; m <= given.Entries.Count; m++)
            {
                if (fit.Parts[m - 1] is not Item item)
                {
                    continue;
                }
                var entry = given.Entries[m - 1];
                try
                {
                    Same(item.Type.Id, entry.Type, "type");
                    Same(item.Charge?.Id, entry.Charge, "charge");
                    Same(item.Mutaplasmid?.Type.Id, entry.Mutaplasmid, "mutaplasmid");
                    var got = item.Overrides.Select(o => o.Attribute.Id);
                    if (!got.SequenceEqual(entry.Overrides.Keys))
                    {
                        throw new EsfRuleException(
                            $"attribute IDs [{string.Join(", ", entry.Overrides.Keys)}] resolve to [{string.Join(", ", got)}]");
                    }
                }
                catch (EsfException e)
                {
                    throw e.At(n, m);
                }
            }
        }
    }

    private static string Name(string text)
    {
        if (text.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new EsfFormatException("a fit name contains CR or LF");
        }
        try
        {
            StrictUtf8.GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            throw new EsfFormatException("a fit name is not valid UTF-8");
        }
        return Canonical.Quote(text);
    }

    private static TypeRecord Type(ICatalog catalog, int typeId, string what) =>
        catalog.TypeById(typeId) ?? throw new EsfCatalogException($"unknown {what} type ID {typeId}");

    private static string TypeName(ICatalog catalog, int typeId, string what) =>
        Canonical.QuoteType(Type(catalog, typeId, what).Name);

    private static string TypeRef(ICatalog catalog, int typeId, string what) =>
        Canonical.TypeRef(catalog, Type(catalog, typeId, what));

    private static void Hull(StringBuilder text, EsfFit fit, ICatalog catalog)
    {
        text.Append(fit.Hull is { } hull ? TypeRef(catalog, hull, "hull") : "-");
        if (!string.IsNullOrEmpty(fit.Name))
        {
            text.Append(' ').Append(Name(fit.Name));
        }
        if (fit.Mode is { } mode)
        {
            text.Append(" /").Append(TypeName(catalog, mode, "tactical mode"));
        }
        text.Append('\n');
    }

    private static void Entry(StringBuilder text, Entry e, ICatalog catalog)
    {
        if (e.Count == 0 || e.ChargeCount == 0)
        {
            throw new EsfRuleException("a count must be at least 1");
        }
        if (e.Slot == 0)
        {
            throw new EsfRuleException("a slot must be at least 1");
        }
        var rack = e.Location is { } location && location.ToPlace().IsRack();
        if (e.Slot is not null && !rack)
        {
            throw new EsfRuleException("a slot needs the rack it is in");
        }
        if (e.Type is not null && e.Slot is null && rack)
        {
            throw new EsfRuleException("an item names a rack only to pin a slot");
        }
        if (e.ChargeCount is not null && e.Charge is null)
        {
            throw new EsfRuleException("a charge count is set without a charge");
        }

        if (e.Count is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"{e.Count}x ");
        }
        text.Append(e.Type is { } type ? TypeRef(catalog, type, "item") : "-");
        if (!string.IsNullOrEmpty(e.FitName))
        {
            text.Append(' ').Append(Name(e.FitName));
        }
        if (e.Charge is { } charge)
        {
            text.Append(" :");
            if (e.ChargeCount is not null)
            {
                text.Append(CultureInfo.InvariantCulture, $"{e.ChargeCount}x ");
            }
            text.Append(TypeRef(catalog, charge, "charge"));
        }
        if (e.Mutaplasmid is { } mutaplasmid)
        {
            text.Append(" +").Append(TypeName(catalog, mutaplasmid, "mutaplasmid"));
        }
        if (e.Overrides.Count > 0)
        {
            text.Append(" {").AppendJoin(", ", e.Overrides.Select(Override(catalog))).Append('}');
        }
        if (e.State is { } state)
        {
            text.Append(" !").Append(state.Name());
        }
        if (e.Location is { } at)
        {
            text.Append(" @").Append(at.Name());
            if (e.Slot is { } slot)
            {
                text.Append(CultureInfo.InvariantCulture, $"{slot}");
            }
        }
        text.Append('\n');
    }

    private static Func<KeyValuePair<int, double>, string> Override(ICatalog catalog) => o =>
    {
        var attribute = catalog.AttributeById(o.Key) ?? throw new EsfCatalogException($"unknown attribute ID {o.Key}");
        if (!double.IsFinite(o.Value))
        {
            throw new EsfRuleException("an override value is not finite");
        }
        return $"{attribute.Name} {Number.Format(o.Value)}";
    };

    private static void Same(int? got, int? want, string what)
    {
        if (got != want)
        {
            throw new EsfRuleException($"{what} ID {want} is not what its name resolves to ({got})");
        }
    }
}
