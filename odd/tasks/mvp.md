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
- S12 "Que haya un mensaje de confirmación antes de cerrar"
- S13 "sigue con la recomendación": the recommendation accepted was "resaltado + abrir/guardar archivos `.sql`" (T-SQL syntax highlighting in the editor; open a `.sql` file with `lazytsql archivo.sql` and from the TUI, and save it).
- S14 "Ordena mejor el modal de keys" (screenshot: the Keys dialog clips its content; Connections, Query and `x` export are cut off).
- S15 "autocomplete en los lugares donde se ingresan las rutas"
- S16 "incluso si es posible que se haga un modal de selección de rutas y que se pueda usar si hace falta, como un \"explorador de archivos\", parecido a como funcionan las guis"
- S17 "en el footer del tui se vaya mostrando según el panel o modal las teclas que puede usar y un breve resumen de para qué, incluso si son muchas que al darle en "?" por defecto solo muestre las de ese panel o modal y que pueda expandir a ver las de todo el sistema"
- S18 "Al exportar debería exportar todo, no solo las primeras 1000, además debería haber una opción para escoger la cantidad que quiero o si quiero que traiga todo al visualizar"
- S19 "ahora el autocompletado según la bd cómo se podría hacer?" → "Dale" to the proposed plan: suggestions from the connected database (tables and views after FROM/JOIN, procedures after EXEC, columns by alias or table name, other databases with db.schema.object), Tab accepts, Ctrl+Space opens, Esc closes the popup before leaving the editor, no suggestions inside strings or comments; one commit per step.
- Backlog (not in scope yet): "que se pueda cargar un directorio con archivos sql o abrir un archivo sql directamente y también poder importar o exportar archivos .bacpac con sqlpackage"; "agrega en los pendientes que tenga logs detallados de lo que se hace y que eso quede guardado para consultar historial si hace falta"

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
- [x] T10 S10 Connection dialog: connection string or individual fields — inline — 5 unit tests + tmux e2e (create by fields, connect, toggle modes)
- [x] T11 S7 Minimal theme: terminal background, focused border accent, no filled panels — inline — rendered screenshots reviewed; AOT TUI smoke test
- [x] T12 S12 Themed confirmation before quitting (also reused for delete) — inline — tmux e2e: q→n stays, Esc→dialog→Esc stays, Ctrl+Q→Enter exits
- [x] T13 S13 Open and save `.sql` files (CLI positional, Ctrl+O, Ctrl+S) — inline — 3 tests (RED then GREEN) + tmux e2e: open CRLF file, dirty title, save keeps CRLF, discard/overwrite confirms, new file, save-as
- [x] T14 S13 T-SQL syntax highlighting via TextView.OnDrawNormalColor and a Core tokenizer (no new dependency) — inline — 7 tokenizer tests + tmux ANSI capture (vertical/horizontal scroll, reload on open, definitions) + AOT smoke
- [x] T15 S14 Keys dialog grouped by section, sized to its content — inline — 1 test (RED then GREEN) + tmux render at 120x38
- [x] T16 S15,S16 File picker for every path input (open, save, export CSV): Tab completion plus a browsable directory list — inline — 4 tests (RED then GREEN) + tmux e2e: filter, Tab, j/k/l/h, open, save adds .sql, CSV export against SQL Server 2022
- [x] T17 S17 One key registry per context feeding the footer (panels and modals) and a contextual `?`/F1 help that expands to every key — inline — 3 tests (RED then GREEN) + tmux e2e: footer per panel, picker, confirm; ?/F1 per context, `a` expand/back, scroll at 25 rows
- [x] T18 S18 Export streams every row by running the batch again (asks first; Ctrl+K cancels and removes the partial file); `m` in results sets the row limit or `all` for the next run — inline — 8 tests (RED then GREEN, cancel RED reproduced as a >100 s hang) + tmux e2e against SQL Server 2022: limit 10 and all, export 1500 and 3,000,000 rows (~134 MB RSS), cancel mid-export
- [x] T19 S19 Catalog per database without switching the session database + completion engine for objects and keywords + editor popup — inline — 7 engine tests + 1 SQL Server test (another database, name with `]` and `'`, current database unchanged) + tmux e2e: FROM, EXEC, `dbo.`, Tab accepts, Esc closes popup then leaves editor, popup refreshes when names finish loading; CLI `schema` differential vs base identical (5 cases)
- [x] T20 S19 Columns: alias and table resolution in the current statement, lazy column loading — inline — 5 engine tests (RED then GREEN) + tmux e2e: `c.na`→`c.name` and `o.t`→`o.total` mid-line with text after the cursor, `WHERE em`→`email`
- [ ] T21 S19 Other databases: db. → schemas, db.schema. → objects, db.schema.object. → columns — inline

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
- L11 Terminal.Gui emits 24-bit RGB by default even for ColorName16; `Force16Colors` makes it emit ANSI indices so the theme follows the terminal palette. `Scheme(Attribute)` derives Focus by inverting colors, so header and hint schemes set every role explicitly. OptionSelector needs `TabBehavior.NoStop` to be one Tab stop.
- L12 Evidence: 47 tests green (14 core, 15 app, 18 SQL Server). AOT binary 21 MB, TUI smoke-tested.
- L13 2026-10-28 "Que haya un mensaje de confirmación antes de cerrar y sigue con la recomendación. Además agrega en los pendientes que tenga logs detallados de lo que se hace y que eso quede guardado para consultar historial si hace falta"
- L14 Decision: highlighting uses TextView's DrawNormalColor hook with an in-house tokenizer instead of adding the Terminal.Gui.Editor package (TextMate grammars rely on the Oniguruma native library the publish strips). Engram mirror still pending (tools unavailable in this session).
- L15 Evidence: 51 tests green, 6 SQL Server integration tests skipped (no LAZYTSQL_TEST_CONNECTION in this run); TUI e2e against a SQL Server 2022 container. AOT binary 21 MB. Dialog heights were clipping content: PromptDialog (export CSV too) and the new ConfirmDialog. Terminal.Gui marks the last dialog button as default, so the default button goes last.
- L16 Finding (pre-existing): `-c` with a raw connection string uses it as the display name, so the editor title and the messages panel show the password.
- L17 2026-10-28 "Si" (fix the password shown on screen). Fixed with `ConnectionResolver.DisplayName`: profile name, or `server/database` for raw strings. 1 test (RED then GREEN) + tmux check: "Connecting to localhost,1/shop…", no password on screen.
- L18 2026-10-28 "Ordena mejor el modal de keys, autocomplete en los lugares donde se ingresan las rutas, incluso si es posible que se haga un modal de selección de rutas y que se pueda usar si hace falta, como un "explorador de archivos", parecido a como funcionan las guis"
- L19 Decision: an in-house picker instead of Terminal.Gui's FileDialog, to keep the minimal theme and vim keys; it replaces PromptDialog, whose only uses were paths.
- L20 ListView overrides OnKeyDown for search-as-you-type, so its KeyDown event never sees letters; the picker sets `KeystrokeNavigator = null`. Focusing a TextField selects its text, so the picker moves the cursor to the end after focusing.
- L21 2026-10-28 "Ya que cada vez que crezca el sistema van a existir más atajos de teclado sería bueno que en el footer del tui se vaya mostrando según el panel o modal las teclase que puede usar y un breve resumen de para qué, incluso si son muchas que al darle en "?" por defecto solo muestre las de ese panel o modal y que pueda expandir a ver las de todo el sistema"
- L22 Decision: dialogs tag themselves with `View.Data = KeyContext`; MainView follows SessionBegun/SessionEnded to switch the footer. F1 opens help where `?` is text (editor, inputs); `a` toggles the help between the current context and the whole system.
- L23 2026-10-28 "Al exportar debería exportar todo, no solo las primeras 1000, además debería haber una opción para escoger la cantidad que quiero o si quiero que traiga todo al visualizar"
- L24 Decision: exporting a capped result set re-runs its batch and streams rows to the CSV (new `IDatabaseSession.StreamResultSetAsync` + `IRowSink`), instead of fetching everything into the grid; it confirms first (Cancel is the default) because the batch's side effects run again, and refuses when the connection changed. `QueryOptions.MaxRows = 0` means every row; the CLI keeps rejecting `--max-rows 0`, unchanged. Finding: closing a SqlDataReader reads every remaining row, so a cancelled stream hung; the command is now cancelled on the server through the token. Finding: the row limit dialog needed one more row of height to show its error line. Follow-up (pre-existing, not changed): a capped query still drains the remaining rows on the server before the next result set.
- L25 2026-10-28 "Listo, ahora el autocompletado según la bd cómo se podría hacer?" — proposed plan (Terminal.Gui PopupAutocomplete + own generator, heuristic over SqlTokenizer instead of ScriptDom, three steps); user answered "Dale".
- L26 Decision: the catalog loads objects and columns per database with `EXEC [db].sys.sp_executesql`, so completion never changes the session database (expanding a database in the schema tree does switch it, unchanged). Completion reads the whole editor text because the popup only receives the current line.
- L27 Finding: TextView only regenerates and draws the popup after an edit, and a closed popup opens on one RenderOverlay call and draws on the next; `SqlEditor.RefreshCompletion` mirrors that so names that finish loading show without another keystroke. The default popup MaxWidth (10) cut names; it is 60.
- L28 Finding: Terminal.Gui's popup accepts by moving the cursor forward by Suggestion.Remove and deleting backwards, so completing mid-line deleted the text after the cursor; SqlEditor installs its own TextViewAutocomplete that deletes only the typed prefix. Its HostControl must not be set in the constructor (NullReferenceException at startup); TextView attaches it in EndInit.
