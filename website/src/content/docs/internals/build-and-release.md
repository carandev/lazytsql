---
title: Build and release
description: How the native binaries and the dotnet tool are built and published on every push to main.
---

## Native AOT

`src/LazyTsql/LazyTsql.csproj` publishes with Native AOT: the C# code and the parts of .NET it uses are compiled
ahead of time into one native executable, with no JIT and no runtime to install.

| Setting | Effect |
| --- | --- |
| `PublishAot=true` | Native compilation; trimming removes unused code |
| `OptimizationPreference=Size` | Smaller binary over marginal speed |
| `StripSymbols`, `CopyOutputSymbolsToPublishDirectory=false` | Debug symbols stay out of the binary and the package |
| `EventSourceSupport=false` | Drops tracing infrastructure lazytsql does not use |
| Workstation, non-concurrent GC | Lower memory for a single-user tool |

The result is about 21 MB per platform. AOT only compiles for the operating system it runs on, so each platform is
built on its own runner. Microsoft.Data.SqlClient reports trim warnings for features lazytsql does not use (they are
listed as warnings, not errors); integration tests run against SQL Server to catch a real break.

Linux binaries need ICU (`libicu`) at runtime for culture-aware text, which SqlClient requires.

## The dotnet tool

lazytsql is also a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) on NuGet.org, using the
.NET 10 RID-specific tool packages:

- `PackAsTool` with `ToolPackageRuntimeIdentifiers=linux-x64;linux-arm64;win-x64;osx-arm64;any`.
- One package per platform (`lazytsql.linux-x64`, …) holds that platform's **native AOT binary**.
- A small pointer package, `lazytsql`, is what users install; the .NET CLI picks the package for the current
  platform.
- `lazytsql.any` is a framework-dependent build (`PublishAot=false`) for any other platform with the .NET 10
  runtime.

## The release workflow

`.github/workflows/release.yml` runs on every push to `main` (and by hand). The version is `0.1.<run number>`.

```text
test ──► build (linux-x64 · linux-arm64 · win-x64 · osx-arm64) ──► publish
```

1. **test** (`ubuntu-24.04`): starts SQL Server 2022 as a service container, installs sqlpackage, builds, waits
   until the server answers `SELECT 1`, and runs every test, including the integration tests and a real `.bacpac`
   round trip.
2. **build** (a matrix, each on its own OS, including an ARM Linux runner): `dotnet publish -r <rid>` for the
   binary, `dotnet pack -r <rid>` for the platform tool package, and a `lazytsql-<rid>.tar.gz` (`.zip` on Windows).
3. **publish** (`ubuntu-24.04`, the only job with write permission):
   - packs the pointer package and the `any` fallback;
   - pushes to NuGet.org with `--skip-duplicate`: platform packages first, the pointer last, because installing the
     pointer needs every platform package to exist;
   - creates the GitHub release `v<version>` with the archives and generated notes, or uploads to it if it exists.

A failed publish can be re-run safely: NuGet skips packages it already has and the release is updated, not
duplicated.

The website is built and deployed to GitHub Pages by a separate workflow, only when `website/` changes.
