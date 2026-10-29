using LazyTsql.Core.Schema;

namespace LazyTsql.Tui;

/// <summary>Schema tree node. Children are loaded on first expand and cached until refresh.</summary>
public abstract class SchemaNode
{
    public List<SchemaNode>? Children { get; set; }

    public bool IsLoading { get; set; }

    public virtual bool CanExpand => true;

    public abstract string Label { get; }

    public override string ToString() => Label;
}

public sealed class DatabaseNode(string name) : SchemaNode
{
    public string Name { get; } = name;

    public override string Label => Name;
}

public sealed class FolderNode(DbObjectKind kind, IReadOnlyList<DbObject> objects) : SchemaNode
{
    public DbObjectKind Kind { get; } = kind;

    public override string Label => $"{Plural(Kind)} ({objects.Count})";

    public static string Plural(DbObjectKind kind) => kind switch
    {
        DbObjectKind.Table => "Tables",
        DbObjectKind.View => "Views",
        DbObjectKind.Procedure => "Procedures",
        _ => "Functions",
    };
}

public sealed class ObjectNode(string database, DbObject value) : SchemaNode
{
    public string Database { get; } = database;

    public DbObject Value { get; } = value;

    public bool HasColumns => Value.Kind is DbObjectKind.Table or DbObjectKind.View;

    public override bool CanExpand => HasColumns;

    public override string Label => $"{Value.Schema}.{Value.Name}";
}

public sealed class ColumnNode(ColumnDefinition column) : SchemaNode
{
    public override bool CanExpand => false;

    public override string Label =>
        $"{(column.IsPrimaryKey ? "PK" : "  ")} {column.Name}  {column.DataType}{(column.IsNullable ? "?" : "")}";
}

public sealed class MessageNode(string text) : SchemaNode
{
    public override bool CanExpand => false;

    public override string Label => text;
}
