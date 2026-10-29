---
title: Command line
description: Every lazytsql command and option, as printed by lazytsql --help.
---

```text
lazytsql [-c <connection>] [<file.sql> | <dir>]  Open the TUI, optionally editing a script or browsing a folder of scripts
lazytsql query  [-c <conn>] [-d <db>] (-q <sql> | -f <file> | stdin) [--json | --csv] [--max-rows N] [--timeout S]
lazytsql schema [-c <conn>] [-d <db>] [<schema.object>] [--json]
lazytsql databases [-c <conn>]
lazytsql export [-c <conn>] [-d <db>] <file.bacpac>     Export a database with sqlpackage
lazytsql import [-c <conn>] [-d <new db>] <file.bacpac> Import a .bacpac as a new database
lazytsql connections [list | add <name> <connection string> | remove <name>]
lazytsql --version
lazytsql --help
```

## Commands

| Command | Does | Output |
| --- | --- | --- |
| *(none)* | Opens the TUI. A `.sql` path opens it in the editor; a directory opens it in the files panel. | |
| `query` | Runs SQL from `-q`, `-f` or stdin, batch by batch, stopping at the first failing batch | Text, `--json` or `--csv` |
| `schema` | Lists objects; with `<schema.object>`, a table's columns or a routine's definition | Text or `--json` (definitions are always text) |
| `databases` | Lists the databases you can access | Text |
| `export` | Exports a database to `.bacpac` with sqlpackage | Progress on stderr |
| `import` | Imports a `.bacpac` into a new database with sqlpackage | Progress on stderr |
| `connections` | Lists, adds or removes saved connections | Text |

## Options

| Option | Commands | Meaning |
| --- | --- | --- |
| `-c`, `--connection` | all | A saved name (case-insensitive) or a raw connection string. Defaults to `$LAZYTSQL_CONNECTION`. |
| `-d`, `--database` | query, schema, export, import | The database to use. For export/import it defaults to the connection's. |
| `-q`, `--query` | query | The SQL to run |
| `-f`, `--file` | query | A file with the SQL to run |
| `-m`, `--max-rows` | query | Rows per result set, at least 1 (default 1000) |
| `-t`, `--timeout` | query | Command timeout in seconds (default 30) |
| `--json` | query, schema | JSON output, see [JSON output](/lazytsql/reference/json-output/) |
| `--csv` | query | Rows as CSV on stdout, messages on stderr |
| `--version` | | Prints the version |
| `-h`, `--help` | | Prints the usage |

`--json` and `--csv` cannot be combined.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | SQL errors in at least one batch, or sqlpackage failed |
| `2` | Usage error, unknown command, or connection error |

When `--json` is given, an error that stops the command is printed as `{"success":false,"error":"<message>"}` on
stdout; otherwise it is printed as `lazytsql: <message>` on stderr.
