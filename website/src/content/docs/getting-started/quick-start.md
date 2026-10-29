---
title: Quick start
description: Save a connection, open the TUI, run a query and find your way around in five minutes.
---

## 1. Save a connection

Keep the password in an environment variable so it never lands in the file:

```bash
export DEV_DB_PASSWORD='your-password'
lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
```

`${DEV_DB_PASSWORD}` is replaced when lazytsql connects, not when it saves. You can also add connections from the
TUI: run `lazytsql`, press `a` in the connections panel and fill in the form.

## 2. Open the TUI

```bash
lazytsql -c dev
```

The screen has six numbered panels. Press a number to jump to one:

| Key | Panel | What it shows |
| --- | --- | --- |
| `1` | Connections | Saved connections; `Enter` connects |
| `2` | Files | The `.sql` scripts of a folder |
| `3` | Schema | Databases, tables, views, procedures and functions |
| `4` | Query | The SQL editor |
| `5` | Results | The rows of the last run, one result set at a time |
| `6` | Messages | Server messages, errors and what lazytsql did |

## 3. Run a query

Press `4`, write a query and press `F5`:

```sql
SELECT TOP (10) name, create_date FROM sys.objects ORDER BY create_date DESC;
```

While you type, the editor completes table and column names from the database. The rows appear in panel `5` and
the messages in panel `6`. `F5` runs the selection if there is one, otherwise the whole script, batch by batch
(`GO` separates batches, as in SSMS).

## 4. Explore

- In the schema panel (`3`), `Enter` expands a node, `s` runs `SELECT TOP (100)` on a table and `e` opens a
  procedure's definition.
- In the results panel (`5`), `[` and `]` switch between result sets and `x` exports the current one to CSV.
- `Esc` leaves the editor; outside it, `h` `j` `k` `l` move like in vim.
- `?` shows the keys of the panel you are in. `q` quits.

## 5. Use it from a script

The same connection works without the TUI:

```bash
lazytsql query -c dev -q "SELECT TOP 5 name FROM sys.tables" --json
```

See [headless mode](/lazytsql/guides/headless/).

## Next steps

- [The TUI in detail](/lazytsql/guides/tui/)
- [Writing queries and completion](/lazytsql/guides/writing-queries/)
- [Every key](/lazytsql/reference/keys/)
