---
title: Keys
description: Every key of the TUI, by panel and dialog. The same list lazytsql shows with ? and in its footer.
---

This list mirrors the registry lazytsql uses for its footer and help dialog, so it is exactly what the app shows
with `?` (or `F1`).

## Global

| Key | Action |
| --- | --- |
| `1`-`6` / `Alt+1`-`Alt+6` | Focus a panel (`Alt+` also works inside the editor) |
| `Tab` | Next panel |
| `h` `j` `k` `l` | Move (outside the editor) |
| `g` / `G` | Top / bottom |
| `Ctrl+D` / `Ctrl+U` | Page down / up |
| `F5` / `Ctrl+E` | Run the selection, or the whole script batch by batch |
| `Ctrl+K` | Cancel the running query, export or import |
| `Ctrl+O` / `Ctrl+S` | Open / save a `.sql` file |
| `?` / `F1` | Help for the focused panel (`F1` inside the editor) |
| `q` / `Esc` / `Ctrl+Q` | Quit after confirming (`q` and `Esc` only outside the editor) |

## Panels

| Panel | Key | Action |
| --- | --- | --- |
| Connections `[1]` | `Enter` | Connect |
| | `a` / `e` / `d` | Add / edit / delete a connection |
| Files `[2]` | `Enter` | Open the script, or expand/collapse a folder |
| | `h` / `l` | Collapse / expand |
| | `o` | Add a folder (remembered across sessions) |
| | `d` | Remove the selected top-level folder from the panel |
| | `/` | Search folders and scripts at any depth |
| | `n` / `N` | Next / previous match |
| | `r` | Refresh |
| File search | *text* | Search as you type; the first match is selected |
| | `Enter` / `Esc` | Keep the match / go back to where you were |
| | `Backspace` | Delete a character (on an empty search, cancel) |
| Schema `[3]` | `Enter`, `l` / `h` | Expand / collapse |
| | `s` | `SELECT TOP (100)` from the selected table or view |
| | `e` | Open the selected procedure or function definition |
| | `r` | Reload the schema and the completion names |
| | `x` / `i` | Export the selected database to `.bacpac` / import a `.bacpac` |
| Query `[4]` | `F5` | Run the selection or everything |
| | `Ctrl+K` | Cancel |
| | `Ctrl+S` | Save |
| | `Ctrl+V` | Paste the system clipboard |
| | `Tab` / `Ctrl+Space` | Accept / open completion (`Esc` closes it) |
| | `Esc` | Leave the editor |
| Results `[5]` | `[` / `]` | Previous / next result set |
| | `x` | Export to CSV |
| | `m` | Row limit |
| | `h` `j` `k` `l` | Move between cells |
| Messages `[6]` | `j` / `k` | Scroll |

## Dialogs

| Dialog | Key | Action |
| --- | --- | --- |
| File picker | `Tab` | Complete the path |
| | `↓` | Move into the list |
| | `Enter` / `l` | Open a folder or pick a file |
| | `h` / `Backspace` | Go up a folder |
| | `Esc` | Cancel |
| Confirmation | `y` / `n` | Press the button marked `(y)` / `(n)` |
| | `←` `→` (or `h` `l`) | Choose a button; the chosen one is highlighted |
| | `Enter` | Press the chosen button |
| | `Esc` | Cancel |
| Connection form | `Tab` | Next field |
| | `←` `→` | Choose an option |
| | `Enter` / `Esc` | Save / cancel |
| Row limit | `Enter` / `Esc` | Apply / cancel |
| Database name | `Enter` / `Esc` | Import / cancel |
| Help | `a` | Switch between this panel's keys and every key |
| | `j` / `k` | Scroll |
| | `Esc` | Close |
