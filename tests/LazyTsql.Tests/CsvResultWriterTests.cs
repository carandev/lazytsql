using LazyTsql.Core.Execution;
using LazyTsql.Output;

namespace LazyTsql.Tests;

public class CsvResultWriterTests
{
    [Fact]
    public void Writes_header_and_rows_quoting_only_when_needed()
    {
        var resultSet = new ResultSet(
            [new ColumnInfo("id", "int"), new ColumnInfo("name", "nvarchar"), new ColumnInfo("price", "decimal")],
            [[1, "Ana", 12.5m], [2, "Pérez, \"Luis\"", null], [3, "two\nlines", 0.1m]],
            IsTruncated: false);
        var output = new StringWriter();

        CsvResultWriter.Write(output, resultSet);

        Assert.Equal(
            "id,name,price\n1,Ana,12.5\n2,\"Pérez, \"\"Luis\"\"\",\n3,\"two\nlines\",0.1\n",
            output.ToString());
    }

    [Fact]
    public void Quotes_fields_with_surrounding_spaces_and_header_names()
    {
        var resultSet = new ResultSet([new ColumnInfo("full name", "nvarchar")], [[" padded "]], false);
        var output = new StringWriter();

        CsvResultWriter.Write(output, resultSet);

        Assert.Equal("full name\n\" padded \"\n", output.ToString());
    }
}
