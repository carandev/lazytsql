---
title: The TUI
description: The six panels, how focus and keys work, and how to start lazytsql on a connection, a script or a folder.
---

## Starting it

```bash
lazytsql                        # pick or create a connection in panel [1]
lazytsql -c dev                 # connect on start (a saved name or a raw connection string)
lazytsql -c dev migration.sql   # edit a script (created on the first save if it does not exist)
lazytsql -c dev ./scripts       # browse a folder of scripts in panel [2]
```

Without `-c`, lazytsql connects on start when `LAZYTSQL_CONNECTION` is set.

## Layout

```text
┌ [1] Connections ┐┌ [4] Query · file.sql · dev › shop ───────────┐
│                 ││                                              │
├ [2] Files ──────┤└──────────────────────────────────────────────┘
│                 │┌ [5] Results · 5 rows ────────────────────────┐
├ [3] Schema ─────┤│                                              │
│                 │└──────────────────────────────────────────────┘
│                 │┌ [6] Messages ────────────────────────────────┐
└─────────────────┘└──────────────────────────────────────────────┘
 key hints for the focused panel                     │ global keys
```

| Panel | Shows | Title |
| --- | --- | --- |
| `[1]` Connections | Saved connections, sorted by name | |
| `[2]` Files | Folders and `.sql` files of one directory | |
| `[3]` Schema | Databases → Tables / Views / Procedures / Functions → columns | |
| `[4]` Query | The SQL editor | The open file (`*` when unsaved), the connection and current database |
| `[5]` Results | One result set at a time | Its position (`2/3 [ ]`) and row count, `first N rows` when capped by the row limit |
| `[6]` Messages | Server messages, errors and lazytsql's own messages | `· update available` when there is a newer release |

## Focus and keys

- `1`-`6` focus a panel; `Tab` goes to the next one. Inside the editor, digits are text, so use `Alt+1`-`Alt+6`.
- `Esc` leaves the editor and focuses the results.
- Outside the editor, vim motions work everywhere: `h` `j` `k` `l`, `g`/`G` for top and bottom, `Ctrl+D`/`Ctrl+U`
  to page.
- The bottom line always shows the keys of the focused panel or open dialog, followed by the global keys that still
  work there. `?` (or `F1` inside the editor) opens the help for the current panel; `a` in the help switches to every
  key.
- `q` quits outside the editor, `Ctrl+Q` anywhere. Quitting always asks, and warns when the open script has unsaved
  changes.

Every key is listed in the [key reference](/lazytsql/reference/keys/).

## Working in the background

Queries, schema loading, exports and imports run in the background, so the screen stays responsive. They share the
one connection, so they run one at a time, in order. `Ctrl+K` cancels the running query, export or import.

## Colors

The UI keeps the terminal's background and uses its 16 ANSI colors, so it follows your terminal theme: accent green,
muted gray, selection blue, and unknown names underlined in red.

## File picker

Every path (open, save, export) goes through a file picker:

- `Tab` completes the typed path; `~` is your home directory.
- The list below shows the folders and matching files, filtering as you type. Hidden entries appear when you type
  the leading dot.
- `↓` moves into the list, where `j`/`k` move, `Enter`/`l` opens a folder or picks a file, `h`/`Backspace` goes up,
  and `k` on the first row returns to the path.
- A name without extension gets the expected one (`.sql`, `.csv`, `.bacpac`).
