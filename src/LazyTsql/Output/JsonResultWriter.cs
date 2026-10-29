using System.Text.Encodings.Web;
using System.Text.Json;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;

namespace LazyTsql.Output;

/// <summary>Stable machine-readable output for agents; written with Utf8JsonWriter to stay reflection-free under AOT.</summary>
public static class JsonResultWriter
{
    // Relaxed escaping keeps quotes and accents readable for agents; output is never embedded in HTML.
    private static readonly JsonWriterOptions Options = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void WriteBatches(Stream output, IReadOnlyList<BatchResult> results)
    {
        using var json = new Utf8JsonWriter(output, Options);
        json.WriteStartObject();
        json.WriteBoolean("success", results.All(r => !r.HasErrors));
        json.WriteStartArray("batches");
        foreach (var result in results)
        {
            json.WriteStartObject();
            json.WriteNumber("startLine", result.Batch.StartLine);
            json.WriteNumber("elapsedMs", Math.Round(result.Elapsed.TotalMilliseconds, 2));
            json.WriteNumber("recordsAffected", result.RecordsAffected);
            WriteMessages(json, result.Messages, result.Batch.StartLine);
            json.WriteStartArray("resultSets");
            foreach (var resultSet in result.ResultSets) WriteResultSet(json, resultSet);
            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    public static void WriteObjects(Stream output, string database, IReadOnlyList<DbObject> objects)
    {
        using var json = new Utf8JsonWriter(output, Options);
        json.WriteStartObject();
        json.WriteString("database", database);
        json.WriteStartArray("objects");
        foreach (var item in objects)
        {
            json.WriteStartObject();
            json.WriteString("schema", item.Schema);
            json.WriteString("name", item.Name);
            json.WriteString("kind", item.Kind.ToString().ToLowerInvariant());
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    public static void WriteColumns(Stream output, DbObject table, IReadOnlyList<ColumnDefinition> columns)
    {
        using var json = new Utf8JsonWriter(output, Options);
        json.WriteStartObject();
        json.WriteString("schema", table.Schema);
        json.WriteString("name", table.Name);
        json.WriteStartArray("columns");
        foreach (var column in columns)
        {
            json.WriteStartObject();
            json.WriteString("name", column.Name);
            json.WriteString("type", column.DataType);
            json.WriteBoolean("nullable", column.IsNullable);
            json.WriteBoolean("primaryKey", column.IsPrimaryKey);
            json.WriteBoolean("identity", column.IsIdentity);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    public static void WriteError(Stream output, string message)
    {
        using var json = new Utf8JsonWriter(output, Options);
        json.WriteStartObject();
        json.WriteBoolean("success", false);
        json.WriteString("error", message);
        json.WriteEndObject();
    }

    private static void WriteMessages(Utf8JsonWriter json, IReadOnlyList<ServerMessage> messages, int batchStartLine)
    {
        json.WriteStartArray("messages");
        foreach (var message in messages)
        {
            json.WriteStartObject();
            json.WriteString("level", message.IsError ? "error" : "info");
            json.WriteNumber("number", message.Number);
            json.WriteNumber("severity", message.Severity);
            // Server line numbers are relative to the batch; report the line in the submitted script.
            json.WriteNumber("line", batchStartLine + Math.Max(message.LineNumber, 1) - 1);
            json.WriteString("text", message.Text);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }

    private static void WriteResultSet(Utf8JsonWriter json, ResultSet resultSet)
    {
        json.WriteStartObject();
        json.WriteStartArray("columns");
        foreach (var column in resultSet.Columns)
        {
            json.WriteStartObject();
            json.WriteString("name", column.Name);
            json.WriteString("type", column.TypeName);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("rows");
        foreach (var row in resultSet.Rows)
        {
            json.WriteStartArray();
            foreach (var value in row) WriteValue(json, value);
            json.WriteEndArray();
        }

        json.WriteEndArray();
        json.WriteBoolean("truncated", resultSet.IsTruncated);
        json.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null: json.WriteNullValue(); break;
            case bool b: json.WriteBooleanValue(b); break;
            case byte n: json.WriteNumberValue(n); break;
            case short n: json.WriteNumberValue(n); break;
            case int n: json.WriteNumberValue(n); break;
            case long n: json.WriteNumberValue(n); break;
            case float n when float.IsFinite(n): json.WriteNumberValue(n); break;
            case double n when double.IsFinite(n): json.WriteNumberValue(n); break;
            // decimal keeps exact precision as a JSON number literal.
            case decimal n: json.WriteNumberValue(n); break;
            case string s: json.WriteStringValue(s); break;
            case Guid g: json.WriteStringValue(g); break;
            case DateTime d: json.WriteStringValue(d); break;
            case DateTimeOffset d: json.WriteStringValue(d); break;
            default: json.WriteStringValue(ValueFormatter.Format(value)); break;
        }
    }
}
