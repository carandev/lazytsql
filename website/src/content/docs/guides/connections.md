---
title: Connections
description: Save connections from the TUI or the command line, keep passwords out of the file, and choose which databases appear.
---

## Saving connections

From the command line:

```bash
lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
lazytsql connections list
lazytsql connections remove dev
```

From the TUI, in the connections panel (`1`): `a` adds, `e` edits, `d` deletes (after confirming) and `Enter`
connects.

Connections are stored in `~/.config/lazytsql/connections.json` (or `$XDG_CONFIG_HOME/lazytsql/connections.json`),
readable only by you. See [storage](/lazytsql/internals/storage/) for the format.

## The connection form

The add/edit dialog has two modes; switch with `←`/`→` and `Space` on *Mode*:

- **Connection string**: any SqlClient connection string.
- **Fields**: server, port, database, authentication (SQL login or integrated), user, password, *Encrypt* and
  *Trust server certificate*.

A connection string with settings the form cannot show opens in connection string mode, so editing never drops
them. The password field is masked.

## Keeping passwords out of the file

Write `${VAR}` anywhere in a connection string and lazytsql replaces it with that environment variable when it
connects:

```bash
export DEV_DB_PASSWORD='s3cret'
lazytsql connections add dev 'Server=db;User Id=app;Password=${DEV_DB_PASSWORD}'
```

The file keeps `${DEV_DB_PASSWORD}`; an unset variable is an error at connect time.

## Choosing a connection

`-c` takes a saved name (case-insensitive) or a raw connection string. Without `-c`, `LAZYTSQL_CONNECTION` is used:

```bash
lazytsql -c dev
lazytsql -c 'Server=localhost;Database=app;Integrated Security=true'
LAZYTSQL_CONNECTION=dev lazytsql query -q "SELECT 1"
```

Messages and the activity log name the connection by its saved name or its server, never by the connection string,
because it may hold a password.

## Which databases appear

When the connection names a database (`Database=` or `Initial Catalog=`), the schema panel shows only that
database. Otherwise it lists every online database you can access.

## Encryption

*Encrypt* is on by default. *Trust server certificate* accepts a server certificate that no trusted authority
signed, which development servers usually have; leave it off for servers you reach over networks you do not
control. See [security](/lazytsql/internals/security/).

Integrated (Windows) and Microsoft Entra ID authentication are not yet verified in the native binary.
