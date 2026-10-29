---
title: Troubleshooting
description: Common errors and how to fix them.
---

| Symptom | Fix |
| --- | --- |
| `lazytsql: command not found` after `dotnet tool install` | Add `~/.dotnet/tools` to your `PATH` and open a new terminal. |
| `Globalization Invariant Mode is not supported` or ICU errors on Linux | Install ICU: `sudo apt install libicu-dev` (Debian/Ubuntu) or `sudo dnf install libicu` (Fedora). |
| `The certificate chain was issued by an authority that is not trusted` | The server uses a self-signed certificate. For a development server, add `TrustServerCertificate=true` to the connection string; for anything else, install the server's certificate authority instead. |
| `Environment variable 'X' referenced in the connection string is not set` | The connection string references `${X}`; export it before running lazytsql. |
| `sqlpackage not found` | `dotnet tool install -g microsoft.sqlpackage`, or set `LAZYTSQL_SQLPACKAGE` to its path. |
| `Ctrl+V` pastes nothing on Linux | lazytsql reads the clipboard with `wl-paste` (Wayland), `xclip` or `xsel`; install one of them (`wl-clipboard` on Wayland), or use the terminal's paste (`Ctrl+Shift+V`). |
| Import fails with `contains one or more user objects` | Import into a new database name; sqlpackage only imports into an empty database. |
| `sqlpackage cannot read a connection string or file path containing a double quote` | sqlpackage's response file cannot hold `"`; change that password or path. |
| macOS says the binary "cannot be opened" | It was downloaded with a browser: `xattr -d com.apple.quarantine /usr/local/bin/lazytsql`. |
| Completion shows nothing for a table | Its names load in the background the first time; wait a moment, or press `r` in the schema panel after schema changes. |
| A query in the TUI runs forever | The TUI has no command timeout; press `Ctrl+K` to cancel. |
| `Could not write the activity log` | The log directory is not writable; fix its permissions or set `LAZYTSQL_LOG_DIR`. |

Integrated (Windows) and Microsoft Entra ID authentication are not yet verified in the native binary.

Still stuck? [Open an issue](https://github.com/carandev/lazytsql/issues) with the command, the error and the output
of `lazytsql --version`.
