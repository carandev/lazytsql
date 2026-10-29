---
title: Results and export
description: Result sets, editing cells, the row limit, memory, and exporting every row to CSV.
---

## Result sets

Every result set of the last run is kept; the results panel shows one at a time. `[` and `]` move between them and
the title shows the position (`2/3`) and the row count. Cells are shown on one line: line breaks become `⏎`, tabs
become spaces and `NULL` is written as `NULL`.

Row counts (`(3 rows affected)`), `PRINT` output and errors go to the messages panel, with the time each run took.

## Editing cells

The results of a lone `SELECT` can be edited in place and saved with a script you review first, like in DataGrip.

1. Put the cursor on a cell and press `e` (or `Enter`). Type the new value; `Ctrl+N` sets `NULL`. The value is
   checked against the column's type, so a date must be `yyyy-MM-dd` and a number cannot be text.
2. The new value shows in yellow and the title counts the changes: `3 rows · 2 unsaved changes (s save)`. `u` undoes
   the selected cell and `U` every change.
3. `s` opens a confirmation with the exact script that runs. `y` runs it; `n`, `Esc` or `Enter` cancel.

```sql
IF @@TRANCOUNT > 0 THROW 50000, N'A transaction is open in this session. Commit or roll it back before saving edits.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE [shop].[dbo].[customers] SET [name] = N'Anna' WHERE [id] = 1;
    IF @@ROWCOUNT <> 1 THROW 50000, N'Row 1 no longer matches exactly one row of [shop].[dbo].[customers]. Nothing was saved.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

### What can be edited

The first time you edit, lazytsql asks SQL Server where each column comes from
(`sp_describe_first_result_set` in browse mode), compiling the query without running it again. A cell can change
when its column is read from a table and the result includes **every column of that table's primary or unique key**,
so each change names exactly one row. A query on a view edits the view's base table.

Read-only, with the reason in the messages panel:

- Columns computed by the query (`UPPER(name)`, `COUNT(*)`, literals).
- Identity, computed and rowversion columns.
- Tables without a primary or unique key, or whose key columns the query leaves out.
- A table the query reads more than once (a self-join, or a subquery on the same table): its rows cannot be told
  apart.
- Types without a literal form: `geography`, `geometry`, `hierarchyid`, `sql_variant`.
- Results of a batch with more than one statement, and values with line breaks.

### Saving

- Every changed row becomes one `UPDATE` by its key, with the table's full name.
- The script is one transaction. If a row was deleted or its key changed since the query ran, the `UPDATE` matches
  no row, the transaction rolls back and **nothing is saved**.
- On any error, the changes stay pending, so you can fix them or undo them.
- Saving is refused while a transaction you opened (`BEGIN TRANSACTION`) is open in the session: rolling back the
  save would undo your work too. Commit or roll it back first.
- The script is written to the [activity log](/lazytsql/guides/activity-log/).
- Running another query or quitting with unsaved edits asks first.

## Row limit

Each result set fetches the first **1000 rows** to keep memory bounded. Press `m` in the results panel to change the
limit, or type `all` (also `0` or empty) for every row; it applies to the next run. The title says `first 1000 rows`
when a result set was capped.

What happens at the limit depends on the batch:

- When the batch is **a single `SELECT`**, lazytsql asks the server to stop, so a huge table costs nothing beyond
  the rows you see.
- Any other batch reads and discards the remaining rows, so every statement after it still runs.

Running again releases the previous results first. As a reference, one million rows of three columns take about
220 MB.

## Exporting to CSV

`x` in the results panel exports the current result set to a CSV file:

- UTF-8 **with BOM**, so Excel keeps accents; RFC 4180 quoting; `NULL` is an empty field.
- If the result set was **not** capped, the rows on screen are written.
- If it **was** capped, lazytsql runs its batch again and streams every row straight to the file, without holding
  them in memory. It asks first, because the batch's side effects run again too.
- `Ctrl+K` cancels and removes the partial file.
- If you connected somewhere else since the run, the export is refused: run the query again.

From the command line, `--csv` writes rows to stdout; see [headless mode](/lazytsql/guides/headless/).
