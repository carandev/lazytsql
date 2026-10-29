---
title: Update notice
description: How the TUI finds out about a newer release, and how to turn it off.
---

When a newer release exists, the TUI says so in the messages panel and in its title:

```text
Update available: 0.1.2 → 0.1.5 · dotnet tool update -g lazytsql
```

## How it works

1. After the screen is up, a background task asks NuGet.org for the published versions of `lazytsql`
   (`https://api.nuget.org/v3-flatcontainer/lazytsql/index.json`). Nothing else is sent.
2. Prereleases are ignored. When the newest version is above yours, the notice appears.
3. The answer is cached in `~/.local/state/lazytsql/latest-version`, so NuGet is asked **at most once a day**.
4. Offline, a slow answer (5 second timeout) or an unexpected one shows nothing.

The notice suggests the right way to update:

- installed as a .NET tool (the executable is under `~/.dotnet/tools`): `dotnet tool update -g lazytsql`;
- a downloaded binary: the [latest release page](https://github.com/carandev/lazytsql/releases/latest).

lazytsql never downloads or replaces anything by itself.

## Turning it off

```bash
export LAZYTSQL_NO_UPDATE_CHECK=1
```

Any non-empty value turns the check off.
