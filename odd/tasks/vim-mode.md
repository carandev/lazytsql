# Vim mode for the query editor

Objective: an optional Neovim-style modal editor (normal, insert, visual, `:` commands) in the query panel.
Branch: `20261114-vim-mode` (on top of the query tabs commit 4d840ac). Delivery: exception-ok, one PR of work-unit commits; push/PR are the user's call. Runner: `dotnet test`.

## Specs

- S1 — User request (verbatim): "Podemos cambiar todo el flujo para que sean atajos de neovim y estilo neovim incluyendo los comandos con : y el modo normal, visual y edit en el editor?"
- S2 — Scope decision (verbatim answer): "Modo opcional (Recommended)" — "Vim se activa con una opción guardada o con :set. Los atajos actuales siguen funcionando para quien no use vim." With vim off, the editor behaves exactly as before.
- S3 — Toggle: `F2` toggles vim mode anywhere; `:set vim` / `:set novim` too. The choice is kept in the state directory for the next session.
- S4 — Normal mode: counts; `h j k l`, `w b e W B E`, `0 ^ $`, `gg G`, `f F t T ; ,`, `%`, `Ctrl+D/Ctrl+U`; `x X`, `dd yy cc`, `D C Y`, `d/c/y` + motion or text object (`iw aw i" a" i' a' i( a( ib ab`), `p P`, `r`, `J`, `~`, `>> <<`, `u`, `Ctrl+R`; `i a I A o O s S`; `/` and `?` search with `n N`, `*`; `gt gT`.
- S5 — Insert mode: typing edits as today (completion included); `Esc` returns to normal mode.
- S6 — Visual (`v`) and visual line (`V`): motions extend the selection; `y d x c > < ~ p` act on it; `o` swaps ends; `Esc` leaves.
- S7 — `:` command line shown at the bottom: `:w [path]`, `:q`, `:q!`, `:wq`, `:x`, `:qa`, `:qa!`, `:e <path>`, `:tabnew [path]`, `:tabn`, `:tabp`, `:<n>`, `:set vim|novim`, `:run`; unknown ones report `E492: Not an editor command: <cmd>`.
- S8 — The footer shows the mode (`-- NORMAL --`, `-- INSERT --`, `-- VISUAL --`, `-- VISUAL LINE --`) while vim mode is on; help and docs list the keys.
- S9 — User (verbatim): "En modo vim con ctrl + h/j/l/k debería moverse entre paneles". Chosen (verbatim): "Ctrl+C cancela (Recommended)" — "En modo vim, Ctrl+K mueve al panel de arriba y cancelar pasa a Ctrl+C (como interrumpir en vim/terminal), más :cancel. Con vim apagado, Ctrl+K sigue cancelando como hoy."
- Out of scope (assumption, not requested explicitly): `.` repeat, macros, marks, named registers, visual block, splits.

## Tasks

| ID | Specs | Route | Status | Evidence |
| --- | --- | --- | --- | --- |
| T1 | S4, S6 | inline | done | 94 VimEngineTests green; f93d67a |
| T2 | S2, S3, S5, S7, S8 | inline | done | VimEditorTests, ExCommandTests, help test green; tmux smoke (F2, dd, i, :w path, yyp, :q E37, :wq); edb6c54 |
| T4 | S9 | inline | done | PanelNavigationTests, help + :cancel tests green; tmux smoke Ctrl+h/j/k/l across panels |
| T3 | S3, S8 | inline | done | README, keys (en/es), new vim-mode guide (en/es); astro build not run (no node_modules) |

T1: pure `VimEngine` over a buffer interface, unit tested. T2: wire it into `SqlEditor`/`MainView`, ex commands, toggle, footer. T3: help, README, website docs.
Route evidence: T2 and T3 depend on T1, so there are no independent parallel units; no writer trigger.

## Log

- L1 (2026-11-14) — "Podemos cambiar todo el flujo para que sean atajos de neovim y estilo neovim incluyendo los comandos con : y el modo normal, visual y edit en el editor?"
- L2 (2026-11-14) — User chose "Modo opcional (Recommended)". Forecast ~1500 authored lines, over the 400 heuristic; kept as one PR with work-unit commits since delivery is the user's decision.
- L3 (2026-11-14) — Terminal.Gui 2.5 `InsertText` with several lines leaves the rest of the line on the first line, and its history splits such inserts; the adapter replaces up to the row end (one edit) and keeps vim's own snapshot undo (one step per command or insert stay).
- L4 (2026-11-14) — "En modo vim con ctrl + h/j/l/k debería moverse entre paneles"; answer "Ctrl+C cancela (Recommended)". Probe: Ctrl+H arrives as Ctrl+Backspace and Ctrl+J as Ctrl+Enter, both distinct from Backspace/Enter.
