using LazyTsql.Core.Execution;

namespace LazyTsql.Output;

/// <summary>RFC 4180 CSV with LF line endings; NULL is an empty field.</summary>
public static class CsvResultWriter
{
    public static void Write(TextWriter output, ResultSet resultSet)
    {
        var sink = new CsvRowWriter(output);
        sink.Begin(resultSet.Columns);
        foreach (var row in resultSet.Rows) sink.Row(row);
    }

    internal static void WriteHeader(TextWriter output, IReadOnlyList<ColumnInfo> columns)
    {
        output.Write(string.Join(',', columns.Select(c => Escape(c.Name))));
        output.Write('\n');
    }

    internal static void WriteRow(TextWriter output, object?[] row)
    {
        output.Write(string.Join(',', row.Select(v => v is null ? string.Empty : Escape(ValueFormatter.Format(v)))));
        output.Write('\n');
    }

    private static string Escape(string field)
    {
        var needsQuotes = field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            || (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1])));
        return needsQuotes ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}

/// <summary>Writes CSV as rows arrive, for result sets too large to hold in memory.</summary>
public sealed class CsvRowWriter(TextWriter output) : IRowSink
{
    public void Begin(IReadOnlyList<ColumnInfo> columns) => CsvResultWriter.WriteHeader(output, columns);

    public void Row(object?[] values) => CsvResultWriter.WriteRow(output, values);
}
