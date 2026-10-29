---
title: Configuration
description: Every file and environment variable lazytsql reads or writes.
---

lazytsql has no settings file: it is configured with environment variables, and it keeps two kinds of files.

## Files

| Path | Holds | Permissions (Linux, macOS) |
| --- | --- | --- |
| `$XDG_CONFIG_HOME/lazytsql/connections.json` (default `~/.config/lazytsql/connections.json`) | Saved connections | Owner only (0600) |
| `$XDG_CONFIG_HOME/lazytsql/folders.json` | The folders shown in the files panel | |
| `$XDG_STATE_HOME/lazytsql/logs/<date>.log` (default `~/.local/state/lazytsql/logs/`) | [Activity log](/lazytsql/guides/activity-log/), kept 30 days | Owner only (0600, directory 0700) |
| `$XDG_STATE_HOME/lazytsql/layout` | Panel sizes set with `Alt`+arrows | |
| `$XDG_STATE_HOME/lazytsql/latest-version` | The latest release seen on NuGet.org and when | |

On Windows the state files live in `%LOCALAPPDATA%\lazytsql\` and the connections in
`%USERPROFILE%\.config\lazytsql\connections.json`.

## Environment variables

| Variable | Purpose |
| --- | --- |
| `LAZYTSQL_CONNECTION` | Connection used when `-c` is not given: a saved name or a connection string |
| `${VAR}` in a connection string | Replaced by that variable when connecting, so secrets stay out of `connections.json` |
| `LAZYTSQL_SQLPACKAGE` | Path to sqlpackage when it is not on the `PATH` or in `~/.dotnet/tools` |
| `LAZYTSQL_LOG_DIR` | Write the activity log to this directory instead |
| `LAZYTSQL_NO_UPDATE_CHECK` | Any non-empty value stops the TUI from checking NuGet.org for a newer release |
| `XDG_CONFIG_HOME`, `XDG_STATE_HOME` | Standard base directories for the files above |

## Defaults worth knowing

| Setting | TUI | Command line |
| --- | --- | --- |
| Rows per result set | 1000, change with `m` (`all` for every row) | 1000, change with `--max-rows` (at least 1) |
| Command timeout | None; cancel with `Ctrl+K` | 30 seconds, change with `--timeout` |
| CSV encoding | UTF-8 with BOM (for Excel) | UTF-8 without BOM (for pipes) |
