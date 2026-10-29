using LazyTsql.Core.Editing;

namespace LazyTsql.Core.Tests;

public class SqlValueTests
{
    [Theory]
    [InlineData("int", "42", 42)]
    [InlineData("int", "-7", -7)]
    [InlineData("bigint", "9000000000", 9000000000L)]
    [InlineData("smallint", "300", (short)300)]
    [InlineData("tinyint", "255", (byte)255)]
    [InlineData("bit", "1", true)]
    [InlineData("bit", "false", false)]
    [InlineData("float", "1.5", 1.5)]
    [InlineData("real", "2.25", 2.25f)]
    [InlineData("nvarchar(20)", " keeps spaces ", " keeps spaces ")]
    [InlineData("varchar", "", "")]
    public void Typed_text_becomes_the_value_the_column_reads_back(string type, string text, object expected)
    {
        Assert.True(SqlValue.TryParse(text, type, out var value, out var error), error);
        Assert.Equal(expected, value);
        Assert.Equal(expected.GetType(), value!.GetType());
    }

    [Fact]
    public void Exact_numbers_dates_guids_and_bytes_parse_to_their_types()
    {
        Assert.Equal(12.50m, Parse("12.50", "decimal(10,2)"));
        Assert.Equal(3.1m, Parse("3.1", "money"));
        Assert.Equal(new DateTime(2024, 2, 29), Parse("2024-02-29", "date"));
        Assert.Equal(new DateTime(2024, 2, 29, 10, 5, 0), Parse("2024-02-29 10:05", "datetime"));
        Assert.Equal(new DateTime(2024, 2, 29, 10, 5, 1).AddTicks(1234567), Parse("2024-02-29T10:05:01.1234567", "datetime2"));
        Assert.Equal(new DateTimeOffset(2024, 2, 29, 10, 5, 1, TimeSpan.FromHours(-5)), Parse("2024-02-29 10:05:01 -05:00", "datetimeoffset"));
        Assert.Equal(new TimeSpan(0, 23, 59, 58, 500), Parse("23:59:58.5", "time"));
        Assert.Equal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), Parse("0F8FAD5B-D9CB-469F-A165-70867728950E", "uniqueidentifier"));
        Assert.Equal(new byte[] { 0x1F, 0xA0 }, Parse("0x1FA0", "varbinary(max)"));
    }

    [Theory]
    [InlineData("int", "4.5", "int expects a whole number from -2147483648 to 2147483647.")]
    [InlineData("tinyint", "256", "tinyint expects a whole number from 0 to 255.")]
    [InlineData("decimal(5,2)", "1,5", "decimal expects a number such as 12.50.")]
    [InlineData("float", "NaN", "float expects a number such as 1.5 or 2E-3.")]
    [InlineData("real", "1E39", "real expects a number such as 1.5 or 2E-3.")]
    [InlineData("bit", "yes", "bit expects 1 or 0.")]
    [InlineData("date", "29/02/2024", "date expects yyyy-MM-dd.")]
    [InlineData("date", "2024-02-29 10:00", "date expects yyyy-MM-dd.")]
    [InlineData("datetime", "02/29/2024", "datetime expects yyyy-MM-dd HH:mm:ss.")]
    [InlineData("datetimeoffset", "2024-02-29 10:00", "datetimeoffset expects yyyy-MM-dd HH:mm:ss +hh:mm.")]
    [InlineData("time", "25:00", "time expects HH:mm:ss.")]
    [InlineData("uniqueidentifier", "abc", "uniqueidentifier expects a GUID such as 0f8fad5b-d9cb-469f-a165-70867728950e.")]
    [InlineData("varbinary", "1FA", "varbinary expects hexadecimal bytes such as 0x1FA0.")]
    public void Text_that_does_not_fit_the_type_explains_what_it_expects(string type, string text, string message)
    {
        Assert.False(SqlValue.TryParse(text, type, out _, out var error));
        Assert.Equal(message, error);
    }

    [Theory]
    [InlineData("geography")]
    [InlineData("sql_variant")]
    [InlineData("hierarchyid")]
    [InlineData("timestamp")]
    public void Types_without_a_literal_are_not_supported(string type) => Assert.False(SqlValue.IsSupported(type));

    [Fact]
    public void Literals_quote_and_format_each_type_for_SQL_Server()
    {
        Assert.Equal("NULL", SqlValue.Literal(null, "int"));
        Assert.Equal("N'it''s ☃'", SqlValue.Literal("it's ☃", "nvarchar"));
        Assert.Equal("'plain'", SqlValue.Literal("plain", "varchar"));
        // A varchar value outside ASCII stays Unicode, so the server converts it with the column's collation.
        Assert.Equal("N'año'", SqlValue.Literal("año", "varchar"));
        Assert.Equal("-42", SqlValue.Literal(-42, "int"));
        Assert.Equal("12.50", SqlValue.Literal(12.50m, "decimal"));
        Assert.Equal("1E-05", SqlValue.Literal(0.00001, "float"));
        Assert.Equal("2.25", SqlValue.Literal(2.25f, "real"));
        Assert.Equal("1", SqlValue.Literal(true, "bit"));
        Assert.Equal("'2024-02-29'", SqlValue.Literal(new DateTime(2024, 2, 29), "date"));
        Assert.Equal("'2024-02-29T10:05:01.003'", SqlValue.Literal(new DateTime(2024, 2, 29, 10, 5, 1).AddTicks(33333), "datetime"));
        Assert.Equal("'2024-02-29T10:05:00'", SqlValue.Literal(new DateTime(2024, 2, 29, 10, 5, 0), "smalldatetime"));
        Assert.Equal("'2024-02-29T10:05:01.1234567'", SqlValue.Literal(new DateTime(2024, 2, 29, 10, 5, 1).AddTicks(1234567), "datetime2"));
        Assert.Equal("'2024-02-29T10:05:01.0000000-05:00'", SqlValue.Literal(new DateTimeOffset(2024, 2, 29, 10, 5, 1, TimeSpan.FromHours(-5)), "datetimeoffset"));
        Assert.Equal("'23:59:58.5000000'", SqlValue.Literal(new TimeSpan(0, 23, 59, 58, 500), "time"));
        Assert.Equal("'0f8fad5b-d9cb-469f-a165-70867728950e'", SqlValue.Literal(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), "uniqueidentifier"));
        Assert.Equal("0x1FA0", SqlValue.Literal(new byte[] { 0x1F, 0xA0 }, "varbinary"));
        Assert.Equal("0x", SqlValue.Literal(Array.Empty<byte>(), "binary"));
    }

    [Fact]
    public void Bracket_quoting_escapes_closing_brackets() => Assert.Equal("[a]]b]", SqlValue.Quote("a]b"));

    private static object? Parse(string text, string type)
    {
        Assert.True(SqlValue.TryParse(text, type, out var value, out var error), error);
        return value;
    }
}
