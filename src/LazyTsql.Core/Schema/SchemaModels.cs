namespace LazyTsql.Core.Schema;

public enum DbObjectKind
{
    Table,
    View,
    Procedure,
    Function,
    Synonym,
}

/// <summary>A schema object; a synonym also carries the object it points to when that is in this server.</summary>
public sealed record DbObject(string Schema, string Name, DbObjectKind Kind, SynonymTarget? Target = null)
{
    public string QualifiedName => $"[{Schema}].[{Name}]";
}

/// <summary>What a synonym points to: [database.][schema.]name, the database omitted when it is the synonym's own.</summary>
public sealed record SynonymTarget(string? Database, string? Schema, string Name);

public sealed record ColumnDefinition(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, bool IsIdentity);

/// <summary>A foreign key from FromSchema.FromTable to ToSchema.ToTable, its columns in key order.</summary>
public sealed record ForeignKey(string FromSchema, string FromTable, string ToSchema, string ToTable, IReadOnlyList<KeyColumn> Columns);

public sealed record KeyColumn(string From, string To);
