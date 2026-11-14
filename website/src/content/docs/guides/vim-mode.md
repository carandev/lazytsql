---
title: Vim mode
description: Edit queries with Neovim-style modes, motions, operators and : commands.
---

The query editor can work like Neovim: a **normal** mode for moving and editing with commands, an **insert** mode for
typing, **visual** modes for selecting, and a **command line** for `:` commands. It is optional and off by default;
with it off, the editor works as before.

## Turning it on

Press `F2` anywhere, or type `:set vim` (`:set novim` turns it off). lazytsql remembers the choice for the next
session. The footer shows the mode (`-- NORMAL --`, `-- INSERT --`, `-- VISUAL --`, `-- VISUAL LINE --`), the keys of
a command not finished yet, and what you type after `:`, `/` or `?`.

In vim mode `Esc` belongs to vim, so it no longer leaves the editor: use `Alt+1`-`Alt+6` to move to another panel.
`F5`, `Ctrl+K`, `Ctrl+S`, `Ctrl+T`, `Ctrl+W` and the other global keys keep working in every mode.

## Normal mode

Counts work as in vim: `3j`, `2dd`, `d2w`.

| Keys | Action |
| --- | --- |
| `h` `j` `k` `l` (or the arrows) | Move |
| `w` `b` `e`, `W` `B` `E` | Next word / previous word / end of word (`W` words are anything but spaces) |
| `0` `^` `$` | Start of the line / first non-blank / end of the line |
| `gg` / `G`, `5G` | First / last line, line 5 |
| `f` `F` `t` `T` + character, `;` `,` | Jump to a character on the line, repeat forward / backward |
| `%` | Matching bracket |
| `Ctrl+D` / `Ctrl+U` | Half a page down / up |
| `/text`, `?text`, `n` / `N`, `*` | Search forward / backward (ignoring case), next / previous match, the word under the cursor |
| `i` `a` `I` `A` `o` `O` | Insert before / after the cursor, at the line's start / end, on a new line below / above |
| `x` `X`, `s` `S`, `r` + character | Delete a character after / before, change a character / the line, replace a character |
| `d` `c` `y` + motion | Delete / change / yank (copy): `dw`, `c$`, `y2j`, `dfx` |
| `dd` `cc` `yy`, `D` `C` `Y` | The same on whole lines; `D` and `C` to the end of the line |
| `iw` `aw`, `i"` `a"`, `i'` `a'`, `i(` `a(` (`ib`), `i[`, `i{` | Text objects after an operator: `ciw`, `di(`, `ya'` |
| `p` / `P` | Paste after / before (lines go below / above) |
| `J`, `~`, `>>` / `<<` | Join lines, toggle case, indent / dedent |
| `u` / `Ctrl+R` | Undo / redo; a whole stay in insert mode is one step |
| `gt` / `gT` | Next / previous query tab |
| `ZZ` / `ZQ` | Same as `:x` / `:q!` |

Yanked text also goes to the system clipboard, so it can be pasted in other apps.

## Insert mode

Typing works as in the plain editor, completion included. `Esc` closes the completion list and goes back to normal
mode.

## Visual modes

`v` selects characters and `V` whole lines. Motions and text objects (`viw`, `vi(`) extend the selection; `o` moves
to its other end. Then `y`, `d` or `x`, `c`, `>`, `<`, `~` or `p` act on it, and `Esc` leaves.

## Commands

| Command | Action |
| --- | --- |
| `:w`, `:w path` | Save the query (asking for a path the first time), or save it as `path`; `:w! path` replaces an existing file |
| `:q`, `:q!` | Close the query tab, refusing unsaved changes unless `!`; on the last tab, quit (after confirming) |
| `:wq`, `:x` | Save and close (`:x` saves only when there are changes) |
| `:qa` | Quit |
| `:e path`, `:tabnew [path]` | Open a script in a tab / open a new tab |
| `:tabn`, `:tabp` | Next / previous tab |
| `:12` | Go to line 12 |
| `:run` | Run the query, like `F5` |
| `:set vim`, `:set novim` | Turn vim mode on / off |

Paths can start with `~` for your home folder. An unknown command shows `E492: Not an editor command`.

Not included: `.` repeat, macros, marks, named registers, visual block mode and window splits.
