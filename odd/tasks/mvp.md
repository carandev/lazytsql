# lazytsql MVP
Objective: lazygit-style TUI SQL client focused on SQL Server, plus a headless JSON mode for agents.
Branch: feat/mvp · Delivery: ask-on-risk · Test runner: `dotnet test`

## Specs
- S1 SQL Server first: "Soporte para sql server, mi desarrollo es en .net entonces es lo primordial"
- S2 UI: "quiero que la UI sea limpia y cómoda como lazygit o lazydocker"
- S3 Performance: "que sea lo más óptimo posible y que gaste muy pocos recursos" (Native AOT single binary, streamed/capped result sets).
- S4 MVP scope (accepted proposal): connection, schema tree, editor with `GO` batches, results grid, `--json` mode for agents.
- S5 Usable on the server where the DB is deployed and by agents in the terminal (headless commands).

## Tasks
- [x] T1 S4 Core domain: batch splitter (`GO`, `GO n`), result models, ports — inline — 14 tests green
- T2 S1 SQL Server adapter: execute batches, multiple result sets, PRINT messages, schema introspection — inline
- T3 S4,S5 Headless CLI: `query`, `schema`, `connections` with `--json`; connection config — inline
- T4 S2 TUI: numbered panels (connections, schema, editor, results), keybindings, status bar — inline
- T5 S3 Native AOT publish, measure binary size and memory — inline

## Log
- L1 2026-10-28 "Me gustaría crear algo como "lazysql" que sea tan extenso como datagrip pero en una tui dentro de la terminal, así facilita el desarrollo agéntico y en terminal con herdr por ejemplo y también facilita tener un cliente de sql en el servidor donde está desplegada la bd"
- L2 2026-10-28 "Soporte para sql server, mi desarrollo es en .net entonces es lo primordial"
- L3 2026-10-28 "Si, quiero que la UI sea limpia y cómoda como lazygit o lazydocker, y que sea lo más óptimo posible y que gaste muy pocos recursos"
- L4 Stack decision: C# / .NET 10, Microsoft.Data.SqlClient 7.1.1, Terminal.Gui 2.5.0, Native AOT. Working name `lazytsql`.
