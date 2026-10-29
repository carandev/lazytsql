---
title: Results and export
description: Result sets, the row limit, memory, and exporting every row to CSV.
---

## Result sets

Every result set of the last run is kept; the results panel shows one at a time. `[` and `]` move between them and
the title shows the position (`2/3`) and the row count. Cells are shown on one line: line breaks become `⏎`, tabs
become spaces and `NULL` is written as `NULL`.

Row counts (`(3 rows affected)`), `PRINT` output and errors go to the messages panel, with the time each run took.

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
