---
title: How scripts run
description: Batch splitting, the script runner, the row limit, server messages, cancellation and streaming exports.
---

## 1. Splitting into batches

SQL Server runs one *batch* at a time; `GO` is not T-SQL but a separator understood by client tools. lazytsql's
`BatchSplitter` (`LazyTsql.Core/Batches`) does what SSMS and sqlcmd do:

- A line that is only `GO`, optionally followed by a count and a comment, ends a batch:
  `^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?$`, case-insensitive.
- It only counts when the splitter is **outside** a block comment (including nested ones), a `'string'`, a
  `"quoted name"` or a `[bracketed name]`, which it tracks across lines.
- `GO 5` gives the batch a repeat count of 5.
- Each batch remembers its `StartLine` (1-based) in the script, so messages can point at the right line.
- Batches that are only whitespace are dropped; line endings are kept as written.

## 2. Running batches

`ScriptRunner` runs each batch `RepeatCount` times through `IDatabaseSession.ExecuteAsync` and yields a
`BatchResult` per run: result sets, messages, rows affected and elapsed time. **It stops after the first batch with
an error**, like `sqlcmd -b`: an error is any message with severity above 10.

## 3. Reading results with a limit

`QueryOptions.MaxRows` (1000 by default, 0 for every row) caps each result set. `SqlServerSession` reads rows until
the cap and marks the result set as truncated. What happens with the rest depends on the batch:

- **A lone `SELECT`** (`SqlBatch.IsSingleQuery`): lazytsql calls `SqlCommand.Cancel()`, which tells the server to
  stop producing rows, and ignores the cancellation error when closing the reader. A `SELECT *` on a table of
  millions of rows returns in milliseconds.
- **Anything else**: the remaining rows are read and discarded, so later statements of the batch (an `INSERT` after
  a `SELECT`, for example) still run. Cancelling there would abort them.

`IsSingleQuery` is a conservative token check: the batch must start with an unbracketed `SELECT`; any statement word
(`INSERT`, `UPDATE`, `DELETE`, `MERGE`, `EXEC`, `INTO`, `SET`, `DECLARE`…) makes it false; a `;` is only allowed at
the end; another `SELECT` is allowed only inside parentheses or after `UNION`, `EXCEPT`, `INTERSECT` or `ALL`. When
in doubt it answers false, which is always safe.

## 4. Messages and line numbers

SqlClient reports `PRINT` and low-severity messages through `InfoMessage`, and errors through `SqlException`; both
become `ServerMessage(Text, Number, Severity, LineNumber)`. The server's line number is relative to the batch, so
every writer converts it to a script line with `StartLine + max(LineNumber, 1) - 1`. That is why errors in the TUI,
the text output and the JSON `line` field point at the line you wrote.

## 5. Cancellation

The cancellation token reaches `ExecuteReaderAsync` and `ReadAsync`, so `Ctrl+K` (or `Ctrl+C` in the CLI) stops a
running query on the server. In the TUI, work waits for a one-slot semaphore because a session has a single
connection; cancelling a query releases it for the next operation.

## 6. Streaming exports

When you export a capped result set to CSV, lazytsql does not have the rows. `StreamResultSetAsync` runs the batch
again, skips to the same result-set index, and hands every row to an `IRowSink` (`CsvRowWriter`) as it arrives, with
no row limit. Memory stays flat whatever the size. The TUI asks before doing this because the batch's side effects
run again, refuses if the connection changed since the run, and deletes the partial file when cancelled or failed.

## 7. Memory

Before each run the previous results are released; after a result of 100,000 rows or more the TUI asks the garbage
collector to compact the heap and give the memory back to the operating system. Cells are formatted only when the
table draws them.
