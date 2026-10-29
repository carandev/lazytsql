---
title: Cómo se ejecutan los scripts
description: División en lotes, el ejecutor de scripts, el límite de filas, los mensajes del servidor, la cancelación y las exportaciones por transmisión.
---

## 1. División en lotes

SQL Server ejecuta un *lote* a la vez; `GO` no es T-SQL, sino un separador que entienden las herramientas cliente. El
`BatchSplitter` de lazytsql (`LazyTsql.Core/Batches`) hace lo mismo que SSMS y sqlcmd:

- Una línea que solo contiene `GO`, opcionalmente seguido de un número y un comentario, termina un lote:
  `^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?$`, sin distinguir mayúsculas de minúsculas.
- Solo cuenta cuando el divisor está **fuera** de un comentario de bloque (incluidos los anidados), de una `'string'`, de un
  `"quoted name"` o de un `[bracketed name]`, que sigue a lo largo de varias líneas.
- `GO 5` le da al lote un número de repeticiones de 5.
- Cada lote recuerda su `StartLine` (base 1) en el script, para que los mensajes puedan señalar la línea correcta.
- Los lotes que solo contienen espacios en blanco se descartan; los finales de línea se conservan tal como se escribieron.

## 2. Ejecución de lotes

`ScriptRunner` ejecuta cada lote `RepeatCount` veces mediante `IDatabaseSession.ExecuteAsync` y produce un
`BatchResult` por ejecución: conjuntos de resultados, mensajes, filas afectadas y tiempo transcurrido. **Se detiene después
del primer lote con un error**, como `sqlcmd -b`: un error es cualquier mensaje con severidad mayor que 10.

## 3. Lectura de resultados con un límite

`QueryOptions.MaxRows` (1000 por defecto, 0 para todas las filas) limita cada conjunto de resultados. `SqlServerSession` lee filas hasta
el límite y marca el conjunto de resultados como truncado. Lo que pasa con el resto depende del lote:

- **Un `SELECT` aislado** (`SqlBatch.IsSingleQuery`): lazytsql llama a `SqlCommand.Cancel()`, que le indica al servidor que
  deje de producir filas, e ignora el error de cancelación al cerrar el lector. Un `SELECT *` sobre una tabla de
  millones de filas responde en milisegundos.
- **Cualquier otra cosa**: las filas restantes se leen y se descartan, de modo que las sentencias posteriores del lote (un `INSERT` después
  de un `SELECT`, por ejemplo) igual se ejecutan. Cancelar ahí las abortaría.

`IsSingleQuery` es una comprobación conservadora de tokens: el lote debe empezar con un `SELECT` sin corchetes; cualquier palabra de sentencia
(`INSERT`, `UPDATE`, `DELETE`, `MERGE`, `EXEC`, `INTO`, `SET`, `DECLARE`…) lo vuelve falso; un `;` solo se permite al
final; otro `SELECT` se permite solo dentro de paréntesis o después de `UNION`, `EXCEPT`, `INTERSECT` o `ALL`. Ante
la duda responde falso, lo cual siempre es seguro.

## 4. Mensajes y números de línea

SqlClient informa los mensajes de `PRINT` y de baja severidad mediante `InfoMessage`, y los errores mediante `SqlException`; ambos
se convierten en `ServerMessage(Text, Number, Severity, LineNumber)`. El número de línea del servidor es relativo al lote, así que
cada escritor lo convierte en una línea del script con `StartLine + max(LineNumber, 1) - 1`. Por eso los errores en la TUI,
la salida de texto y el campo `line` del JSON señalan la línea que escribiste.

## 5. Cancelación

El token de cancelación llega a `ExecuteReaderAsync` y a `ReadAsync`, así que `Ctrl+K` (o `Ctrl+C` en la CLI) detiene una
consulta en ejecución en el servidor. En la TUI, el trabajo espera un semáforo de una sola plaza porque una sesión tiene una sola
conexión; cancelar una consulta lo libera para la siguiente operación.

## 6. Exportaciones por transmisión

Cuando exportas a CSV un conjunto de resultados limitado, lazytsql no tiene las filas. `StreamResultSetAsync` vuelve a ejecutar el lote,
salta al mismo índice de conjunto de resultados y entrega cada fila a un `IRowSink` (`CsvRowWriter`) a medida que llega, sin
límite de filas. La memoria se mantiene estable sea cual sea el tamaño. La TUI pregunta antes de hacerlo porque los efectos secundarios del lote
se ejecutan de nuevo, se niega si la conexión cambió desde la ejecución y elimina el archivo parcial si se cancela o falla.

## 7. Memoria

Antes de cada ejecución se liberan los resultados anteriores; después de un resultado de 100,000 filas o más, la TUI le pide al recolector
de basura que compacte el heap y le devuelva la memoria al sistema operativo. Las celdas se formatean solo cuando la
tabla las dibuja.
