using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Completion;

/// <summary>
/// Object and column names per database for completion. It answers only from what is loaded; a missing entry raises
/// a request once, so the host can load it in the background and suggestions appear on the next keystroke.
/// </summary>
public sealed class SchemaCatalog
{
    private readonly Dictionary<string, IReadOnlyList<DbObject>> _objects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ColumnDefinition>> _columns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ForeignKey>> _foreignKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _requested = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Databases { get; set; } = [];

    public event Action<string>? ObjectsNeeded;

    public event Action<string, DbObject>? ColumnsNeeded;

    public event Action<string>? ForeignKeysNeeded;

    public void SetObjects(string database, IReadOnlyList<DbObject> objects) => _objects[database] = objects;

    public void SetColumns(string database, DbObject table, IReadOnlyList<ColumnDefinition> columns) =>
        _columns[ColumnsKey(database, table)] = columns;

    public void SetForeignKeys(string database, IReadOnlyList<ForeignKey> foreignKeys) => _foreignKeys[database] = foreignKeys;

    public IReadOnlyList<DbObject>? Objects(string database)
    {
        if (_objects.TryGetValue(database, out var objects)) return objects;
        if (_requested.Add(database)) ObjectsNeeded?.Invoke(database);
        return null;
    }

    public IReadOnlyList<ColumnDefinition>? Columns(string database, DbObject table)
    {
        var key = ColumnsKey(database, table);
        if (_columns.TryGetValue(key, out var columns)) return columns;
        if (_requested.Add(key)) ColumnsNeeded?.Invoke(database, table);
        return null;
    }

    public IReadOnlyList<ForeignKey>? ForeignKeys(string database)
    {
        if (_foreignKeys.TryGetValue(database, out var foreignKeys)) return foreignKeys;
        if (_requested.Add($"{database}\u0001fk")) ForeignKeysNeeded?.Invoke(database);
        return null;
    }

    public void Clear()
    {
        _objects.Clear();
        _foreignKeys.Clear();
        _columns.Clear();
        _requested.Clear();
        Databases = [];
    }

    private static string ColumnsKey(string database, DbObject table) => $"{database}\u0001{table.Schema}\u0001{table.Name}";
}
