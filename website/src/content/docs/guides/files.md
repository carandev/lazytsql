---
title: Files panel
description: Browse a folder of .sql scripts and open them in the editor.
---

The files panel (`2`) shows a folder of scripts as a tree, so a project's queries, migrations or reports are one
key away.

## Which folder

1. The folder given on the command line: `lazytsql -c dev ./scripts`.
2. Otherwise, the folder of the script given on the command line: `lazytsql -c dev scripts/report.sql`.
3. Otherwise, the working directory.

The tree shows subfolders first and then `.sql` files (any case: `.SQL` too), each sorted by name. Hidden folders
such as `.git` are left out, and a folder that cannot be read shows as empty.

## Keys

| Key | Action |
| --- | --- |
| `Enter` | Open the selected script in the editor, or expand/collapse a folder |
| `l` / `h` | Expand / collapse a folder |
| `j` / `k` | Move |
| `/` | Search, like vim (see below) |
| `n` / `N` | Next / previous match of the last search |
| `r` | Refresh after files change on disk |

Opening a script asks first when the editor has unsaved changes. Saving a new script with `Ctrl+S` refreshes the
tree, and a refresh keeps the same folders expanded.

## Searching

`/` searches folders and scripts **at any depth**, not only the visible ones, as in vim:

1. Type part of a name; the first match is selected as you type, and the folders above it expand. The title shows
   `/query 1/3`, or `no match`.
2. `Enter` keeps the match; `Esc` goes back to where you were. `Backspace` deletes a character, and on an empty
   search cancels it.
3. `n` and `N` then move to the next and previous match, wrapping around.

The search ignores case and hidden folders, and stops after 20,000 entries so a search from a huge directory stays
fast.
