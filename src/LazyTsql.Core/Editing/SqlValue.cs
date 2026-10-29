using System.Globalization;

namespace LazyTsql.Core.Editing;

/// <summary>
/// Reads typed text as a value of a SQL Server type and writes values as T-SQL literals. Parsed values have the .NET
/// type SqlClient reads for that column, so an edited cell looks like a read one. Dates and times are ISO 8601 only:
/// a literal such as 01/02/2024 means different days under different DATEFORMAT settings.
/// </summary>
public static class SqlValue
{
    private const string Guid = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private enum Kind { Ansi, Unicode, Integer, Exact, Approximate, Bit, Date, DateTime, SmallDateTime, DateTime2, DateTimeOffset, Time, Guid, Binary }

    private static readonly Dictionary<string, Kind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["char"] = Kind.Ansi, ["varchar"] = Kind.Ansi, ["text"] = Kind.Ansi,
        ["nchar"] = Kind.Unicode, ["nvarchar"] = Kind.Unicode, ["ntext"] = Kind.Unicode, ["sysname"] = Kind.Unicode, ["xml"] = Kind.Unicode,
        ["tinyint"] = Kind.Integer, ["smallint"] = Kind.Integer, ["int"] = Kind.Integer, ["bigint"] = Kind.Integer,
        ["decimal"] = Kind.Exact, ["numeric"] = Kind.Exact, ["money"] = Kind.Exact, ["smallmoney"] = Kind.Exact,
        ["float"] = Kind.Approximate, ["real"] = Kind.Approximate,
        ["bit"] = Kind.Bit,
        ["date"] = Kind.Date, ["datetime"] = Kind.DateTime, ["smalldatetime"] = Kind.SmallDateTime, ["datetime2"] = Kind.DateTime2,
        ["datetimeoffset"] = Kind.DateTimeOffset, ["time"] = Kind.Time,
        ["uniqueidentifier"] = Kind.Guid,
        ["binary"] = Kind.Binary, ["varbinary"] = Kind.Binary, ["image"] = Kind.Binary,
    };

    private static readonly string[] DateTimeFormats =
        [.. from separator in new[] { " ", "'T'" } from time in new[] { "HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF" } select $"yyyy-MM-dd{separator}{time}", "yyyy-MM-dd"];

    private static readonly string[] OffsetFormats =
        [.. from format in DateTimeFormats.SkipLast(1) from zone in new[] { " zzz", "zzz" } select format + zone];

    private static readonly string[] TimeFormats = [@"hh\:mm", @"hh\:mm\:ss", @"hh\:mm\:ss\.FFFFFFF"];

    /// <summary>The type name without its length or precision: <c>decimal(10,2)</c> is <c>decimal</c>.</summary>
    public static string BaseType(string typeName)
    {
        var open = typeName.IndexOf('(');
        return (open < 0 ? typeName : typeName[..open]).Trim().ToLowerInvariant();
    }

    public static bool IsSupported(string typeName) => Kinds.ContainsKey(BaseType(typeName));

    /// <summary>Bracket-quotes a name, so it is read as a name whatever it contains.</summary>
    public static string Quote(string name) => $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";

    public static bool TryParse(string text, string typeName, out object? value, out string? error)
    {
        var type = BaseType(typeName);
        value = Kinds[type] switch
        {
            Kind.Ansi or Kind.Unicode => text,
            Kind.Integer => ParseInteger(text, type),
            Kind.Exact => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact) ? exact : null,
            Kind.Approximate => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && (type != "real" || float.IsFinite((float)number))
                ? type == "real" ? (object)(float)number : number
                : null,
            Kind.Bit => text.Trim().ToLowerInvariant() switch { "1" or "true" => true, "0" or "false" => false, _ => null },
            Kind.Date => DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
            Kind.DateTime or Kind.SmallDateTime or Kind.DateTime2 =>
                DateTime.TryParseExact(text.Trim(), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) ? dateTime : null,
            Kind.DateTimeOffset =>
                DateTimeOffset.TryParseExact(text.Trim(), OffsetFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset) ? offset : null,
            Kind.Time => TimeSpan.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, out var time) && time < TimeSpan.FromDays(1) ? time : null,
            Kind.Guid => System.Guid.TryParse(text.Trim(), out var guid) ? guid : null,
            Kind.Binary => ParseBinary(text.Trim()),
            _ => null,
        };

        error = value is null ? $"{type} expects {Expected(type)}." : null;
        return value is not null;
    }

    public static string Literal(object? value, string typeName)
    {
        if (value is null) return "NULL";
        var type = BaseType(typeName);
        return Kinds[type] switch
        {
            Kind.Unicode => Text(value, unicode: true),
            Kind.Ansi => Text(value, unicode: ((string)value).Any(c => c > 127)),
            Kind.Integer or Kind.Exact => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            Kind.Approximate => value is float single ? single.ToString("R", CultureInfo.InvariantCulture) : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
            Kind.Bit => (bool)value ? "1" : "0",
            Kind.Date => Quoted((DateTime)value, "yyyy-MM-dd"),
            Kind.DateTime => Quoted((DateTime)value, "yyyy-MM-dd'T'HH:mm:ss.fff"),
            Kind.SmallDateTime => Quoted((DateTime)value, "yyyy-MM-dd'T'HH:mm:ss"),
            Kind.DateTime2 => Quoted((DateTime)value, "yyyy-MM-dd'T'HH:mm:ss.fffffff"),
            Kind.DateTimeOffset => $"'{((DateTimeOffset)value).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)}'",
            Kind.Time => $"'{((TimeSpan)value).ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture)}'",
            Kind.Guid => $"'{(Guid)value:D}'",
            Kind.Binary => "0x" + Convert.ToHexString((byte[])value),
            _ => throw new NotSupportedException($"lazytsql cannot write {type} values."),
        };
    }

    private static object? ParseInteger(string text, string type)
    {
        if (!long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return null;
        var (min, max) = Range(type);
        if (number < min || number > max) return null;
        return type switch
        {
            "tinyint" => (object)(byte)number,
            "smallint" => (object)(short)number,
            "int" => (object)(int)number,
            _ => number,
        };
    }

    private static (long Min, long Max) Range(string type) => type switch
    {
        "tinyint" => (byte.MinValue, byte.MaxValue),
        "smallint" => (short.MinValue, short.MaxValue),
        "int" => (int.MinValue, int.MaxValue),
        _ => (long.MinValue, long.MaxValue),
    };

    private static byte[]? ParseBinary(string text)
    {
        if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || text.Length % 2 != 0) return null;
        try
        {
            return Convert.FromHexString(text.AsSpan(2));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Expected(string type) => Kinds[type] switch
    {
        Kind.Integer => $"a whole number from {Range(type).Min} to {Range(type).Max}",
        Kind.Exact => "a number such as 12.50",
        Kind.Approximate => "a number such as 1.5 or 2E-3",
        Kind.Bit => "1 or 0",
        Kind.Date => "yyyy-MM-dd",
        Kind.DateTimeOffset => "yyyy-MM-dd HH:mm:ss +hh:mm",
        Kind.DateTime or Kind.SmallDateTime or Kind.DateTime2 => "yyyy-MM-dd HH:mm:ss",
        Kind.Time => "HH:mm:ss",
        Kind.Guid => $"a GUID such as {Guid}",
        Kind.Binary => "hexadecimal bytes such as 0x1FA0",
        _ => "text",
    };

    private static string Text(object value, bool unicode) =>
        $"{(unicode ? "N" : string.Empty)}'{((string)value).Replace("'", "''", StringComparison.Ordinal)}'";

    private static string Quoted(DateTime value, string format) => $"'{value.ToString(format, CultureInfo.InvariantCulture)}'";
}
