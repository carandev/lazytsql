using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;
using LazyTsql.Output;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Virtual table over a result set: cells are formatted only when the grid draws them.</summary>
public sealed class ResultSetTableSource(ResultSet resultSet) : ITableSource
{
    private readonly string[] _columnNames = resultSet.Columns.Select(c => c.Name).ToArray();

    /// <summary>Changes not saved yet; their cells show the new value.</summary>
    public ResultEdits? Edits { get; set; }

    public object this[int row, int col] => ValueFormatter.FormatSingleLine(Edits is { } edits ? edits.Value(row, col) : resultSet.Rows[row][col]);

    public string[] ColumnNames => _columnNames;

    public int Columns => _columnNames.Length;

    public int Rows => resultSet.Rows.Count;
}
