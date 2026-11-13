using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Completion;

/// <summary>
/// Object and column names per database for completion. It answers only from what is loaded; a missing entry raises
/// a request once, so the host can load it in the background and suggestions appear on the next keystroke.
/// It is not thread-safe: other threads read a <see cref="Snapshot"/> instead.
/// </summary>
public sealed class SchemaCatalog
{
    private readonly Dictionary<string, IReadOnlyList<DbObject>> _objects;
    private readonly Dictionary<string, ILookup<string, DbObject>> _byName;
    private readonly Dictionary<string, IReadOnlyList<ColumnDefinition>> _columns;
    private readonly Dictionary<string, IReadOnlyList<ForeignKey>> _foreignKeys;
    private readonly HashSet<string> _requested = new(StringComparer.OrdinalIgnoreCase);

    // Only a snapshot has it: what it was asked for and did not have, instead of raising the events.
    private readonly List<Action<SchemaCatalog>>? _missed;

    public SchemaCatalog()
    {
        _objects = new(StringComparer.OrdinalIgnoreCase);
        _byName = new(StringComparer.OrdinalIgnoreCase);
        _columns = new(StringComparer.OrdinalIgnoreCase);
        _foreignKeys = new(StringComparer.OrdinalIgnoreCase);
    }

    private SchemaCatalog(SchemaCatalog source)
    {
        // The lists and lookups are replaced on each load, never changed, so sharing them is safe.
        _objects = new(source._objects, StringComparer.OrdinalIgnoreCase);
        _byName = new(source._byName, StringComparer.OrdinalIgnoreCase);
        _columns = new(source._columns, StringComparer.OrdinalIgnoreCase);
        _foreignKeys = new(source._foreignKeys, StringComparer.OrdinalIgnoreCase);
        _missed = [];
        Databases = source.Databases;
    }

    public IReadOnlyList<string> Databases { get; set; } = [];

    public event Action<string>? ObjectsNeeded;

    public event Action<string, DbObject>? ColumnsNeeded;

    public event Action<string>? ForeignKeysNeeded;

    /// <summary>
    /// A copy of the names loaded so far that another thread can read while this catalog keeps loading. It raises no
    /// events: what it misses is requested later by <see cref="RequestMissing"/>.
    /// </summary>
    public SchemaCatalog Snapshot() => new(this);

    /// <summary>Requests, as a lookup in this catalog would, what <paramref name="snapshot"/> was asked for and did not have.</summary>
    public void RequestMissing(SchemaCatalog snapshot)
    {
        foreach (var request in snapshot._missed ?? []) request(this);
    }

    public void SetObjects(string database, IReadOnlyList<DbObject> objects)
    {
        _objects[database] = objects;
        _byName[database] = objects.ToLookup(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    public void SetColumns(string database, DbObject table, IReadOnlyList<ColumnDefinition> columns) =>
        _columns[ColumnsKey(database, table)] = columns;

    public void SetForeignKeys(string database, IReadOnlyList<ForeignKey> foreignKeys) => _foreignKeys[database] = foreignKeys;

    public IReadOnlyList<DbObject>? Objects(string database)
    {
        if (_objects.TryGetValue(database, out var objects)) return objects;
        if (_requested.Add(database)) Request(catalog => catalog.Objects(database), () => ObjectsNeeded?.Invoke(database));
        return null;
    }

    /// <summary>
    /// The loaded objects of <paramref name="database"/> called <paramref name="name"/> in any schema, in load order,
    /// without scanning the whole list; empty when none are loaded, and it never requests a load.
    /// </summary>
    public IEnumerable<DbObject> ObjectsNamed(string database, string name) =>
        _byName.TryGetValue(database, out var byName) ? byName[name] : [];

    public IReadOnlyList<ColumnDefinition>? Columns(string database, DbObject table)
    {
        var key = ColumnsKey(database, table);
        if (_columns.TryGetValue(key, out var columns)) return columns;
        if (_requested.Add(key)) Request(catalog => catalog.Columns(database, table), () => ColumnsNeeded?.Invoke(database, table));
        return null;
    }

    public IReadOnlyList<ForeignKey>? ForeignKeys(string database)
    {
        if (_foreignKeys.TryGetValue(database, out var foreignKeys)) return foreignKeys;
        if (_requested.Add($"{database}\u0001fk")) Request(catalog => catalog.ForeignKeys(database), () => ForeignKeysNeeded?.Invoke(database));
        return null;
    }

    public void Clear()
    {
        _objects.Clear();
        _byName.Clear();
        _foreignKeys.Clear();
        _columns.Clear();
        _requested.Clear();
        Databases = [];
    }

    private void Request(Action<SchemaCatalog> lookup, Action raise)
    {
        if (_missed is null) raise();
        else _missed.Add(lookup);
    }

    private static string ColumnsKey(string database, DbObject table) => $"{database}\u0001{table.Schema}\u0001{table.Name}";
}
