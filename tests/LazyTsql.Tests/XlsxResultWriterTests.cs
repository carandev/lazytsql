using System.IO.Compression;
using System.Xml.Linq;
using LazyTsql.Core.Execution;
using LazyTsql.Output;

namespace LazyTsql.Tests;

public class XlsxResultWriterTests
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [Fact]
    public void Writes_a_workbook_with_every_required_part()
    {
        var bytes = Write(new ResultSet([new ColumnInfo("id", "int")], [[1]], IsTruncated: false));

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(
            ["[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            XDocument.Load(stream); // every part is well-formed XML
        }
    }

    [Fact]
    public void Writes_a_bold_header_and_typed_cells()
    {
        var resultSet = new ResultSet(
            [
                new ColumnInfo("id", "int"), new ColumnInfo("name", "nvarchar"), new ColumnInfo("price", "decimal"),
                new ColumnInfo("active", "bit"), new ColumnInfo("born", "date"), new ColumnInfo("seen", "datetime2"),
                new ColumnInfo("data", "varbinary"),
            ],
            [
                [1, "Ana", 12.5m, true, new DateTime(2024, 3, 1), new DateTime(2024, 3, 1, 12, 0, 0), new byte[] { 0xAB, 0x01 }],
                [2, " padded ", null, false, null, null, null],
            ],
            IsTruncated: false);

        var rows = Sheet(Write(resultSet));

        Assert.Equal(3, rows.Count);
        Assert.All(rows[0], c => Assert.Equal(("inlineStr", "1"), (c.Type, c.Style)));
        Assert.Equal(["id", "name", "price", "active", "born", "seen", "data"], rows[0].Select(c => c.Value));
        Assert.Equal(
            [
                new Cell("A2", null, null, "1"), new Cell("B2", "inlineStr", null, "Ana"), new Cell("C2", null, null, "12.5"),
                new Cell("D2", "b", null, "1"), new Cell("E2", null, "2", "45352"), new Cell("F2", null, "3", "45352.5"),
                new Cell("G2", "inlineStr", null, "0xAB01"),
            ],
            rows[1]);
        // NULL leaves the cell out; whitespace survives.
        Assert.Equal(
            [new Cell("A3", null, null, "2"), new Cell("B3", "inlineStr", null, " padded "), new Cell("D3", "b", null, "0")],
            rows[2]);
    }

    [Fact]
    public void Keeps_numbers_Excel_would_round_as_text()
    {
        var resultSet = new ResultSet(
            [new ColumnInfo("big", "bigint"), new ColumnInfo("exact", "decimal"), new ColumnInfo("small", "bigint")],
            [[9_007_199_254_740_993L, 12345678901234567.89m, 999_999_999_999_999L]],
            IsTruncated: false);

        var row = Sheet(Write(resultSet))[1];

        Assert.Equal(
            [new Cell("A2", "inlineStr", null, "9007199254740993"), new Cell("B2", "inlineStr", null, "12345678901234567.89"), new Cell("C2", null, null, "999999999999999")],
            row);
    }

    [Fact]
    public void Writes_dates_before_March_1900_as_text()
    {
        var resultSet = new ResultSet([new ColumnInfo("d", "date")], [[new DateTime(1900, 1, 1)]], IsTruncated: false);

        Assert.Equal(new Cell("A2", "inlineStr", null, "1900-01-01"), Sheet(Write(resultSet))[1].Single());
    }

    [Fact]
    public void Drops_characters_XML_cannot_hold_and_cuts_text_at_the_Excel_cell_limit()
    {
        var resultSet = new ResultSet(
            [new ColumnInfo("a", "nvarchar"), new ColumnInfo("b", "nvarchar")],
            [["bell\u0007tab\tline\nend", new string('x', 40_000)]],
            IsTruncated: false);

        var row = Sheet(Write(resultSet))[1];

        Assert.Equal("belltab\tline\nend", row[0].Value);
        Assert.Equal(32_767, row[1].Value!.Length);
    }

    [Fact]
    public void Streamed_rows_match_the_in_memory_output()
    {
        var columns = new[] { new ColumnInfo("id", "int"), new ColumnInfo("name", "nvarchar") };
        object?[][] values = [[1, "Ana"], [2, null]];
        var streamed = new MemoryStream();
        using (var sink = new XlsxRowWriter(streamed))
        {
            sink.Begin(columns);
            foreach (var row in values) sink.Row(row);
        }

        Assert.Equal(Sheet(Write(new ResultSet(columns, values, IsTruncated: false))), Sheet(streamed.ToArray()));
    }

    [Fact]
    public void A_sink_closed_before_any_row_still_writes_a_valid_workbook()
    {
        var output = new MemoryStream();
        new XlsxRowWriter(output).Dispose();

        Assert.Empty(Assert.Single(Sheet(output.ToArray())));
    }

    [Fact]
    public void Refuses_more_rows_than_a_sheet_holds()
    {
        using var sink = new XlsxRowWriter(Stream.Null);
        sink.Begin([]);
        for (var i = 0; i < XlsxRowWriter.MaxRows; i++) sink.Row([]);

        var error = Assert.Throws<InvalidOperationException>(() => sink.Row([]));
        Assert.Equal("Excel sheets hold at most 1048575 rows besides the header. Export to CSV instead.", error.Message);
    }

    private static byte[] Write(ResultSet resultSet)
    {
        var output = new MemoryStream();
        XlsxResultWriter.Write(output, resultSet);
        return output.ToArray();
    }

    private sealed record Cell(string Reference, string? Type, string? Style, string? Value);

    private static List<List<Cell>> Sheet(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        return XDocument.Load(stream).Descendants(Main + "row")
            .Select(row => row.Elements(Main + "c").Select(c => new Cell(
                (string)c.Attribute("r")!,
                (string?)c.Attribute("t"),
                (string?)c.Attribute("s"),
                c.Element(Main + "v")?.Value ?? c.Element(Main + "is")?.Element(Main + "t")?.Value)).ToList())
            .ToList();
    }
}
