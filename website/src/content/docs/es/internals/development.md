---
title: Desarrollo
description: Compila, prueba y ejecuta lazytsql desde el código fuente, y conoce cómo está organizado el código.
---

## Compilar y ejecutar

```bash
git clone https://github.com/carandev/lazytsql.git
cd lazytsql
dotnet build
dotnet run --project src/LazyTsql -- -c 'Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=true'
```

`dotnet run` usa el JIT, lo cual está bien para desarrollar; `dotnet publish -r <rid>` produce el binario nativo.

## Pruebas

```bash
dotnet test
```

| Proyecto | Cubre |
| --- | --- |
| `LazyTsql.Core.Tests` | División en lotes, autocompletado, diagnósticos, `IsSingleQuery` |
| `LazyTsql.Tests` | Análisis y comandos de la CLI (con dobles), escritores de salida, archivos, registro de actividad, comprobación de actualizaciones, lógica de la TUI |
| `LazyTsql.SqlServer.Tests` | La sesión de SqlClient y sqlpackage contra un servidor real |

Las pruebas de SQL Server se omiten a menos que `LAZYTSQL_TEST_CONNECTION` apunte a un servidor desechable. El ciclo de exportación e importación
de `.bacpac` también necesita sqlpackage:

```bash
docker run -d --name lazytsql-dev-test -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=LazyTsql_Dev_2026!' \
  -p 14339:1433 mcr.microsoft.com/mssql/server:2022-latest
export LAZYTSQL_TEST_CONNECTION='Server=localhost,14339;User Id=sa;Password=LazyTsql_Dev_2026!;TrustServerCertificate=true'
dotnet test
docker rm -f lazytsql-dev-test
```

Las pruebas crean sus propias bases de datos con nombres aleatorios y las eliminan después; aun así, nunca las apuntes a un
servidor que te importe.

## Mapa del código

```text
src/
  LazyTsql.Core/         dominio y puertos, sin dependencias
    Batches/             división por GO, IsSingleQuery
    Execution/           ScriptRunner, resultados y mensajes
    Completion/          analizador léxico, SqlCompletion, SchemaCatalog
    Syntax/              clasificador de resaltado
    Schema/              DbObject, columnas, claves foráneas
    Connections/         ConnectionProfile, expansión de ${VAR}
  LazyTsql.SqlServer/    SqlServerSession, SqlPackageTool
  LazyTsql/
    Program.cs           punto de entrada y conexión de componentes
    Cli/                 argumentos y comandos headless
    Output/              escritores de texto, JSON y CSV
    Infrastructure/      archivo de conexiones, scripts, registro de actividad, comprobación de actualizaciones
    Tui/                 vistas y diálogos de Terminal.Gui
website/                 este sitio (Astro Starlight)
```

## Este sitio web

```bash
cd website
npm ci
npm run dev       # http://localhost:4321/lazytsql/
npm run build     # static site in website/dist
```

Las páginas son archivos Markdown en `website/src/content/docs/`, con la traducción al español de cada una en la misma ruta bajo `es/`. Las pantallas de la página principal son salida real de lazytsql,
capturada desde una terminal con `tmux capture-pane -e` y convertida a HTML.
