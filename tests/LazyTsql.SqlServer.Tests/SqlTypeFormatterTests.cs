namespace LazyTsql.SqlServer.Tests;

public class SqlTypeFormatterTests
{
    [Theory]
    [InlineData("nvarchar", 200, 0, 0, "nvarchar(100)")]
    [InlineData("nvarchar", -1, 0, 0, "nvarchar(max)")]
    [InlineData("varchar", 50, 0, 0, "varchar(50)")]
    [InlineData("varbinary", -1, 0, 0, "varbinary(max)")]
    [InlineData("decimal", 9, 18, 4, "decimal(18,4)")]
    [InlineData("datetime2", 8, 27, 7, "datetime2(7)")]
    [InlineData("int", 4, 10, 0, "int")]
    public void Formats_declarations(string type, short maxLength, byte precision, byte scale, string expected)
    {
        Assert.Equal(expected, SqlTypeFormatter.Format(type, maxLength, precision, scale));
    }
}
