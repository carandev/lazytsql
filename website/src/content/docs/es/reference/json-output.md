---
title: Salida JSON
description: La forma exacta de la salida --json de lazytsql para consultas, objetos, columnas y errores.
---

Todo el JSON es un único documento en stdout, en UTF-8, sin escapar caracteres no ASCII.

## `query --json`

```json
{
  "success": false,
  "batches": [
    {
      "startLine": 3,
      "elapsedMs": 5.9,
      "recordsAffected": -1,
      "messages": [
        { "level": "error", "number": 208, "severity": 16, "line": 3, "text": "Invalid object name 'nope'." }
      ],
      "resultSets": [
        { "columns": [{ "name": "id", "type": "int" }], "rows": [[1]], "truncated": false }
      ]
    }
  ]
}
```

| Campo | Significado |
| --- | --- |
| `success` | `false` cuando algún lote tuvo un error (severidad mayor que 10) |
| `batches` | Los lotes que se ejecutaron, en orden; la ejecución se detiene después del primero con un error |
| `startLine` | La línea donde empieza el lote en el script enviado (empezando en 1) |
| `elapsedMs` | Cuánto tardó el lote, en milisegundos |
| `recordsAffected` | Filas modificadas por `INSERT`/`UPDATE`/`DELETE`/`MERGE`, o `-1` cuando no se ejecutó ninguno |
| `messages[].level` | `error` (severidad mayor que 10) o `info` (`PRINT`, `RAISERROR` con severidad baja) |
| `messages[].line` | La línea en el **script enviado**, no en el lote |
| `resultSets[].columns` | Nombres de columna y tipos de SQL Server |
| `resultSets[].rows` | Filas como arreglos, en el orden de las columnas |
| `resultSets[].truncated` | `true` cuando el conjunto de resultados alcanzó `--max-rows` |

### Valores

| Tipo de SQL Server | JSON |
| --- | --- |
| `NULL` | `null` |
| `bit` | `true` / `false` |
| enteros, `decimal`, `numeric`, `money`, `float`, `real` | número (los decimales conservan sus dígitos exactos) |
| `uniqueidentifier`, cadenas | cadena |
| `date`, `datetime`, `datetime2`, `datetimeoffset` | cadena ISO 8601 |
| `binary`, `varbinary`, `image` | cadena `"0x..."` |
| cualquier otro | su forma de texto como cadena |

## `schema --json`

Objetos de una base de datos:

```json
{ "database": "app", "objects": [{ "schema": "dbo", "name": "users", "kind": "table" }] }
```

`kind` es `table`, `view`, `procedure` o `function`.

Columnas de una tabla o vista (`schema dbo.users --json`):

```json
{
  "schema": "dbo",
  "name": "users",
  "columns": [{ "name": "id", "type": "int", "nullable": false, "primaryKey": true, "identity": true }]
}
```

`type` incluye longitud, precisión y escala: `nvarchar(100)`, `decimal(10,2)`, `varchar(max)`.

## Errores

Un error que detiene el comando (uso incorrecto, conexión fallida) reemplaza todo el documento:

```json
{ "success": false, "error": "Login failed for user 'sa'." }
```

El código de salida distingue ambos casos: `1` para errores de SQL dentro de `batches`, `2` para esta forma.
