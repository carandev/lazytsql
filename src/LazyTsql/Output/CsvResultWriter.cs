using LazyTsql.Core.Execution;

namespace LazyTsql.Output;

/// <summary>RFC 4180 CSV with LF line endings; NULL is an empty field.</summary>
public static class CsvResultWriter
{
    public static void Write(TextWriter output, ResultSet resultSet)
    {
        output.Write(string.Join(',', resultSet.Columns.Select(c => Escape(c.Name))));
        output.Write('\n');
        foreach (var row in resultSet.Rows)
        {
            output.Write(string.Join(',', row.Select(v => v is null ? string.Empty : Escape(ValueFormatter.Format(v)))));
            output.Write('\n');
        }
    }

    private static string Escape(string field)
    {
        var needsQuotes = field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            || (field.Length > 0 && (char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1])));
        return needsQuotes ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
