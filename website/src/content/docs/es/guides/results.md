---
title: Resultados y exportación
description: Conjuntos de resultados, el límite de filas, la memoria y la exportación de todas las filas a CSV.
---

## Conjuntos de resultados

Se conservan todos los conjuntos de resultados de la última ejecución; el panel de resultados muestra uno a la vez. `[` y `]` se mueven entre ellos y
el título muestra la posición (`2/3`) y el número de filas. Las celdas se muestran en una sola línea: los saltos de línea se convierten en `⏎`, las tabulaciones
en espacios y `NULL` se escribe como `NULL`.

Los conteos de filas (`(3 rows affected)`), la salida de `PRINT` y los errores van al panel de mensajes, junto con el tiempo que tardó cada ejecución.

## Límite de filas

Cada conjunto de resultados obtiene las primeras **1000 filas** para mantener acotada la memoria. Pulsa `m` en el panel de resultados para cambiar el
límite, o escribe `all` (también `0` o vacío) para obtener todas las filas; se aplica a la siguiente ejecución. El título dice `first 1000 rows`
cuando un conjunto de resultados fue recortado.

Lo que ocurre al llegar al límite depende del lote:

- Cuando el lote es **un único `SELECT`**, lazytsql le pide al servidor que se detenga, así que una tabla enorme no cuesta nada más allá de
  las filas que ves.
- Cualquier otro lote lee y descarta las filas restantes, de modo que todas las sentencias posteriores se siguen ejecutando.

Volver a ejecutar libera primero los resultados anteriores. Como referencia, un millón de filas de tres columnas ocupa unos
220 MB.

## Exportar a CSV

`x` en el panel de resultados exporta el conjunto de resultados actual a un archivo CSV:

- UTF-8 **con BOM**, para que Excel conserve los acentos; comillas según RFC 4180; `NULL` es un campo vacío.
- Si el conjunto de resultados **no** fue recortado, se escriben las filas que están en pantalla.
- Si **sí** fue recortado, lazytsql vuelve a ejecutar su lote y envía cada fila directamente al archivo, sin mantenerlas
  en memoria. Pregunta primero, porque los efectos secundarios del lote también se vuelven a ejecutar.
- `Ctrl+K` cancela y elimina el archivo parcial.
- Si te conectaste a otro lugar después de la ejecución, la exportación se rechaza: vuelve a ejecutar la consulta.

Desde la línea de comandos, `--csv` escribe las filas en stdout; consulta [modo headless](/lazytsql/es/guides/headless/).
