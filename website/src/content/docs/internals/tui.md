---
title: Inside the TUI
description: How the main view, key dispatch, the editor, background work and the key registry are built on Terminal.Gui.
---

The TUI is built on [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 2.5 and lives in `src/LazyTsql/Tui`.

## Main view

`MainView` is a `Runnable` with six `FrameView` panels: a 28% wide left column (connections, files, schema) and,
on the right, the editor (38% high), the results and a seven-row messages panel. Panel indices are named constants
(`ConnectionsPanel = 0` … `MessagesPanel = 5`), and the digit keys map to them.

## Key dispatch

All main-view keys go through one handler on the application keyboard, `OnGlobalKeyDown`, in a fixed order:

1. If a dialog is open, only help (`F1`, or `?` when not typing) is handled; the dialog owns every other key.
2. Global commands that work everywhere: `F5`/`Ctrl+E`, `Ctrl+K`, `F1`, `Ctrl+S`, `Ctrl+O`, `Ctrl+Q`.
3. `Esc` in the editor (when the completion popup is closed) leaves it; `Alt+digit` focuses a panel.
4. Outside the editor, vim motions are translated to the keys the views already bind: `j`→`↓`, `k`→`↑`, `h`→`←`,
   `l`→`→`, `g`→`Home`, `G`→`End`, `Ctrl+D`/`Ctrl+U`→`PgDn`/`PgUp`, and re-sent to the focused view. Behavior is
   then exactly the arrows', in trees, tables and lists alike.
5. The editor and the messages panel get everything else as text; other panels get digits, `q`, `Tab`, `?` and
   their own keys.

## One key registry

`KeyHelp` holds every key once, grouped by `KeyContext` (each panel and each dialog). The footer, the help dialog
and the [key reference](/lazytsql/reference/keys/) are all rendered from it, so a new key is documented in one place.
Dialogs tag themselves with their context, so the footer follows them.

## The editor

`SqlEditor` extends Terminal.Gui's `TextView`:

- **Highlighting**: the text is classified per character (keyword, string, comment, number, variable) and cached
  until it changes; drawing picks the color per character.
- **Completion**: an autocomplete popup fed by `SqlSuggestionGenerator`, which passes the whole text and cursor to
  `SqlCompletion.Suggest`. Accepting replaces only the typed prefix, and the popup does not reopen right after an
  accept.
- **Unknown names**: `Diagnose` runs when the text or the cursor changes; marked ranges draw underlined in red,
  taking priority over syntax colors.

## Background work

Database work runs in `RunInBackground`: `Task.Run`, then a `SemaphoreSlim(1, 1)` so operations on the single
connection run one at a time, in order. Results come back to the UI thread with `App.Invoke`. Errors are logged as
`✗ message`, and cancellations as `Cancelled.`.

The update check runs on its own task, outside the semaphore, because it does not use the connection.

## Theme

`Force16Colors` makes Terminal.Gui use the terminal's 16 ANSI colors instead of fixed RGB values, and panels keep
the default background, so lazytsql follows the terminal's theme. `Theme` defines the few roles it uses: accent
(green), muted (dark gray), selection (blue), syntax colors and the red underline for unknown names. Titles and labels
turn off Terminal.Gui's `_` hotkey marker, so names like `top_customers.sql` keep their underscores.

## Dialogs

`ConfirmDialog` (`y`/`n`, the safe button focused by default for destructive actions), `ConnectionDialog` (two
modes: connection string or fields), `FilePicker` (path completion plus a filtered list), `NameDialog`,
`RowLimitDialog` and `HelpDialog`. Each runs modally and returns its answer.
