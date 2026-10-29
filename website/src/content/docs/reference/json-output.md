---
title: JSON output
description: The exact shape of lazytsql's --json output for queries, objects, columns and errors.
---

All JSON is a single document on stdout, UTF-8, without escaping non-ASCII characters.

## `query --json`

```json
{
  "success": false,
  "batches": [
    {
      "startLine": 3,
      "elapsedMs": 5.9,
      "recordsAffected": -1,
      "messages": [
        { "level": "error", "number": 208, "severity": 16, "line": 3, "text": "Invalid object name 'nope'." }
      ],
      "resultSets": [
        { "columns": [{ "name": "id", "type": "int" }], "rows": [[1]], "truncated": false }
      ]
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `success` | `false` when any batch had an error (severity above 10) |
| `batches` | The batches that ran, in order; execution stops after the first one with an error |
| `startLine` | The line where the batch starts in the submitted script (1-based) |
| `elapsedMs` | How long the batch took, in milliseconds |
| `recordsAffected` | Rows changed by `INSERT`/`UPDATE`/`DELETE`/`MERGE`, or `-1` when none ran |
| `messages[].level` | `error` (severity above 10) or `info` (`PRINT`, `RAISERROR` with low severity) |
| `messages[].line` | The line in the **submitted script**, not in the batch |
| `resultSets[].columns` | Column names and SQL Server types |
| `resultSets[].rows` | Rows as arrays, in column order |
| `resultSets[].truncated` | `true` when the result set hit `--max-rows` |

### Values

| SQL Server type | JSON |
| --- | --- |
| `NULL` | `null` |
| `bit` | `true` / `false` |
| integers, `decimal`, `numeric`, `money`, `float`, `real` | number (decimals keep their exact digits) |
| `uniqueidentifier`, strings | string |
| `date`, `datetime`, `datetime2`, `datetimeoffset` | ISO 8601 string |
| `binary`, `varbinary`, `image` | `"0x..."` string |
| anything else | its text form as a string |

## `schema --json`

Objects of a database:

```json
{ "database": "app", "objects": [{ "schema": "dbo", "name": "users", "kind": "table" }] }
```

`kind` is `table`, `view`, `procedure` or `function`.

Columns of a table or view (`schema dbo.users --json`):

```json
{
  "schema": "dbo",
  "name": "users",
  "columns": [{ "name": "id", "type": "int", "nullable": false, "primaryKey": true, "identity": true }]
}
```

`type` includes length, precision and scale: `nvarchar(100)`, `decimal(10,2)`, `varchar(max)`.

## Errors

An error that stops the command (bad usage, failed connection) replaces the whole document:

```json
{ "success": false, "error": "Login failed for user 'sa'." }
```

The exit code tells the two apart: `1` for SQL errors inside `batches`, `2` for this form.
