using LazyTsql.Core.Execution;

namespace LazyTsql.Output;

/// <summary>Human-readable aligned tables for terminal use without the TUI.</summary>
public static class TextResultWriter
{
    private const int MaxColumnWidth = 40;

    public static void WriteBatches(TextWriter output, IReadOnlyList<BatchResult> results)
    {
        foreach (var result in results)
        {
            foreach (var resultSet in result.ResultSets) WriteTable(output, resultSet);
            WriteMessages(output, result);
        }
    }

    /// <summary>Server messages in sqlcmd format, with lines relative to the submitted script.</summary>
    public static void WriteMessages(TextWriter output, BatchResult result)
    {
        foreach (var message in result.Messages)
        {
            var line = result.Batch.StartLine + Math.Max(message.LineNumber, 1) - 1;
            output.WriteLine(message.IsError ? $"Msg {message.Number}, Level {message.Severity}, Line {line}: {message.Text}" : message.Text);
        }

        if (result.RecordsAffected >= 0) output.WriteLine($"({result.RecordsAffected} rows affected)");
    }

    public static void WriteTable(TextWriter output, ResultSet resultSet)
    {
        var cells = resultSet.Rows.Select(row => row.Select(v => Truncate(ValueFormatter.FormatSingleLine(v))).ToArray()).ToList();
        var widths = resultSet.Columns
            .Select((column, i) => Math.Max(Truncate(column.Name).Length, cells.Count == 0 ? 0 : cells.Max(r => r[i].Length)))
            .ToArray();

        output.WriteLine(string.Join(" | ", resultSet.Columns.Select((c, i) => Truncate(c.Name).PadRight(widths[i]))).TrimEnd());
        output.WriteLine(string.Join("-+-", widths.Select(w => new string('-', w))));
        foreach (var row in cells)
        {
            output.WriteLine(string.Join(" | ", row.Select((value, i) => value.PadRight(widths[i]))).TrimEnd());
        }

        output.WriteLine(resultSet.IsTruncated ? $"({cells.Count} rows shown, more available; use --max-rows)" : $"({cells.Count} rows)");
        output.WriteLine();
    }

    private static string Truncate(string text) => text.Length <= MaxColumnWidth ? text : text[..(MaxColumnWidth - 1)] + "…";
}
