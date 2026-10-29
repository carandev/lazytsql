# lazytsql

A lazygit-style terminal client for SQL Server, plus a headless JSON mode for scripts and coding agents.
Ships as a single Native AOT binary you can copy to the server that hosts the database.

## Build

```bash
dotnet publish src/LazyTsql -c Release -r linux-x64 -o dist   # or win-x64, osx-arm64, linux-arm64
```

The result is `dist/lazytsql` (~21 MB). Runtime requirements on Linux: ICU (`libicu`) and OpenSSL,
present on virtually every distribution.

## Connections

Manage them from the TUI: focus panel `[1]` and press `a` to add, `e` to edit (renaming included),
`d` to delete (Enter cancels the confirmation) and `Enter` to connect. The dialog accepts either a raw connection string or
individual fields (server, port, database, SQL login or integrated authentication, encryption); switch with
`←`/`→` + `Space` on *Mode*. Strings with settings the form cannot show stay in connection string mode. The same profiles are available from the CLI:

```bash
lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
lazytsql connections list
lazytsql connections remove dev
```

Profiles live in `$XDG_CONFIG_HOME/lazytsql/connections.json` (`~/.config/...` by default), created with
owner-only permissions. Reference secrets as `${VAR}` so they stay in the environment instead of the file.

When the connection string names a database (`Database=` / `Initial Catalog=`), the schema tree shows only that
database; otherwise it lists every database you can access.

`-c` accepts a saved name or a raw connection string. Without `-c`, `$LAZYTSQL_CONNECTION` is used.

## TUI

```bash
lazytsql            # pick or create a connection in panel [1]
lazytsql -c dev     # connect on start
lazytsql -c dev migration.sql   # edit a script (created on the first save if it does not exist)
```

| Key | Action |
| --- | --- |
| `1`-`5`, `Alt+1`-`Alt+5`, `Tab` | Focus panel: connections, schema, query, results, messages |
| `F5` / `Ctrl+E` | Run the selection, or the whole script batch by batch (`GO`, `GO n`) |
| `Ctrl+K` | Cancel the running query |
| `Tab` / `Ctrl+Space` | Accept / open the completion list in the editor (`Esc` closes it) |
| `Ctrl+O` / `Ctrl+S` | Open / save a `.sql` file; the title shows `*` while there are unsaved changes |
| `Enter` | Connect (panel 1), expand or collapse (panel 2) |
| `a` / `e` / `d` | Add, edit or delete a connection (panel 1) |
| `s` | `SELECT TOP (100)` from the selected table or view |
| `e` | Open the selected procedure or function definition in the editor |
| `r` | Reload the schema |
| `[` / `]` | Previous / next result set |
| `x` | Export the current result set to CSV with every row (UTF-8 with BOM so Excel keeps accents) |
| `m` | Row limit: how many rows each result set fetches, or `all` |
| `h` `j` `k` `l`, `g` / `G`, `Ctrl+D` / `Ctrl+U` | Vim motions outside the editor (`h`/`l` collapse/expand in the schema) |
| `Esc` | Leave the editor |
| `?` / `F1` | Keys of the focused panel or open dialog; `a` switches to every key (`F1` also works where `?` is text) |
| `q` / `Ctrl+Q` | Quit after confirming (`y`/`Enter` quits, `n`/`Esc` stays; `q` only outside the editor) |

Result sets show the first 1000 rows to keep memory bounded; press `m` in the results panel to fetch more, or `all`.
The limit applies to the next run. When the batch is a single `SELECT`, the server stops once the limit is reached
instead of sending the remaining rows; any other batch reads and discards them, so every statement still runs. Exporting a capped result set runs its batch again and streams every row to the
file without holding them in memory; it asks first, because the batch's side effects run again too, and `Ctrl+K`
cancels it (the partial file is removed).

Every path (open, save, export CSV) goes through a file picker: `Tab` completes the typed path, the list below shows
the folders and matching files of that directory and filters as you type. `↓` moves into the list, where `j`/`k` move,
`Enter`/`l` opens a folder or picks a file, `h`/`Backspace` goes up and `k` on the first row returns to the path.
A name without extension gets `.sql` or `.csv`.

Saving keeps the file's encoding and line endings, so scripts from SSMS (UTF-16 or UTF-8 with BOM, CRLF) round-trip
unchanged. Quitting or replacing the editor contents (`s`, `e`) asks first when the open file has unsaved changes.

The UI keeps the terminal's own background and uses the 16 ANSI colors, so it follows your terminal theme:
gray borders, green border on the focused panel and a footer with the keys of the focused panel or open dialog.
The editor highlights T-SQL keywords and types, strings, comments, numbers and `@variables`, and completes names
from the connected database while you type: tables, views and functions after `FROM`/`JOIN`, procedures after
`EXEC`, the objects of a schema after `schema.`, the columns of an alias or table after `alias.`, and elsewhere the
columns of the statement's tables, their aliases and keywords. Other databases complete too: `db.` lists its
schemas and `db.schema.` its objects (when the connection is not scoped to one database). A `dbo` name is inserted
without its schema unless another schema has an object with the same name. Right after `JOIN` it suggests the
tables related by a foreign key with an alias and the condition (`orders o ON o.customer_id = c.id`), and after `ON`
the condition. Tables and `alias.column` names the database does not have are underlined in red once you leave
the word (temporary tables, CTEs, tables the script creates and system views are never marked). Names load in the background the first time
they are needed, without changing the current database; `r` in the schema panel reloads them.

## Headless mode (scripts and agents)

```bash
lazytsql query -c dev -q "SELECT TOP 5 * FROM dbo.users" --json
lazytsql query -c dev -f migration.sql            # GO batches, stops at the first failing batch
lazytsql query -c dev -q "SELECT * FROM dbo.users" --csv > users.csv   # rows on stdout, messages on stderr
echo "SELECT 1" | lazytsql query -c dev --json
lazytsql schema -c dev -d app --json              # tables, views, procedures, functions
lazytsql schema -c dev -d app dbo.users --json    # columns
lazytsql schema -c dev -d app dbo.get_users       # routine definition
lazytsql databases -c dev
```

Options: `-d/--database`, `-m/--max-rows` (default 1000), `-t/--timeout` seconds (default 30).

Exit codes: `0` success, `1` SQL errors, `2` usage or connection errors.

`query --json` output:

```json
{"success":false,"batches":[{"startLine":3,"elapsedMs":5.9,"recordsAffected":-1,
  "messages":[{"level":"error","number":208,"severity":16,"line":3,"text":"Invalid object name 'nope'."}],
  "resultSets":[{"columns":[{"name":"id","type":"int"}],"rows":[[1]],"truncated":false}]}]}
```

`line` is the line in the submitted script, not in the batch. Numbers and decimals stay JSON numbers;
binary values are `0x...` strings.

## Development

```bash
dotnet test
```

SQL Server integration tests run when `LAZYTSQL_TEST_CONNECTION` points at a disposable server:

```bash
docker run -d --name lazytsql-dev-test -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=LazyTsql_Dev_2026!' \
  -p 14339:1433 mcr.microsoft.com/mssql/server:2022-latest
export LAZYTSQL_TEST_CONNECTION='Server=localhost,14339;User Id=sa;Password=LazyTsql_Dev_2026!;TrustServerCertificate=true'
dotnet test
```

Layout: `LazyTsql.Core` (domain and ports, no dependencies), `LazyTsql.SqlServer` (SqlClient adapter),
`LazyTsql` (CLI, JSON/text output, Terminal.Gui TUI).
