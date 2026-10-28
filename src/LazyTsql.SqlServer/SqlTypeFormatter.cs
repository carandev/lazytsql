namespace LazyTsql.SqlServer;

/// <summary>Renders sys.columns metadata as the type declaration a developer would write.</summary>
public static class SqlTypeFormatter
{
    public static string Format(string typeName, short maxLength, byte precision, byte scale) => typeName switch
    {
        "varchar" or "char" or "varbinary" or "binary" => $"{typeName}({Length(maxLength)})",
        "nvarchar" or "nchar" => $"{typeName}({(maxLength == -1 ? "max" : (maxLength / 2).ToString())})",
        "decimal" or "numeric" => $"{typeName}({precision},{scale})",
        "datetime2" or "time" or "datetimeoffset" => $"{typeName}({scale})",
        _ => typeName,
    };

    private static string Length(short maxLength) => maxLength == -1 ? "max" : maxLength.ToString();
}
