using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using LazyTsql.Core.Execution;

namespace LazyTsql.Output;

/// <summary>An Excel workbook (.xlsx) with one sheet: a bold, frozen header row and one row per result row.</summary>
public static class XlsxResultWriter
{
    public static void Write(Stream output, ResultSet resultSet)
    {
        using var sink = new XlsxRowWriter(output);
        sink.Begin(resultSet.Columns);
        foreach (var row in resultSet.Rows) sink.Row(row);
    }
}

/// <summary>
/// Writes the workbook as rows arrive, for result sets too large to hold in memory; disposing it completes the file.
/// Numbers, dates and bits become typed cells, NULL an empty cell, and everything else text, as the grid shows it.
/// </summary>
public sealed class XlsxRowWriter : IRowSink, IDisposable
{
    /// <summary>Rows a sheet holds below its header.</summary>
    public const int MaxRows = 1_048_575;

    private const int MaxColumns = 16_384;
    private const int MaxCellText = 32_767;
    private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    // Excel keeps 15 significant digits; larger integers would silently change.
    private const long MaxExactInteger = 999_999_999_999_999;

    // Style indexes in styles.xml.
    private const string HeaderStyle = "1";
    private const string DateStyle = "2";
    private const string DateTimeStyle = "3";

    // Excel counts 1900 as a leap year, so its serial numbers are off by one before March 1900.
    private static readonly DateTime FirstExactDate = new(1900, 3, 1);

    private readonly ZipArchive _zip;
    private Stream? _sheetStream;
    private XmlWriter? _sheet;
    private string[] _columnNames = [];
    private int _rowNumber;
    private bool _disposed;

    public XlsxRowWriter(Stream output)
    {
        _zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
    }

    public void Begin(IReadOnlyList<ColumnInfo> columns)
    {
        if (_sheet is not null) throw new InvalidOperationException("The sheet has already begun.");
        if (columns.Count > MaxColumns) throw new InvalidOperationException($"Excel sheets hold at most {MaxColumns} columns. Export to CSV instead.");

        WritePart("[Content_Types].xml", ContentTypes);
        WritePart("_rels/.rels", RootRelationships);
        WritePart("xl/workbook.xml", Workbook);
        WritePart("xl/_rels/workbook.xml.rels", WorkbookRelationships);
        WritePart("xl/styles.xml", Styles);

        _columnNames = [.. Enumerable.Range(0, columns.Count).Select(ColumnName)];
        _sheetStream = _zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest).Open();
        _sheet = XmlWriter.Create(_sheetStream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        _sheet.WriteStartDocument(standalone: true);
        _sheet.WriteStartElement("worksheet", MainNamespace);
        _sheet.WriteRaw("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        _sheet.WriteStartElement("sheetData", MainNamespace);

        _rowNumber = 1;
        StartRow();
        for (var i = 0; i < columns.Count; i++) TextCell(i, columns[i].Name, HeaderStyle);
        _sheet.WriteEndElement();
    }

    public void Row(object?[] values)
    {
        var sheet = _sheet ?? throw new InvalidOperationException("Begin must be called before Row.");
        if (_rowNumber > MaxRows) throw new InvalidOperationException($"Excel sheets hold at most {MaxRows} rows besides the header. Export to CSV instead.");

        _rowNumber++;
        StartRow();
        for (var i = 0; i < values.Length && i < _columnNames.Length; i++) Cell(i, values[i]);
        sheet.WriteEndElement();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_sheet is null) Begin([]);
        _sheet!.WriteEndElement(); // sheetData
        _sheet.WriteEndElement(); // worksheet
        _sheet.WriteEndDocument();
        _sheet.Dispose();
        _sheetStream!.Dispose();
        _zip.Dispose();
    }

    private void StartRow()
    {
        _sheet!.WriteStartElement("row", MainNamespace);
        _sheet.WriteAttributeString("r", _rowNumber.ToString(CultureInfo.InvariantCulture));
    }

    private void Cell(int column, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case bool boolean:
                RawCell(column, "b", null, boolean ? "1" : "0");
                return;
            case byte or sbyte or short or ushort or int or uint:
                RawCell(column, null, null, ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            case long or ulong or decimal or double or float when Number(value) is { } number:
                RawCell(column, null, null, number);
                return;
            case DateTime dateTime when dateTime >= FirstExactDate:
                RawCell(column, null, dateTime.TimeOfDay == TimeSpan.Zero ? DateStyle : DateTimeStyle, dateTime.ToOADate().ToString("R", CultureInfo.InvariantCulture));
                return;
            default:
                TextCell(column, ValueFormatter.Format(value), style: null);
                return;
        }
    }

    /// <summary>The value as Excel would store it exactly, or null when it must stay text to keep every digit.</summary>
    private static string? Number(object value) => value switch
    {
        long l when Math.Abs((decimal)l) <= MaxExactInteger => l.ToString(CultureInfo.InvariantCulture),
        ulong u when u <= MaxExactInteger => u.ToString(CultureInfo.InvariantCulture),
        decimal d when RoundTrips(d) => d.ToString(CultureInfo.InvariantCulture),
        double d when double.IsFinite(d) => d.ToString("R", CultureInfo.InvariantCulture),
        float f when float.IsFinite(f) => f.ToString("R", CultureInfo.InvariantCulture),
        _ => null,
    };

    private static bool RoundTrips(decimal value)
    {
        try
        {
            return (decimal)(double)value == value;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private void RawCell(int column, string? type, string? style, string value)
    {
        var sheet = _sheet!;
        sheet.WriteStartElement("c", MainNamespace);
        sheet.WriteAttributeString("r", Reference(column));
        if (type is not null) sheet.WriteAttributeString("t", type);
        if (style is not null) sheet.WriteAttributeString("s", style);
        sheet.WriteElementString("v", MainNamespace, value);
        sheet.WriteEndElement();
    }

    private void TextCell(int column, string text, string? style)
    {
        var sheet = _sheet!;
        sheet.WriteStartElement("c", MainNamespace);
        sheet.WriteAttributeString("r", Reference(column));
        sheet.WriteAttributeString("t", "inlineStr");
        if (style is not null) sheet.WriteAttributeString("s", style);
        sheet.WriteStartElement("is", MainNamespace);
        sheet.WriteStartElement("t", MainNamespace);
        sheet.WriteAttributeString("xml", "space", null, "preserve");
        sheet.WriteString(CellText(text));
        sheet.WriteEndElement();
        sheet.WriteEndElement();
        sheet.WriteEndElement();
    }

    private string Reference(int column) => _columnNames[column] + _rowNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>Drops characters XML 1.0 cannot hold and cuts the text at the Excel cell limit.</summary>
    private static string CellText(string text)
    {
        if (text.Length <= MaxCellText && text.All(XmlConvert.IsXmlChar) && !HasSurrogates(text)) return text;

        var builder = new StringBuilder(Math.Min(text.Length, MaxCellText));
        for (var i = 0; i < text.Length && builder.Length < MaxCellText; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                if (builder.Length + 2 > MaxCellText) break;
                builder.Append(c).Append(text[++i]);
            }
            else if (XmlConvert.IsXmlChar(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // XmlConvert.IsXmlChar rejects each half of a surrogate pair, so text with any goes through the slow path.
    private static bool HasSurrogates(string text) => text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') >= 0;

    /// <summary>A, B, … Z, AA, AB, …</summary>
    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }

    private void WritePart(string name, string xml)
    {
        using var stream = _zip.CreateEntry(name, CompressionLevel.Fastest).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(xml);
    }

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
        """;

    private const string RootRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
        """;

    private const string Workbook = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Results" sheetId="1" r:id="rId1"/></sheets></workbook>
        """;

    private const string WorkbookRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
        """;

    // Formats match the grid: yyyy-mm-dd, and yyyy-mm-dd hh:mm:ss when there is a time.
    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="yyyy-mm-dd"/><numFmt numFmtId="165" formatCode="yyyy-mm-dd hh:mm:ss"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
        """;
}
