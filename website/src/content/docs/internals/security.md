---
title: Security
description: How lazytsql handles passwords, files, encryption, logs and releases, and what it does not protect yet.
---

lazytsql connects to databases that often hold sensitive data, so this page states plainly what it protects and
what it does not.

## Passwords and connection strings

- **Kept out of files when you want**: `${VAR}` in a connection string is replaced by an environment variable when
  connecting. The file keeps the reference, never the value.
- **Owner-only file**: `connections.json` is written with 0600 permissions on Linux and macOS.
- **Never on a command line**: sqlpackage receives the connection string through a temporary response file (0600,
  created exclusively, deleted afterwards), so it does not show in `ps` or shell history. Values are quoted so that a
  password cannot inject sqlpackage options; a `"`, which the response file format cannot hold, is refused.
- **Never in logs or messages**: the activity log and the messages panel name the saved connection or the server,
  derived without printing the connection string.
- **Masked input**: the password field of the connection form hides what you type.

## Encryption in transit

Connections use Microsoft.Data.SqlClient, the official driver. In the connection form **Encrypt** is on by default
(TLS required). **Trust server certificate** skips certificate validation, which is what development servers with
self-signed certificates need, but it also lets anyone between you and the server impersonate it. Turn it off for
servers reached over networks you do not control, and install the server's certificate authority instead.

## Local files

| File | Permissions |
| --- | --- |
| Connections | 0600 |
| Activity log | 0600, directory 0700 |
| sqlpackage response file | 0600, deleted after use |

The activity log contains the SQL you ran **as written**, which can include data or secrets typed in a script; it
is deleted after 30 days.

## Network

Besides the database, the only network request is the [update check](/lazytsql/guides/updates/): an anonymous `GET`
to NuGet.org at most once a day, over HTTPS, sending nothing about you or your databases. It never downloads or
runs code. `LAZYTSQL_NO_UPDATE_CHECK=1` turns it off.

## Dependencies and releases

- Three runtime dependencies: Microsoft.Data.SqlClient, Terminal.Gui and, through SqlClient, Microsoft's
  cryptography packages. `dotnet list package --vulnerable` reports none.
- Releases are built by GitHub Actions from the public repository, on each platform's own runner. The workflow runs
  with read-only permissions except the final publish job; the NuGet API key is a repository secret.

## Known gaps

These are tracked to be improved:

| Gap | Impact | Today's mitigation |
| --- | --- | --- |
| *Trust server certificate* is pre-checked in the form | A new connection over an untrusted network could be intercepted | Uncheck it, or use a connection string without `TrustServerCertificate=true` |
| Passwords typed directly into a connection are stored in plain text | Anyone with your user account can read them | Use `${VAR}`; the file is 0600 |
| Release binaries have SHA-256 checksums but no signatures or provenance attestations | Someone who could change both a release's files and its `SHA256SUMS` would not be detected | The install scripts and `sha256sum -c` catch a corrupted or swapped download; `dotnet tool` installs packages NuGet.org signs |
| No confirmation before destructive statements | A `DELETE` without `WHERE` runs immediately | Review before `F5`; use a login with only the permissions you need |
| SQL is logged as written | Secrets typed in scripts land in the log | Avoid literal secrets; set `LAZYTSQL_LOG_DIR` to a location you manage |

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use
[GitHub's private vulnerability reporting](https://github.com/carandev/lazytsql/security/advisories/new) instead.
