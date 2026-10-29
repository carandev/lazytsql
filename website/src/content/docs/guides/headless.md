---
title: Headless mode
description: Run queries, read the schema and manage connections from scripts, CI jobs and coding agents, with JSON or CSV output and meaningful exit codes.
---

Every command besides the TUI writes to stdout and exits, so lazytsql fits in shell scripts, CI jobs and coding
agents that need to look at a database.

## Queries

```bash
lazytsql query -c dev -q "SELECT TOP 5 * FROM dbo.users" --json
lazytsql query -c dev -f migration.sql                                  # GO batches, stops at the first failing batch
lazytsql query -c dev -q "SELECT * FROM dbo.users" --csv > users.csv    # rows on stdout, messages on stderr
echo "SELECT 1" | lazytsql query -c dev --json                          # SQL from stdin
```

The SQL comes from `-q`, else `-f`, else stdin when it is redirected. Batches run in order and stop at the first one
with an error, exactly as in the TUI.

| Option | Meaning | Default |
| --- | --- | --- |
| `-c`, `--connection` | Saved name or raw connection string | `$LAZYTSQL_CONNECTION` |
| `-d`, `--database` | Database to switch to before running | The connection's |
| `-m`, `--max-rows` | Rows per result set; must be at least 1 | 1000 |
| `-t`, `--timeout` | Command timeout in seconds | 30 |
| `--json` / `--csv` | Output format | Text |

## Output formats

- **Text** (default): result sets as aligned tables (columns capped at 40 characters) and messages in sqlcmd format,
  `Msg 208, Level 16, Line 3: Invalid object name 'nope'.`
- **JSON** (`--json`): one document with every batch, its messages and result sets. The shape is in the
  [JSON reference](/lazytsql/reference/json-output/). Errors that stop the command, such as a failed login, are
  JSON too: `{"success":false,"error":"..."}`.
- **CSV** (`--csv`): rows only on stdout, one block per result set separated by a blank line; messages go to stderr.
  No BOM, so it pipes cleanly into other tools.

## Schema

```bash
lazytsql databases -c dev                         # databases you can access
lazytsql schema -c dev -d app --json              # tables, views, procedures, functions
lazytsql schema -c dev -d app dbo.users --json    # columns of a table or view
lazytsql schema -c dev -d app dbo.get_users       # definition of a procedure or function
lazytsql schema -c dev -d app users               # no schema means dbo
```

## Connections

```bash
lazytsql connections list
lazytsql connections add dev 'Server=...;Password=${DEV_DB_PASSWORD}'
lazytsql connections remove dev
```

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | The SQL ran and at least one batch had an error, or sqlpackage failed |
| `2` | Usage error, unknown command, or the connection failed |

## Using it from a coding agent

`--json` output is meant to be read by programs: line numbers point at the submitted script, numbers stay numbers,
binary values are `0x...` strings, and `truncated` says when a result set hit the row limit. A typical agent loop:

```bash
lazytsql schema -c dev -d app --json                  # discover tables
lazytsql schema -c dev -d app dbo.orders --json       # read columns
lazytsql query -c dev -d app -q "SELECT TOP 20 * FROM dbo.orders" --json
```
