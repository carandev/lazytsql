---
title: Activity log
description: A private daily log of every script lazytsql ran, its messages, connections, exports and imports.
---

lazytsql keeps a history of what it did, so you can find the query you ran last Tuesday or see exactly what failed.

## Where

One file per day:

| OS | Directory |
| --- | --- |
| Linux, macOS | `~/.local/state/lazytsql/logs/` (or `$XDG_STATE_HOME/lazytsql/logs/`) |
| Windows | `%LOCALAPPDATA%\lazytsql\logs\` |

Set `LAZYTSQL_LOG_DIR` to write somewhere else.

```bash
cat ~/.local/state/lazytsql/logs/2026-10-28.log
grep -A3 "Running on prod" ~/.local/state/lazytsql/logs/*.log
```

## What it holds

```text
2026-10-28 23:01:11.298 -05:00 [tui] Connecting to dev…
2026-10-28 23:01:11.683 -05:00 [tui] Connected to dev (SQL Server 16.00.4305), database shop.
2026-10-28 23:01:17.287 -05:00 [tui] Running on dev › shop:
    SELECT name FROM sys.databases
2026-10-28 23:01:17.380 -05:00 [tui] Done in 32 ms.
2026-10-28 23:01:10.807 -05:00 [cli] ✗ Login failed for user 'sa'.
```

- **From the TUI** (`[tui]`): every message of the messages panel, and the SQL of each run with the connection and
  database it ran on.
- **From the command line** (`[cli]`): each `query` with its SQL, messages and exit code; exports and imports;
  connections added or removed; and errors that stopped a command.

Each entry starts with the local time and its offset; the lines of a multi-line entry are indented.

## Privacy

- Files and the directory are readable only by you (0600 and 0700).
- **Connection strings are never written.** Entries name the saved connection or the server, because a connection
  string may hold a password.
- The SQL is written as run. Avoid literal secrets in scripts (`CREATE LOGIN ... WITH PASSWORD = '...'`), or point
  `LAZYTSQL_LOG_DIR` somewhere you clean up.
- Logs older than **30 days** are deleted when lazytsql starts.

If the log cannot be written (a full disk, no permission), the TUI says so once in the messages panel and carries
on; logging never stops your work.
