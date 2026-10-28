namespace LazyTsql.Core.Schema;

public enum DbObjectKind
{
    Table,
    View,
    Procedure,
    Function,
}

public sealed record DbObject(string Schema, string Name, DbObjectKind Kind)
{
    public string QualifiedName => $"[{Schema}].[{Name}]";
}

public sealed record ColumnDefinition(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, bool IsIdentity);

/// <summary>A foreign key from FromSchema.FromTable to ToSchema.ToTable, its columns in key order.</summary>
public sealed record ForeignKey(string FromSchema, string FromTable, string ToSchema, string ToTable, IReadOnlyList<KeyColumn> Columns);

public sealed record KeyColumn(string From, string To);
