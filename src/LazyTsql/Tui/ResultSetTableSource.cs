using LazyTsql.Core.Execution;
using LazyTsql.Output;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Virtual table over a result set: cells are formatted only when the grid draws them.</summary>
public sealed class ResultSetTableSource(ResultSet resultSet) : ITableSource
{
    private readonly string[] _columnNames = resultSet.Columns.Select(c => c.Name).ToArray();

    public object this[int row, int col] => ValueFormatter.FormatSingleLine(resultSet.Rows[row][col]);

    public string[] ColumnNames => _columnNames;

    public int Columns => _columnNames.Length;

    public int Rows => resultSet.Rows.Count;
}
