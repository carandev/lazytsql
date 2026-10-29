---
title: Backups with .bacpac
description: Export a whole database to a .bacpac file and import it as a new database, from the TUI or the command line.
---

A `.bacpac` holds a whole database, schema and data, in a portable file that Azure SQL and SQL Server both read.
lazytsql runs Microsoft's [sqlpackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage) for it, so install
that first:

```bash
dotnet tool install -g microsoft.sqlpackage
```

## From the command line

```bash
lazytsql export -c dev -d app app.bacpac         # the database to a file
lazytsql import -c dev -d app_copy app.bacpac    # the file into a new, empty database
```

- `-d` defaults to the database in the connection string.
- A file name without extension gets `.bacpac`.
- sqlpackage's progress goes to stderr; the final line (`Exported app to /path/app.bacpac`) to stdout.
- `--json` and `--csv` are not supported here.

## From the TUI

In the schema panel (`3`), select a database and press `x` to export it, or press `i` to import a file. The import
asks for the new database name, suggesting the file name. Progress shows in the messages panel and `Ctrl+K` cancels.

## What to expect

- **Imports need a new or empty database.** sqlpackage refuses to import into a database with objects.
- **A failed or cancelled export keeps the previous file.** The export is written next to the target as
  `name.partial-<id>.bacpac` and replaces the target only when complete.
- **A cancelled import may leave a partially imported database**, which you can drop.
- **The connection string never appears on a command line.** It reaches sqlpackage through a temporary file only
  you can read, deleted afterwards. sqlpackage cannot read a double quote (`"`) in that file, so a password
  containing one is refused before sqlpackage starts.

## Finding sqlpackage

lazytsql looks for it in this order:

1. `$LAZYTSQL_SQLPACKAGE`, a path to the executable;
2. the `PATH`;
3. `~/.dotnet/tools`, where `dotnet tool install -g` puts it.
