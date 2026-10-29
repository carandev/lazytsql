---
title: Completion engine
description: How lazytsql tokenizes SQL, finds the context, loads names lazily, suggests joins and marks unknown names.
---

The completion engine lives in `LazyTsql.Core/Completion` and is pure: it receives the editor lines, the cursor and a
`SchemaCatalog`, and returns suggestions or diagnostics. It never talks to the server itself.

## Tokens

`SqlLexer` turns the text into tokens: words (with brackets and quotes removed and a `Bracketed` flag), dots, commas,
open parentheses, statement separators (`;` or a `GO` line) and everything else. It knows when the cursor is inside a
string or a comment, where nothing is suggested.

Syntax highlighting uses a separate, character-level classifier (`SqlTokenizer`: keyword, string, comment, number,
variable) that tracks multi-line comments and strings.

## Finding the context

`SqlCompletion.Suggest` looks at the word being typed and what is around it:

1. **A qualifier**: a dot chain touching the word (`c.`, `sales.`, `archive.dbo.`).
2. **The previous word**: `FROM`, `JOIN`, `UPDATE`, `INTO`, `APPLY`, `TABLE`, `EXEC`, `ON`…
3. **The statement's tables**: `References` collects every table after those keywords in the current statement,
   with its alias (`AS` optional), continuing a `FROM a, b` list after commas. A statement ends at `;` or `GO`.

From that:

| Context | Suggestions |
| --- | --- |
| After `FROM`, `JOIN`, `UPDATE`, `INTO`, `APPLY` | Tables, views, functions, synonyms and databases |
| After `EXEC` | Procedures |
| `x.` | An alias or table of the statement → its columns; a schema → its objects; a database → its schemas |
| `x.y.` | `schema.table` → columns, or `database.schema` → objects |
| Anywhere else | Columns of the statement's tables, their aliases, and keywords |
| After `JOIN` / `ON` | Joins and conditions from foreign keys, even before you type a letter |

## Ranking

Candidates match by prefix, case-insensitively, and each text appears once. A keyword typed in full is dropped (no
point suggesting `FROM` after `FROM`), but a table typed in full is kept and sorted **first**, so `Tab` can confirm
it. Then they sort by kind (join, column, alias, table, view, function, synonym, procedure, schema, database,
keyword) and by name, up to 200.

Inserted names are written to fit: a `dbo` object goes without its schema unless another schema has an object with
the same name, and a name is bracketed only when it is not a regular identifier or is a reserved word.

## Joins from foreign keys

For each table already in the statement, `Links` finds the foreign keys that point from it or to it, and pairs the
columns in key order. A suggestion is the related table, an alias from its initials (`order_lines` → `ol`, numbered
when taken) and the condition with every column: `shipments s ON s.order_id = l.order_id AND s.line = l.line`. After
`ON`, only the conditions between the table just joined and the earlier ones are offered.

## Synonyms

Synonyms are read with the object they point to (`PARSENAME` of `sys.synonyms.base_object_name`). `Resolve` follows
the target once, into the same or another database, so a synonym's columns, foreign keys and unknown-column checks
are its table's. A synonym on a linked server has no readable target and is only completed by name.

## Lazy schema catalog

`SchemaCatalog` holds what has been loaded: the database list, then per database its objects and foreign keys, and
per table its columns. When the engine asks for something missing, the catalog returns `null` and raises
`ObjectsNeeded`, `ColumnsNeeded` or `ForeignKeysNeeded`, **once** per key. The TUI answers in the background: it
reads the names with schema queries that run in that database without changing yours, stores them, and refreshes the
completion list, so the suggestions appear a moment later without blocking typing.

## Unknown names

`Diagnose` checks every table reference and every `alias.column` in the script. A table is marked when its database
is loaded and has no object with that name; a column when its table's columns are loaded and none matches. To avoid
false positives it skips:

- the target of `CREATE`/`ALTER`/`DROP TABLE` and `SELECT ... INTO` (it may not exist yet), but checks `INSERT INTO`
  and `MERGE INTO`;
- temporary tables, names the script defines (CTEs, `CREATE TABLE`, `CREATE VIEW`, `CREATE SYNONYM`), `sys` and
  `INFORMATION_SCHEMA`, aliases used as names (`UPDATE c ... FROM customers c`);
- four-part names (linked servers) and databases that are not in the list;
- the word under the cursor, and anything not loaded yet.

The editor draws the marked ranges with a red underline and recomputes them when the text or the cursor changes.
