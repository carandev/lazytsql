# Edit query results
Objective: edit cells of a SELECT's results in the TUI and save them with a reviewed UPDATE, like DataGrip.
Branch: feat/edit-results · Delivery: ask-on-risk · Test runner: `dotnet test`

## Specs
- S1 "Se puede agregar que se editen los campos en el panel de resultados cuando se hace un select? y que eso reemplace el sql con un update con los cambios que se hace en la tabla?" → "El de datagrip": edits stay pending in the grid, highlighted, until they are submitted; the query in the editor is not replaced.
- S2 "un modal de confirmación con el query que se ejecutará": submitting opens a confirmation that shows the exact script that runs; nothing reaches the database without confirming it.
- S3 (decision, from SQL Server's browse metadata) A cell is editable only when its column comes from a table column that can be updated (not computed, identity, rowversion or an expression) and the result shows every column of that table's key; otherwise editing explains why it cannot.
- S4 (decision) The script updates each changed row by its key, fully qualified table name, in one transaction that is rolled back unless every UPDATE matched exactly one row.
- S5 (decision) A cell can be set to NULL, and pending changes can be reverted per cell or all at once; running another query or quitting with pending changes asks first.
- S6 Help text, README and website docs (EN/ES) describe the keys.

## Tasks
- [x] T1 S3-S4 Core: result source metadata, typed values and literals, pending edits and the update script (inline) · GREEN: SqlValueTests, ResultEditsTests
- [x] T2 S3 SqlServer: describe a result's source columns through sp_describe_first_result_set (inline) · GREEN: ResultEditingTests (29, SQL Server 2022); RED by mutation: removing the row check fails the deleted-row test
- [ ] T3 S1-S2,S5 TUI: edit dialog, pending highlight, revert, submit with confirmation, guards (inline)
- [ ] T4 S6 Docs and help (inline)

Risk: item 1 (writes rows of the user's database) → independent verifier at the feature's end.

## Log
- L1 2026-10-29 "Se puede agregar que se editen los campos en el panel de resultados cuando se hace un select? y que eso reemplace el sql con un update con los cambios que se hace en la tabla? Como funciona datagrip?"
- L2 2026-10-29 "El de datagrip, un modal de confirmación con el query que se ejecutará"
- L3 Probe (SQL Server 2022, browse mode 1): source table/column and is_part_of_unique_key per column; key columns not selected come back hidden; views map to their base table; heaps have no key; expressions have no source; computed/identity/rowversion are not updateable.
- L4 T1-T2 green: full suite 104 app / 108 Core / 65 SqlServer. A trigger writing other rows keeps @@ROWCOUNT of the UPDATE; a datetime key read as .0033333 matches through its .003 literal. Engram mirror pending: memory tools unavailable this session.
