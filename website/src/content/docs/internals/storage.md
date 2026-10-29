---
title: Files and storage
description: How lazytsql stores connections, scripts, the activity log and the update cache.
---

## Connections

`~/.config/lazytsql/connections.json` (or `$XDG_CONFIG_HOME/lazytsql/`) is a JSON array:

```json
[
  {
    "name": "dev",
    "connectionString": "Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD}"
  }
]
```

- Written atomically: a `.tmp` file is created with owner-only permissions (0600), written, then moved over the
  original, so a crash never leaves a half-written file.
- Profiles are kept sorted by name; names are unique, case-insensitively.
- Serialized with a `System.Text.Json` source-generated context, which Native AOT needs.
- `${VAR}` is expanded when connecting, never when saving.

## Folders

`folders.json`, next to `connections.json`, is a JSON array of the absolute paths shown in the files panel, in the
order they were added. Paths are normalized (absolute, no trailing separator) so the same folder is never listed twice,
and the file is replaced atomically like the connections file.

## Scripts

`ScriptFile` reads a script detecting its encoding from the byte order mark (UTF-8 without BOM when there is none)
and remembers whether it used CRLF or LF. The editor works with `\n`; saving converts back to the original line
endings and writes with the original encoding, BOM included. Scripts from SSMS, often UTF-16 or UTF-8 with BOM and
CRLF, round-trip byte for byte. A path that does not exist opens as an empty, new file.

## Activity log

`ActivityLog` appends entries to `<state>/logs/yyyy-MM-dd.log`:

- `<state>` is `$XDG_STATE_HOME/lazytsql` or `~/.local/state/lazytsql` (`%LOCALAPPDATA%\lazytsql` on Windows);
  `LAZYTSQL_LOG_DIR` replaces the logs directory.
- The directory is created with 0700 and files with 0600; writes are serialized with a lock and opened in append
  mode, so the TUI and a CLI command can log at the same time.
- An entry is `yyyy-MM-dd HH:mm:ss.fff zzz [source] text`; the following lines of a multi-line entry are indented by
  four spaces.
- `Write` returns `false` instead of throwing; the TUI reports the first failure once.
- `Prune` deletes daily files older than 30 days on every start.

## Update cache

`<state>/latest-version` holds one line: when NuGet.org was last asked (ISO 8601, UTC) and the latest version it
returned. A line younger than a day answers without network.

## Temporary files

- **sqlpackage response file**: created in the temp directory with `CreateNew` and 0600, holding the connection
  string; always deleted after sqlpackage exits, including on failure or cancel.
- **Partial export**: `name.partial-<guid>.bacpac` next to the target, moved over it only on success, deleted
  otherwise.
- **Partial CSV**: deleted when an export fails or is cancelled.
