---
title: Files panel
description: Browse a folder of .sql scripts and open them in the editor.
---

The files panel (`2`) shows a folder of scripts as a tree, so a project's queries, migrations or reports are one
key away.

## Your folders

The panel shows the folders you add, one tree each, and **remembers them**: they are there every time you open
lazytsql, until you remove them.

- `o` adds a folder: type or browse to it in the picker (`Enter` on a folder in the list goes into it) and press
  `Enter` on the path, or *Add*.
- A folder given on the command line is added and remembered too: `lazytsql -c dev ./scripts`.
- The folder of a script given on the command line (`lazytsql -c dev scripts/report.sql`) shows for that session only.
- `d` on a folder at the top level removes it from the panel, after confirming. The folder and its files stay on disk.
- A remembered folder that was moved or deleted shows as `name/ (not found)`, so you can remove it.

With no folders the panel stays empty, reading nothing from disk. Adding only the folders you work in keeps the trees
and the search small. The list is kept in `~/.config/lazytsql/folders.json`.

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
| `o` | Add a folder (remembered) |
| `d` | Remove the selected top-level folder from the panel |
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

The search covers every folder in the panel. It runs in the background, so typing never waits for the disk, and each key cancels the previous search. It
ignores case and hidden folders, does not follow links to other folders, and stops after 20,000 entries.
