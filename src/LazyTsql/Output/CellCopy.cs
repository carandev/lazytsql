namespace LazyTsql.Output;

/// <summary>Text to copy from result cells: one cell as its whole value, several as tab-separated rows that paste into a spreadsheet.</summary>
public static class CellCopy
{
    /// <summary>
    /// Cells are copied as the rectangle that spans them, in table order; cells inside it that are not selected are empty,
    /// and so is NULL. Fields with a tab, line break or quote are quoted as spreadsheets expect.
    /// </summary>
    public static string Text(IReadOnlyCollection<(int Row, int Column)> cells, Func<int, int, object?> value)
    {
        if (cells.Count == 1)
        {
            var (row, column) = cells.First();
            return Field(value(row, column));
        }

        var selected = cells.ToHashSet();
        int top = cells.Min(cell => cell.Row), bottom = cells.Max(cell => cell.Row);
        int left = cells.Min(cell => cell.Column), right = cells.Max(cell => cell.Column);
        var lines = Enumerable.Range(top, bottom - top + 1).Select(row => string.Join('\t',
            Enumerable.Range(left, right - left + 1).Select(column => selected.Contains((row, column)) ? Escape(Field(value(row, column))) : string.Empty)));
        return string.Join('\n', lines);
    }

    private static string Field(object? value) => value is null ? string.Empty : ValueFormatter.Format(value);

    private static string Escape(string field) =>
        field.AsSpan().IndexOfAny("\t\r\n\"") >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
}
