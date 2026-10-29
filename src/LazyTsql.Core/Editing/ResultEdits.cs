using System.Text;
using LazyTsql.Core.Execution;

namespace LazyTsql.Core.Editing;

/// <summary>
/// Where a result column comes from, as SQL Server describes the query in browse mode. Columns of a key the query does
/// not select come back hidden; <see cref="Database"/> is null for the database the query ran in.
/// </summary>
public sealed record SourceColumn(
    string? Name,
    bool IsHidden,
    string? Database,
    string? Schema,
    string? Table,
    string? Column,
    bool IsKey,
    bool IsUpdatable,
    string TypeName);

/// <summary>
/// Changes made to the cells of one result set, kept apart from its rows until <see cref="Commit"/>. A cell can change
/// only when its column is read from a table whose key the result shows, so every change names exactly one row.
/// </summary>
public sealed class ResultEdits
{
    private readonly ResultSet _set;
    private readonly Target?[] _targets;
    private readonly string?[] _reasons;
    private readonly Dictionary<(int Row, int Column), object?> _pending = [];

    private ResultEdits(ResultSet set, Target?[] targets, string?[] reasons)
    {
        _set = set;
        _targets = targets;
        _reasons = reasons;
    }

    public int Count => _pending.Count;

    public static ResultEdits Create(ResultSet set, IReadOnlyList<SourceColumn> source, string database)
    {
        var count = set.Columns.Count;
        var targets = new Target?[count];
        var reasons = new string?[count];
        var visible = source.Where(column => !column.IsHidden).ToList();
        if (visible.Count != count || visible.Where((column, i) => (column.Name ?? string.Empty) != set.Columns[i].Name).Any())
        {
            Array.Fill(reasons, "The query's columns changed since it ran. Run it again to edit.");
            return new ResultEdits(set, targets, reasons);
        }

        var tables = new Dictionary<string, Table>();
        for (var i = 0; i < count; i++)
        {
            var column = visible[i];
            var name = set.Columns[i].Name;
            if (column.Table is null || column.Column is null)
            {
                reasons[i] = $"{name} is computed by the query, not read from a table.";
                continue;
            }

            var table = TableOf(column, database, source, visible, tables);
            reasons[i] = !column.IsUpdatable ? $"{name} cannot be changed: it is an identity, computed or rowversion column."
                : !SqlValue.IsSupported(column.TypeName) ? $"{name} cannot be changed here: lazytsql cannot write {SqlValue.BaseType(column.TypeName)} values."
                : table.Problem is { } problem ? $"{name} cannot be changed: {problem}"
                : null;
            if (reasons[i] is null) targets[i] = new Target(table, column.Column, column.TypeName);
        }

        return new ResultEdits(set, targets, reasons);
    }

    /// <summary>Why the column cannot change, or null when it can.</summary>
    public string? ReadOnlyReason(int column) => _reasons[column];

    /// <summary>The column's SQL Server type, as <see cref="SqlValue"/> reads it; only for an editable column.</summary>
    public string TypeName(int column) => _targets[column]?.TypeName ?? throw new InvalidOperationException(_reasons[column]);

    public bool IsPending(int row, int column) => _pending.ContainsKey((row, column));

    public object? Value(int row, int column) => _pending.TryGetValue((row, column), out var value) ? value : _set.Rows[row][column];

    public void Set(int row, int column, object? value)
    {
        if (_reasons[column] is { } reason) throw new InvalidOperationException(reason);
        if (Same(value, _set.Rows[row][column])) _pending.Remove((row, column));
        else _pending[(row, column)] = value;
    }

    public void Revert(int row, int column) => _pending.Remove((row, column));

    public void RevertAll() => _pending.Clear();

    /// <summary>
    /// One UPDATE per changed row of each table, found by the key values the result read. The whole script is one
    /// transaction, rolled back unless every UPDATE changed exactly one row: a row deleted or re-keyed since the query
    /// ran saves nothing instead of saving part of the changes.
    /// </summary>
    public string Script()
    {
        var script = new StringBuilder("BEGIN TRY\n    BEGIN TRANSACTION;\n");
        var changes = _pending
            .OrderBy(change => change.Key.Row).ThenBy(change => change.Key.Column)
            .GroupBy(change => (change.Key.Row, _targets[change.Key.Column]!.Table), change => change);
        foreach (var change in changes)
        {
            var (row, table) = change.Key;
            var assignments = change.Select(cell =>
            {
                var target = _targets[cell.Key.Column]!;
                return $"{SqlValue.Quote(target.Column)} = {SqlValue.Literal(cell.Value, target.TypeName)}";
            });
            var keys = table.Key.Select(key =>
            {
                var value = _set.Rows[row][key.Index];
                return value is null ? $"{SqlValue.Quote(key.Column)} IS NULL" : $"{SqlValue.Quote(key.Column)} = {SqlValue.Literal(value, key.TypeName)}";
            });
            var message = $"Row {row + 1} no longer matches exactly one row of {table.Name}. Nothing was saved.";
            script.Append($"    UPDATE {table.Name} SET {string.Join(", ", assignments)} WHERE {string.Join(" AND ", keys)};\n");
            script.Append($"    IF @@ROWCOUNT <> 1 THROW 50000, {SqlValue.Literal(message, "nvarchar")}, 1;\n");
        }

        script.Append("    COMMIT TRANSACTION;\nEND TRY\nBEGIN CATCH\n    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;\n    THROW;\nEND CATCH;");
        return script.ToString();
    }

    /// <summary>After the script ran: the rows take the pending values, so the grid shows what the table now holds.</summary>
    public void Commit()
    {
        foreach (var ((row, column), value) in _pending) _set.Rows[row][column] = value;
        _pending.Clear();
    }

    private static Table TableOf(SourceColumn column, string database, IReadOnlyList<SourceColumn> source, List<SourceColumn> visible, Dictionary<string, Table> tables)
    {
        var name = $"{SqlValue.Quote(column.Database ?? database)}.{SqlValue.Quote(column.Schema ?? "dbo")}.{SqlValue.Quote(column.Table!)}";
        if (tables.TryGetValue(name, out var known)) return known;

        bool SameTable(SourceColumn other) => other.Table == column.Table && other.Schema == column.Schema && other.Database == column.Database;
        var display = $"{SqlValue.Quote(column.Schema ?? "dbo")}.{SqlValue.Quote(column.Table!)}";
        var keyColumns = source.Where(other => other.IsKey && SameTable(other)).ToList();
        var key = new List<KeyColumn>();
        string? problem = keyColumns.Count == 0 ? $"{display} has no primary key or unique key." : null;
        foreach (var keyColumn in keyColumns)
        {
            var index = visible.FindIndex(other => SameTable(other) && other.Column == keyColumn.Column);
            if (index < 0)
            {
                problem ??= $"add {display}'s key column {keyColumn.Column} to the query.";
                continue;
            }

            if (!SqlValue.IsSupported(keyColumn.TypeName)) problem ??= $"lazytsql cannot write {SqlValue.BaseType(keyColumn.TypeName)} values of {display}'s key.";
            key.Add(new KeyColumn(index, keyColumn.Column!, keyColumn.TypeName));
        }

        return tables[name] = new Table(name, key, problem);
    }

    private static bool Same(object? a, object? b) =>
        a is byte[] left && b is byte[] right ? left.AsSpan().SequenceEqual(right) : Equals(a, b);

    private sealed record Table(string Name, List<KeyColumn> Key, string? Problem);

    private sealed record KeyColumn(int Index, string Column, string TypeName);

    private sealed record Target(Table Table, string Column, string TypeName);
}
