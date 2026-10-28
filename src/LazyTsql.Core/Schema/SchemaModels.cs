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
