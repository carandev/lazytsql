# lazytsql MVP
Objective: lazygit-style TUI SQL client focused on SQL Server, plus a headless JSON mode for agents.
Branch: feat/mvp · Delivery: ask-on-risk · Test runner: `dotnet test`

## Specs
- S1 SQL Server first: "Soporte para sql server, mi desarrollo es en .net entonces es lo primordial"
- S2 UI: "quiero que la UI sea limpia y cómoda como lazygit o lazydocker"
- S3 Performance: "que sea lo más óptimo posible y que gaste muy pocos recursos" (Native AOT single binary, streamed/capped result sets).
- S4 MVP scope (accepted proposal): connection, schema tree, editor with `GO` batches, results grid, `--json` mode for agents.
- S6 "Ejecutando lazytsql debería abrir la tui y permitir gestionar las conexiones, sin necesidad de hacerlo por aparte por cli"
- S5 Usable on the server where the DB is deployed and by agents in the terminal (headless commands).
- S7 "Mejora la UI para que se vea más minimalista" (screenshot: filled blue backgrounds, heavy chrome).
- S8 "agrega una opción para que se pueda exportar en csv"
- S9 "que los movimientos también se puedan hacer con los atajos de vim"
- S10 "Agrega que se pueda escoger si es connection string o por aparte cada valor para iniciar sesión."
- S11 "En el connection string yo especifiqué la bd pero tengo acceso a todas las bases de datos, que si se indica una bd entonces solo aparezca esa, ya si no se indica entonces que se carguen todas."
- Backlog (not in scope yet): "que se pueda cargar un directorio con archivos sql o abrir un archivo sql directamente y también poder importar o exportar archivos .bacpac con sqlpackage"

## Tasks
- [x] T1 S4 Core domain: batch splitter (`GO`, `GO n`), result models, ports — inline — 14 tests green
- [x] T2 S1 SQL Server adapter: execute batches, multiple result sets, PRINT messages, schema introspection — inline — 12 tests green against SQL Server 2022 container
- [x] T3 S4,S5 Headless CLI: `query`, `schema`, `connections` with `--json`; connection config — inline — 7 tests green + e2e smoke against container
- [x] T4 S2 TUI: numbered panels (connections, schema, editor, results), keybindings, status bar — inline — 2 tests + tmux e2e (connect, schema tree, SELECT TOP, definitions, multi result sets, PRINT, quit)
- [x] T5 S3 Native AOT publish, measure binary size and memory — inline — verified at runtime
- [x] T6 S6 Manage connections (add, edit, delete) from TUI panel [1] — inline — rename test + tmux e2e (add, connect, rename, delete confirm)
- [x] T7 S11 Schema tree shows only the connection's database when one is set — inline — integration test green
- [x] T8 S8 CSV export: results panel and `query --csv` — inline — 5 tests + tmux e2e export
- [x] T9 S9 Vim motions (h/j/k/l, g/G, Ctrl+d/u) outside the editor — inline — tmux e2e (tree g/G/j/k/h/l)
- T10 S10 Connection dialog: connection string or individual fields — inline
- T11 S7 Minimal theme: terminal background, focused border accent, no filled panels — inline

## Log
- L1 2026-10-28 "Me gustaría crear algo como "lazysql" que sea tan extenso como datagrip pero en una tui dentro de la terminal, así facilita el desarrollo agéntico y en terminal con herdr por ejemplo y también facilita tener un cliente de sql en el servidor donde está desplegada la bd"
- L2 2026-10-28 "Soporte para sql server, mi desarrollo es en .net entonces es lo primordial"
- L3 2026-10-28 "Si, quiero que la UI sea limpia y cómoda como lazygit o lazydocker, y que sea lo más óptimo posible y que gaste muy pocos recursos"
- L4 Stack decision: C# / .NET 10, Microsoft.Data.SqlClient 7.1.1, Terminal.Gui 2.5.0, Native AOT. Working name `lazytsql`.
- L5 Evidence: 35 tests green (14 core, 9 app, 12 SQL Server 2022 integration). TUI verified end to end in tmux.
- L6 AOT: 21 MB linux-x64 binary. SqlClient throws "Globalization Invariant Mode is not supported", so ICU stays required. SqlClient trim warnings (IL2104/IL3053) are kept visible; all common types verified in the AOT binary.
- L7 Memory: headless query ~46 MB RSS (~15 MB private). TUI ~85 MB RSS (~50 MB private, peak ~100 MB with 1000 rows); idle CPU ~1.8% from the Terminal.Gui main loop. GC gen0/conserve settings had no meaningful effect.
- L8 Follow-ups: Terminal.Gui.Editor for SQL highlighting (TextView is obsolete), ScriptDom-based autocomplete, server-side paging instead of draining capped rows, Entra ID/Windows auth checks under AOT, profiling TUI private memory.
- L9 2026-10-28 "Ejecutando lazytsql debería abrir la tui y permitir gestionar las conexiones, sin necesidad de hacerlo por aparte por cli"
- L10 2026-10-28 "Mejora la UI para que se vea más minimalista, agrega una opción para que se pueda exportar en csv, que los movimientos también se puedan hacer con los atajos de vim. Agrega que se pueda escoger si es connection string o por aparte cada valor para iniciar sesión. En el connection string yo especifiqué la bd pero tengo acceso a todas las bases de datos, que si se indica una bd entonces solo aparezca esa, ya si no se indica entonces que se carguen todas. Para futuras funcionalidades después de lo pendiente podemos agregar que se pueda cargar un directorio con archivos sql o abrir un archivo sql directamente y también poder importar o exportar archivos .bacpac con sqlpackage"
