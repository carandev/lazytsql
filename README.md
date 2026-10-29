# lazytsql

A lazygit-style terminal client for SQL Server: browse the schema, write T-SQL with completion from your database,
run it, page through results and export them, all from the keyboard. It also has a headless mode with JSON output
for scripts and coding agents. It ships as a single native binary you can copy to the server that hosts the database.

**Documentation: <https://carandev.github.io/lazytsql/> · [en español](https://carandev.github.io/lazytsql/es/)**

- [Requirements](#requirements)
- [Install](#install)
- [Quick start](#quick-start)
- [Using the TUI](#using-the-tui)
- [Headless mode (scripts and agents)](#headless-mode-scripts-and-agents)
- [Backups with .bacpac](#backups-with-bacpac)
- [Configuration reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)
- [Development](#development)
- [License](#license)

## Requirements

| What | Needed for | Notes |
| --- | --- | --- |
| A SQL Server you can reach | Everything | SQL Server 2016 or later, or Azure SQL. Tested against SQL Server 2022. |
| ICU (`libicu`) and OpenSSL | Linux only | Present on virtually every distribution. Windows and macOS need nothing. |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Installing with `dotnet tool`, or building | Not needed to run the native binary. |
| [sqlpackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage) | Only `.bacpac` export and import | `dotnet tool install -g microsoft.sqlpackage` |

## Install

Pick one option.

### Option 1: .NET tool (any OS with the .NET 10 SDK)

```bash
dotnet tool install -g lazytsql
lazytsql --version
```

The tool is the native binary of your platform (linux-x64, linux-arm64, win-x64, osx-arm64); other platforms get a
build that runs on the .NET 10 runtime. If `lazytsql` is not found, add `~/.dotnet/tools` (Windows:
`%USERPROFILE%\.dotnet\tools`) to your `PATH`. Update with `dotnet tool update -g lazytsql`.

When a newer release exists, the TUI says so in the messages panel, with the update command for a dotnet tool or the
release page for a downloaded binary. It asks NuGet.org at most once a day, shows nothing when offline, and
`LAZYTSQL_NO_UPDATE_CHECK=1` turns it off.

### Option 2: download the binary (no .NET needed)

Linux (use `lazytsql-linux-arm64.tar.gz` on ARM):

```bash
curl -fsSL https://github.com/carandev/lazytsql/releases/latest/download/lazytsql-linux-x64.tar.gz | tar -xz
sudo mv lazytsql /usr/local/bin/
lazytsql --version
```

macOS (Apple Silicon):

```bash
curl -fsSL https://github.com/carandev/lazytsql/releases/latest/download/lazytsql-osx-arm64.tar.gz | tar -xz
sudo mv lazytsql /usr/local/bin/
lazytsql --version
```

The binary is not signed. If you download it with a browser instead of `curl`, macOS blocks it; allow it with
`xattr -d com.apple.quarantine /usr/local/bin/lazytsql`.

Windows (PowerShell):

```powershell
Invoke-WebRequest https://github.com/carandev/lazytsql/releases/latest/download/lazytsql-win-x64.zip -OutFile lazytsql.zip
Expand-Archive lazytsql.zip -DestinationPath "$env:LOCALAPPDATA\lazytsql"
# Add %LOCALAPPDATA%\lazytsql to your PATH, open a new terminal, then:
lazytsql --version
```

Every release is listed at <https://github.com/carandev/lazytsql/releases>.

### Option 3: build from source

```bash
git clone https://github.com/carandev/lazytsql.git
cd lazytsql
dotnet publish src/LazyTsql -c Release -r linux-x64 -o dist   # or linux-arm64, win-x64, osx-arm64
./dist/lazytsql --version
```

The result is a single file of about 21 MB. Native AOT only compiles for the OS you build on.

## Quick start

1. Save a connection. Keep the password in an environment variable so it never lands in the file:

   ```bash
   export DEV_DB_PASSWORD='your-password'
   lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
   ```

   You can also add it from the TUI: run `lazytsql`, press `a` in the connections panel and fill in the form.

2. Open the TUI connected to it:

   ```bash
   lazytsql -c dev
   ```

3. Write a query in the editor (panel `4`) and press `F5`. The results appear in panel `5` and the server
   messages in panel `6`.

4. Press `?` at any time to see the keys of the panel you are in. `q` quits.

## Using the TUI

```bash
lazytsql                        # pick or create a connection in panel [1]
lazytsql -c dev                 # connect on start
lazytsql -c dev migration.sql   # edit a script (created on the first save if it does not exist)
lazytsql -c dev ./scripts       # browse a folder of scripts in panel [2]
```

The screen has six panels: connections `[1]`, files `[2]`, schema `[3]`, query `[4]`, results `[5]` and messages `[6]`.
The footer always shows the keys of the focused panel or dialog.

The files panel shows the folder given on the command line, else the folder of the script being edited, else the
working directory: its subfolders and `.sql` files, without hidden folders such as `.git`.

### Keys

| Key | Where | Action |
| --- | --- | --- |
| `1`-`6`, `Alt+1`-`Alt+6`, `Tab` | Anywhere | Focus a panel (`Alt+` also works inside the editor) |
| `F5` / `Ctrl+E` | Anywhere | Run the selection, or the whole script batch by batch (`GO`, `GO n`) |
| `Ctrl+K` | Anywhere | Cancel the running query, export or import |
| `Ctrl+O` / `Ctrl+S` | Anywhere | Open / save a `.sql` file; the title shows `*` while there are unsaved changes |
| `?` / `F1` | Anywhere | Keys of the focused panel; `a` in the help switches to every key |
| `q` / `Ctrl+Q` | Anywhere | Quit after confirming (`q` only outside the editor) |
| `h` `j` `k` `l`, `g` / `G`, `Ctrl+D` / `Ctrl+U` | Outside the editor | Vim motions |
| `Enter` | Connections | Connect |
| `a` / `e` / `d` | Connections | Add, edit or delete a connection |
| `Enter` | Files | Open the selected script in the editor (asks first if there are unsaved changes), or expand a folder |
| `h` / `l` | Files | Collapse / expand a folder |
| `r` | Files | Refresh after files change on disk; saving a new script refreshes it too |
| `Enter`, `h` / `l` | Schema | Expand or collapse |
| `s` | Schema | `SELECT TOP (100)` from the selected table or view |
| `e` | Schema | Open the selected procedure or function definition in the editor |
| `r` | Schema | Reload the schema and the completion names |
| `x` / `i` | Schema | Export the selected database to a `.bacpac` / import a `.bacpac` as a new database |
| `Tab` / `Ctrl+Space` | Query | Accept / open the completion list (`Esc` closes it) |
| `Esc` | Query | Leave the editor |
| `[` / `]` | Results | Previous / next result set |
| `x` | Results | Export the current result set to CSV with every row |
| `m` | Results | Row limit: how many rows each result set fetches, or `all` |

### Connections

Panel `[1]` lists the saved profiles. The add/edit dialog accepts either a raw connection string or individual
fields (server, port, database, SQL login or integrated authentication, encryption); switch with `←`/`→` + `Space`
on *Mode*. Strings with settings the form cannot show stay in connection string mode. When the connection names a
database, the schema tree shows only that database; otherwise it lists every database you can access.

### Writing queries

The editor highlights T-SQL and completes names from the connected database while you type:

| You type | It suggests |
| --- | --- |
| `FROM cu`, `JOIN cu` | Tables, views, functions and synonyms (a `dbo` name without its schema unless another schema has the same name) |
| `JOIN ` | Tables related by a foreign key, with an alias and the condition: `orders o ON o.customer_id = c.id` |
| `ON ` | The foreign key condition with the tables joined before |
| `EXEC get` | Procedures |
| `sales.` | The objects of that schema |
| `c.` (an alias or table) | Its columns, with their type |
| `em` elsewhere | Columns of the statement's tables, their aliases and keywords |
| `archive.` / `archive.dbo.` | Schemas / objects of another database |

Tables and `alias.column` names the database does not have are underlined in red once you leave the word. Temporary
tables, CTEs, tables the script creates and system views are never marked. Names load in the background the first
time they are needed, without changing the current database; `r` in the schema panel reloads them.

A synonym behaves like the table or view it points to, in this database or another one on the same server: `c.`
completes that table's columns, `JOIN ` suggests its foreign keys, and its unknown columns are underlined. A synonym
for an object on a linked server is completed by name only.

Saving keeps the file's encoding and line endings, so scripts from SSMS (UTF-16 or UTF-8 with BOM, CRLF) round-trip
unchanged. Quitting or replacing the editor contents asks first when the open file has unsaved changes.

### Results and row limit

Each result set fetches the first 1000 rows to keep memory bounded; press `m` in the results panel to change the
limit or choose `all` (it applies to the next run). One million rows of three columns take about 220 MB, and running
again releases the previous results first. When the batch is a single `SELECT`, the server stops once the limit is
reached; any other batch reads and discards the remaining rows, so every statement still runs.

`x` exports the current result set to CSV (UTF-8 with BOM, so Excel keeps accents). A capped result set is exported
by running its batch again and streaming every row to the file; it asks first, because the batch's side effects run
again too. `Ctrl+K` cancels and removes the partial file.

### File picker

Every path (open, save, export) goes through a file picker: `Tab` completes the typed path and the list below shows
the folders and matching files, filtering as you type. `↓` moves into the list, where `j`/`k` move, `Enter`/`l` opens
a folder or picks a file, `h`/`Backspace` goes up and `k` on the first row returns to the path. A name without
extension gets the expected one (`.sql`, `.csv`, `.bacpac`).

## Headless mode (scripts and agents)

```bash
lazytsql query -c dev -q "SELECT TOP 5 * FROM dbo.users" --json
lazytsql query -c dev -f migration.sql                                  # GO batches, stops at the first failing batch
lazytsql query -c dev -q "SELECT * FROM dbo.users" --csv > users.csv    # rows on stdout, messages on stderr
echo "SELECT 1" | lazytsql query -c dev --json
lazytsql schema -c dev -d app --json              # tables, views, procedures, functions
lazytsql schema -c dev -d app dbo.users --json    # columns
lazytsql schema -c dev -d app dbo.get_users       # routine definition
lazytsql databases -c dev
lazytsql connections list
lazytsql connections remove dev
```

| Option | Meaning |
| --- | --- |
| `-c`, `--connection` | Saved name or raw connection string; defaults to `$LAZYTSQL_CONNECTION` |
| `-d`, `--database` | Database to use |
| `-m`, `--max-rows` | Rows per result set (default 1000) |
| `-t`, `--timeout` | Command timeout in seconds (default 30) |
| `--json` / `--csv` | Output format; text by default |

Exit codes: `0` success, `1` SQL errors, `2` usage or connection errors.

`query --json` output:

```json
{"success":false,"batches":[{"startLine":3,"elapsedMs":5.9,"recordsAffected":-1,
  "messages":[{"level":"error","number":208,"severity":16,"line":3,"text":"Invalid object name 'nope'."}],
  "resultSets":[{"columns":[{"name":"id","type":"int"}],"rows":[[1]],"truncated":false}]}]}
```

`line` is the line in the submitted script, not in the batch. Numbers and decimals stay JSON numbers; binary
values are `0x...` strings.

## Backups with .bacpac

A `.bacpac` holds a whole database, schema and data. lazytsql runs
[sqlpackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage) for it, so install that first:

```bash
dotnet tool install -g microsoft.sqlpackage
```

```bash
lazytsql export -c dev -d app app.bacpac         # the database to a file
lazytsql import -c dev -d app_copy app.bacpac    # the file into a new, empty database
```

In the TUI, select a database in the schema panel and press `x` to export it, or press `i` to import a file; its
progress shows in the messages panel and `Ctrl+K` cancels.

- `-d` defaults to the connection's database. An import needs a database that does not exist or is empty.
- An export is written next to the target and replaces it only when complete, so a failed or cancelled export
  keeps the previous file. A cancelled import may leave a partially imported database.
- The connection string reaches sqlpackage through a temporary file only you can read, never on the command line.
  sqlpackage cannot read a double quote (`"`) there, so a password containing one is refused.
- sqlpackage is found on the `PATH`, in `~/.dotnet/tools`, or at `$LAZYTSQL_SQLPACKAGE`.

## Activity log

Everything lazytsql does is kept on disk so you can look back at it: each SQL script run (TUI and `query`), the
server messages and errors, connections, exports, imports and saved scripts. One file per day:

```bash
cat ~/.local/state/lazytsql/logs/2026-10-28.log
```

```text
2026-10-28 23:01:17.287 -05:00 [tui] Running on dev › shop:
    SELECT name FROM sys.databases
2026-10-28 23:01:17.380 -05:00 [tui] Done in 32 ms.
2026-10-28 23:01:10.807 -05:00 [cli] ✗ Login failed for user 'sa'.
```

- Files are readable only by you and are deleted after 30 days.
- Connection strings are never written; entries name the saved connection or the server. The SQL itself is logged as
  run, so avoid literal secrets in scripts you run.
- On Windows the logs live in `%LOCALAPPDATA%\lazytsql\logs`.

## Configuration reference

| Setting | Purpose |
| --- | --- |
| `$XDG_CONFIG_HOME/lazytsql/connections.json` (default `~/.config/lazytsql/connections.json`) | Saved connections, created with owner-only permissions |
| `${VAR}` inside a connection string | Replaced by that environment variable when connecting, so secrets stay out of the file |
| `LAZYTSQL_CONNECTION` | Connection used when `-c` is not given |
| `LAZYTSQL_SQLPACKAGE` | Path to sqlpackage when it is not on the `PATH` or in `~/.dotnet/tools` |
| `$XDG_STATE_HOME/lazytsql/logs` (default `~/.local/state/lazytsql/logs`) | Activity log, one file per day, kept 30 days |
| `LAZYTSQL_LOG_DIR` | Write the activity log to another directory |
| `LAZYTSQL_NO_UPDATE_CHECK` | Set to `1` to stop the TUI from checking NuGet.org for a newer release |

The UI keeps the terminal's background and uses its 16 ANSI colors, so it follows your terminal theme.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `lazytsql: command not found` after `dotnet tool install` | Add `~/.dotnet/tools` to your `PATH` and open a new terminal. |
| `Globalization Invariant Mode is not supported` or ICU errors on Linux | Install ICU: `sudo apt install libicu-dev` (Debian/Ubuntu) or `sudo dnf install libicu` (Fedora). |
| `The certificate chain was issued by an authority that is not trusted` | Add `TrustServerCertificate=true` to the connection string (development servers with self-signed certificates). |
| `sqlpackage not found` | `dotnet tool install -g microsoft.sqlpackage`, or set `LAZYTSQL_SQLPACKAGE` to its path. |
| Import fails with `contains one or more user objects` | Import into a new database name; sqlpackage only imports into an empty database. |

Integrated (Windows) and Microsoft Entra ID authentication are not yet verified in the native binary.

## Development

```bash
dotnet test
```

SQL Server integration tests run when `LAZYTSQL_TEST_CONNECTION` points at a disposable server; the `.bacpac` round
trip also needs sqlpackage:

```bash
docker run -d --name lazytsql-dev-test -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=LazyTsql_Dev_2026!' \
  -p 14339:1433 mcr.microsoft.com/mssql/server:2022-latest
export LAZYTSQL_TEST_CONNECTION='Server=localhost,14339;User Id=sa;Password=LazyTsql_Dev_2026!;TrustServerCertificate=true'
dotnet test
```

Layout: `LazyTsql.Core` (domain and ports, no dependencies), `LazyTsql.SqlServer` (SqlClient and sqlpackage
adapters), `LazyTsql` (CLI, JSON/text output, Terminal.Gui TUI).

### Releases

Every push to `main` runs `.github/workflows/release.yml`:

1. The test suite against a SQL Server container, with sqlpackage installed.
2. A native AOT build on Linux (x64 and arm64), Windows and macOS (arm64), each on its own OS.
3. Version `0.1.<run number>` published as a GitHub release with `lazytsql-<platform>` archives, and as the
   `lazytsql` .NET tool on NuGet.org (one package per platform plus the one users install).

Publishing to NuGet.org needs the repository secret `NUGET_API_KEY`.

## License

[MIT](LICENSE)
