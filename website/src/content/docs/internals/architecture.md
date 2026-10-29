---
title: Architecture
description: The three projects, the ports between them, and how a key press becomes a query on the server.
---

lazytsql is a .NET 10 application compiled with Native AOT. It follows a ports-and-adapters (hexagonal) layout:
the domain knows nothing about SQL Server or the terminal, and the outer layers plug into it through interfaces.

```text
┌──────────────────────────── LazyTsql (app) ─────────────────────────────┐
│  Program.cs ── CLI (CliArguments, HeadlessCommands) ── Output writers   │
│       │                                                                 │
│       └────── TUI (Terminal.Gui: MainView, SqlEditor, dialogs)          │
│  Infrastructure: connections file, scripts, activity log, update check  │
└───────────────┬───────────────────────────────────────┬─────────────────┘
                │ uses ports                            │ wires adapters
┌───────────────▼──────────── LazyTsql.Core ────────────┴─────────────────┐
│  Ports: IDatabaseConnector, IDatabaseSession, IBacpacTool, IRowSink     │
│  Batches (GO splitting) · Execution (ScriptRunner) · Schema models      │
│  Completion (lexer, SqlCompletion, SchemaCatalog) · Syntax highlighting │
└───────────────▲─────────────────────────────────────────────────────────┘
                │ implements
┌───────────────┴──────────── LazyTsql.SqlServer ─────────────────────────┐
│  SqlServerConnector · SqlServerSession (Microsoft.Data.SqlClient)       │
│  SqlPackageTool (runs sqlpackage)                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Projects

| Project | Depends on | Contains |
| --- | --- | --- |
| `LazyTsql.Core` | nothing | The ports, batch splitting, script execution, schema models, the completion engine and syntax classification. Pure logic, fully unit-tested. |
| `LazyTsql.SqlServer` | Core, Microsoft.Data.SqlClient | The adapters that talk to SQL Server and to sqlpackage. |
| `LazyTsql` | Core, SqlServer, Terminal.Gui | The executable: argument parsing, headless commands, text/JSON/CSV writers, the TUI and local files. |

Each has a test project: `LazyTsql.Core.Tests` (pure logic), `LazyTsql.Tests` (CLI, output, TUI logic, files) and
`LazyTsql.SqlServer.Tests` (integration tests against a real SQL Server, skipped without one).

## Ports

Defined in `LazyTsql.Core`:

| Port | Purpose | Adapter |
| --- | --- | --- |
| `IDatabaseConnector` | Opens a session from a connection string | `SqlServerConnector` |
| `IDatabaseSession` | One open connection: run a batch, stream a result set, change database, read databases, objects, columns, foreign keys, synonyms and definitions | `SqlServerSession` |
| `IBacpacTool` | Export and import `.bacpac` files | `SqlPackageTool` |
| `IRowSink` | Receives streamed rows (`Begin` with the columns, then `Row`) | `CsvRowWriter` |

There is no dependency injection container: `Program.cs` creates the adapters and passes them to the CLI or the TUI.
That keeps startup instant and the AOT binary small, and tests build the same objects with fakes.

## From key press to rows

1. `F5` reaches `MainView.OnGlobalKeyDown`, which calls `RunQuery` with the selection or the whole editor text.
2. `RunInBackground` moves the work off the UI thread and waits for the session gate, because a session has a
   single connection and runs one operation at a time.
3. `ScriptRunner` splits the text into batches with `BatchSplitter` and runs each through
   `IDatabaseSession.ExecuteAsync`.
4. `SqlServerSession` executes the batch with SqlClient, reads each result set up to the row limit, and collects
   `PRINT` output and errors with their line numbers.
5. Each `BatchResult` returns to the UI thread through `App.Invoke`, where the messages are logged and the result
   sets shown in a `TableView`.

The headless `query` command runs the same `ScriptRunner` and passes the results to a text, JSON or CSV writer
instead. Details in [how scripts run](/lazytsql/internals/execution/).

## Technology choices

| Choice | Why |
| --- | --- |
| .NET 10 + Native AOT | A single self-contained binary of about 21 MB that starts instantly and needs no runtime on the server. |
| Microsoft.Data.SqlClient | The official driver: TDS, encryption, `PRINT` and error details with line numbers, cancellation. |
| Terminal.Gui 2 | A mature .NET TUI toolkit with tree, table and text views, dialogs and a layout engine. |
| sqlpackage (external) | Microsoft's own tool is the only reliable `.bacpac` implementation; lazytsql drives it instead of reimplementing it. |
| No reflection-based JSON | `System.Text.Json` source generation and `Utf8JsonWriter`, which AOT supports without trimming warnings. |
