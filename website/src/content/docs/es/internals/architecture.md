---
title: Arquitectura
description: Los tres proyectos, los puertos entre ellos y cómo una tecla se convierte en una consulta en el servidor.
---

lazytsql es una aplicación .NET 10 compilada con Native AOT. Sigue una estructura de puertos y adaptadores (hexagonal):
el dominio no sabe nada de SQL Server ni de la terminal, y las capas externas se conectan a él mediante interfaces.

```text
┌──────────────────────────── LazyTsql (app) ─────────────────────────────┐
│  Program.cs ── CLI (CliArguments, HeadlessCommands) ── Output writers   │
│       │                                                                 │
│       └────── TUI (Terminal.Gui: MainView, SqlEditor, dialogs)          │
│  Infrastructure: connections file, scripts, activity log, update check  │
└───────────────┬───────────────────────────────────────┬─────────────────┘
                │ uses ports                            │ wires adapters
┌───────────────▼──────────── LazyTsql.Core ────────────┴─────────────────┐
│  Ports: IDatabaseConnector, IDatabaseSession, IBacpacTool, IRowSink     │
│  Batches (GO splitting) · Execution (ScriptRunner) · Schema models      │
│  Completion (lexer, SqlCompletion, SchemaCatalog) · Syntax highlighting │
└───────────────▲─────────────────────────────────────────────────────────┘
                │ implements
┌───────────────┴──────────── LazyTsql.SqlServer ─────────────────────────┐
│  SqlServerConnector · SqlServerSession (Microsoft.Data.SqlClient)       │
│  SqlPackageTool (runs sqlpackage)                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Proyectos

| Proyecto | Depende de | Contiene |
| --- | --- | --- |
| `LazyTsql.Core` | nada | Los puertos, la división en lotes, la ejecución de scripts, los modelos de esquema, el motor de autocompletado y la clasificación de sintaxis. Lógica pura, con pruebas unitarias completas. |
| `LazyTsql.SqlServer` | Core, Microsoft.Data.SqlClient | Los adaptadores que se comunican con SQL Server y con sqlpackage. |
| `LazyTsql` | Core, SqlServer, Terminal.Gui | El ejecutable: análisis de argumentos, comandos headless, escritores de texto/JSON/CSV, la TUI y los archivos locales. |

Cada uno tiene un proyecto de pruebas: `LazyTsql.Core.Tests` (lógica pura), `LazyTsql.Tests` (CLI, salida, lógica de la TUI, archivos) y
`LazyTsql.SqlServer.Tests` (pruebas de integración contra un SQL Server real, que se omiten si no hay uno).

## Puertos

Definidos en `LazyTsql.Core`:

| Puerto | Propósito | Adaptador |
| --- | --- | --- |
| `IDatabaseConnector` | Abre una sesión a partir de una cadena de conexión | `SqlServerConnector` |
| `IDatabaseSession` | Una conexión abierta: ejecutar un lote, transmitir un conjunto de resultados, cambiar de base de datos, leer bases de datos, objetos, columnas, claves foráneas, sinónimos y definiciones | `SqlServerSession` |
| `IBacpacTool` | Exportar e importar archivos `.bacpac` | `SqlPackageTool` |
| `IRowSink` | Recibe filas transmitidas (`Begin` con las columnas, luego `Row`) | `CsvRowWriter` |

No hay un contenedor de inyección de dependencias: `Program.cs` crea los adaptadores y los pasa a la CLI o a la TUI.
Eso mantiene el arranque instantáneo y el binario AOT pequeño, y las pruebas construyen los mismos objetos con dobles (fakes).

## De la tecla a las filas

1. `F5` llega a `MainView.OnGlobalKeyDown`, que llama a `RunQuery` con la selección o con todo el texto del editor.
2. `RunInBackground` saca el trabajo del hilo de la interfaz y espera la compuerta de la sesión, porque una sesión tiene una
   sola conexión y ejecuta una operación a la vez.
3. `ScriptRunner` divide el texto en lotes con `BatchSplitter` y ejecuta cada uno mediante
   `IDatabaseSession.ExecuteAsync`.
4. `SqlServerSession` ejecuta el lote con SqlClient, lee cada conjunto de resultados hasta el límite de filas y recopila
   la salida de `PRINT` y los errores con sus números de línea.
5. Cada `BatchResult` vuelve al hilo de la interfaz mediante `App.Invoke`, donde los mensajes se registran y los conjuntos
   de resultados se muestran en un `TableView`.

El comando headless `query` ejecuta el mismo `ScriptRunner` y, en su lugar, pasa los resultados a un escritor de texto,
JSON o CSV. Los detalles están en [cómo se ejecutan los scripts](/lazytsql/es/internals/execution/).

## Decisiones tecnológicas

| Decisión | Por qué |
| --- | --- |
| .NET 10 + Native AOT | Un único binario autocontenido de unos 21 MB que arranca al instante y no necesita runtime en el servidor. |
| Microsoft.Data.SqlClient | El controlador oficial: TDS, cifrado, `PRINT` y detalles de errores con números de línea, cancelación. |
| Terminal.Gui 2 | Un kit de herramientas TUI maduro para .NET con vistas de árbol, tabla y texto, diálogos y un motor de diseño. |
| sqlpackage (externo) | La herramienta propia de Microsoft es la única implementación confiable de `.bacpac`; lazytsql la controla en lugar de reimplementarla. |
| JSON sin reflexión | Generación de código fuente de `System.Text.Json` y `Utf8JsonWriter`, que AOT admite sin advertencias de trimming (recorte). |
