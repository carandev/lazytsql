---
title: Modo headless
description: Ejecuta consultas, lee el esquema y administra conexiones desde scripts, trabajos de CI y agentes de programación, con salida JSON o CSV y códigos de salida significativos.
---

Todos los comandos, salvo la TUI, escriben en stdout y terminan, así que lazytsql encaja en scripts de shell, trabajos de CI y agentes
de programación que necesitan consultar una base de datos.

## Consultas

```bash
lazytsql query -c dev -q "SELECT TOP 5 * FROM dbo.users" --json
lazytsql query -c dev -f migration.sql                                  # GO batches, stops at the first failing batch
lazytsql query -c dev -q "SELECT * FROM dbo.users" --csv > users.csv    # rows on stdout, messages on stderr
echo "SELECT 1" | lazytsql query -c dev --json                          # SQL from stdin
```

El SQL viene de `-q`; si no, de `-f`; si no, de stdin cuando está redirigido. Los lotes se ejecutan en orden y se detienen en el primero
con un error, exactamente como en la TUI.

| Opción | Significado | Predeterminado |
| --- | --- | --- |
| `-c`, `--connection` | Nombre guardado o cadena de conexión directa | `$LAZYTSQL_CONNECTION` |
| `-d`, `--database` | Base de datos a la que cambiar antes de ejecutar | La de la conexión |
| `-m`, `--max-rows` | Filas por conjunto de resultados; debe ser al menos 1 | 1000 |
| `-t`, `--timeout` | Tiempo de espera del comando en segundos | 30 |
| `--json` / `--csv` | Formato de salida | Texto |

## Formatos de salida

- **Texto** (predeterminado): conjuntos de resultados como tablas alineadas (columnas limitadas a 40 caracteres) y mensajes en formato sqlcmd,
  `Msg 208, Level 16, Line 3: Invalid object name 'nope'.`
- **JSON** (`--json`): un documento con cada lote, sus mensajes y sus conjuntos de resultados. La estructura está en la
  [referencia de JSON](/lazytsql/es/reference/json-output/). Los errores que detienen el comando, como un inicio de sesión fallido, también son
  JSON: `{"success":false,"error":"..."}`.
- **CSV** (`--csv`): solo filas en stdout, un bloque por conjunto de resultados separado por una línea en blanco; los mensajes van a stderr.
  Sin BOM, así que se canaliza limpiamente hacia otras herramientas.

## Esquema

```bash
lazytsql databases -c dev                         # databases you can access
lazytsql schema -c dev -d app --json              # tables, views, procedures, functions
lazytsql schema -c dev -d app dbo.users --json    # columns of a table or view
lazytsql schema -c dev -d app dbo.get_users       # definition of a procedure or function
lazytsql schema -c dev -d app users               # no schema means dbo
```

## Conexiones

```bash
lazytsql connections list
lazytsql connections add dev 'Server=...;Password=${DEV_DB_PASSWORD}'
lazytsql connections remove dev
```

## Códigos de salida

| Código | Significado |
| --- | --- |
| `0` | Éxito |
| `1` | El SQL se ejecutó y al menos un lote tuvo un error, o sqlpackage falló |
| `2` | Error de uso, comando desconocido o la conexión falló |

## Uso desde un agente de programación

La salida `--json` está pensada para que la lean programas: los números de línea apuntan al script enviado, los números siguen siendo números,
los valores binarios son cadenas `0x...` y `truncated` indica cuándo un conjunto de resultados alcanzó el límite de filas. Un ciclo típico de un agente:

```bash
lazytsql schema -c dev -d app --json                  # discover tables
lazytsql schema -c dev -d app dbo.orders --json       # read columns
lazytsql query -c dev -d app -q "SELECT TOP 20 * FROM dbo.orders" --json
```
