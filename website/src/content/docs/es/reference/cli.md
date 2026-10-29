---
title: Línea de comandos
description: Todos los comandos y opciones de lazytsql, tal como los muestra lazytsql --help.
---

```text
lazytsql [-c <connection>] [<file.sql> | <dir>]  Open the TUI, optionally editing a script or browsing a folder of scripts
lazytsql query  [-c <conn>] [-d <db>] (-q <sql> | -f <file> | stdin) [--json | --csv] [--max-rows N] [--timeout S]
lazytsql schema [-c <conn>] [-d <db>] [<schema.object>] [--json]
lazytsql databases [-c <conn>]
lazytsql export [-c <conn>] [-d <db>] <file.bacpac>     Export a database with sqlpackage
lazytsql import [-c <conn>] [-d <new db>] <file.bacpac> Import a .bacpac as a new database
lazytsql connections [list | add <name> <connection string> | remove <name>]
lazytsql --version
lazytsql --help
```

## Comandos

| Comando | Qué hace | Salida |
| --- | --- | --- |
| *(ninguno)* | Abre la TUI. Una ruta `.sql` la abre en el editor; un directorio la abre en el panel de archivos. | |
| `query` | Ejecuta SQL desde `-q`, `-f` o stdin, lote por lote, y se detiene en el primer lote que falla | Texto, `--json` o `--csv` |
| `schema` | Lista objetos; con `<schema.object>`, las columnas de una tabla o la definición de una rutina | Texto o `--json` (las definiciones siempre son texto) |
| `databases` | Lista las bases de datos a las que tienes acceso | Texto |
| `export` | Exporta una base de datos a `.bacpac` con sqlpackage | Progreso en stderr |
| `import` | Importa un `.bacpac` en una base de datos nueva con sqlpackage | Progreso en stderr |
| `connections` | Lista, agrega o elimina conexiones guardadas | Texto |

## Opciones

| Opción | Comandos | Significado |
| --- | --- | --- |
| `-c`, `--connection` | todos | Un nombre guardado (sin distinguir mayúsculas y minúsculas) o una cadena de conexión directa. Por defecto, `$LAZYTSQL_CONNECTION`. |
| `-d`, `--database` | query, schema, export, import | La base de datos que se usará. Para export/import, por defecto es la de la conexión. |
| `-q`, `--query` | query | El SQL que se ejecutará |
| `-f`, `--file` | query | Un archivo con el SQL que se ejecutará |
| `-m`, `--max-rows` | query | Filas por conjunto de resultados, al menos 1 (por defecto 1000) |
| `-t`, `--timeout` | query | Tiempo de espera del comando en segundos (por defecto 30) |
| `--json` | query, schema | Salida JSON, consulta [Salida JSON](/lazytsql/es/reference/json-output/) |
| `--csv` | query | Filas como CSV en stdout, mensajes en stderr |
| `--version` | | Muestra la versión |
| `-h`, `--help` | | Muestra el uso |

`--json` y `--csv` no se pueden combinar.

## Códigos de salida

| Código | Significado |
| --- | --- |
| `0` | Éxito |
| `1` | Errores de SQL en al menos un lote, o falló sqlpackage |
| `2` | Error de uso, comando desconocido o error de conexión |

Cuando se indica `--json`, un error que detiene el comando se imprime como `{"success":false,"error":"<message>"}` en
stdout; de lo contrario, se imprime como `lazytsql: <message>` en stderr.
