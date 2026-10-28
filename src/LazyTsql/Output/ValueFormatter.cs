using System.Globalization;

namespace LazyTsql.Output;

public static class ValueFormatter
{
    public const string NullText = "NULL";

    /// <summary>Single-line, culture-invariant text for a cell, as shown in grids and text output.</summary>
    public static string Format(object? value) => value switch
    {
        null => NullText,
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        DateTime dateTime => dateTime.ToString(dateTime.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF zzz", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "1" : "0",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    public static string FormatSingleLine(object? value) =>
        Format(value).ReplaceLineEndings("⏎");
}
