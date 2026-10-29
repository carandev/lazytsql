---
title: Development
description: Build, test and run lazytsql from source, and how the code is organized.
---

## Build and run

```bash
git clone https://github.com/carandev/lazytsql.git
cd lazytsql
dotnet build
dotnet run --project src/LazyTsql -- -c 'Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=true'
```

`dotnet run` uses the JIT, which is fine for development; `dotnet publish -r <rid>` produces the native binary.

## Tests

```bash
dotnet test
```

| Project | Covers |
| --- | --- |
| `LazyTsql.Core.Tests` | Batch splitting, completion, diagnostics, `IsSingleQuery` |
| `LazyTsql.Tests` | CLI parsing and commands (with fakes), output writers, files, activity log, update check, TUI logic |
| `LazyTsql.SqlServer.Tests` | The SqlClient session and sqlpackage against a real server |

The SQL Server tests are skipped unless `LAZYTSQL_TEST_CONNECTION` points at a disposable server. The `.bacpac`
round trip also needs sqlpackage:

```bash
docker run -d --name lazytsql-dev-test -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=LazyTsql_Dev_2026!' \
  -p 14339:1433 mcr.microsoft.com/mssql/server:2022-latest
export LAZYTSQL_TEST_CONNECTION='Server=localhost,14339;User Id=sa;Password=LazyTsql_Dev_2026!;TrustServerCertificate=true'
dotnet test
docker rm -f lazytsql-dev-test
```

The tests create their own databases with random names and drop them afterwards; still, never point them at a
server you care about.

## Code map

```text
src/
  LazyTsql.Core/         domain and ports, no dependencies
    Batches/             GO splitting, IsSingleQuery
    Execution/           ScriptRunner, results and messages
    Completion/          lexer, SqlCompletion, SchemaCatalog
    Syntax/              highlighting classifier
    Schema/              DbObject, columns, foreign keys
    Connections/         ConnectionProfile, ${VAR} expansion
  LazyTsql.SqlServer/    SqlServerSession, SqlPackageTool
  LazyTsql/
    Program.cs           entry point and wiring
    Cli/                 arguments and headless commands
    Output/              text, JSON and CSV writers
    Infrastructure/      connections file, scripts, activity log, update check
    Tui/                 Terminal.Gui views and dialogs
website/                 this site (Astro Starlight)
```

## This website

```bash
cd website
npm ci
npm run dev       # http://localhost:4321/lazytsql/
npm run build     # static site in website/dist
```

Pages are Markdown files in `website/src/content/docs/`, with the Spanish translation of each one at the same path under `es/`. The screens on the landing page are real lazytsql output,
captured from a terminal with `tmux capture-pane -e` and converted to HTML.
