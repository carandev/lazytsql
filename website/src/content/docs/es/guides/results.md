---
title: Resultados y exportación
description: Conjuntos de resultados, la edición de celdas, el límite de filas, la memoria y la exportación de todas las filas a CSV.
---

## Conjuntos de resultados

Se conservan todos los conjuntos de resultados de la última ejecución; el panel de resultados muestra uno a la vez. `[` y `]` se mueven entre ellos y
el título muestra la posición (`2/3`) y el número de filas. Las celdas se muestran en una sola línea: los saltos de línea se convierten en `⏎`, las tabulaciones
en espacios y `NULL` se escribe como `NULL`.

Los conteos de filas (`(3 rows affected)`), la salida de `PRINT` y los errores van al panel de mensajes, junto con el tiempo que tardó cada ejecución.

## Editar celdas

Los resultados de un `SELECT` solo se pueden editar en el mismo panel y guardar con un script que revisas antes, como en DataGrip.

1. Pon el cursor en una celda y presiona `e` (o `Enter`). Escribe el valor nuevo; `Ctrl+N` lo deja en `NULL`. El valor
   se valida contra el tipo de la columna: una fecha debe ser `yyyy-MM-dd` y un número no puede ser texto.
2. El valor nuevo se muestra en amarillo y el título cuenta los cambios: `3 rows · 2 unsaved changes (s save)`. `u`
   deshace la celda seleccionada y `U`, todos los cambios.
3. `s` abre una confirmación con el script exacto que se ejecutará. `y` lo ejecuta; `n`, `Esc` o `Enter` cancelan.

```sql
BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE [shop].[dbo].[customers] SET [name] = N'Anna' WHERE [id] = 1;
    IF @@ROWCOUNT <> 1 THROW 50000, N'Row 1 no longer matches exactly one row of [shop].[dbo].[customers]. Nothing was saved.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

### Qué se puede editar

La primera vez que editas, lazytsql le pregunta a SQL Server de dónde viene cada columna
(`sp_describe_first_result_set` en modo browse), compilando la consulta sin volver a ejecutarla. Una celda se puede
cambiar cuando su columna se lee de una tabla y el resultado incluye **todas las columnas de la llave primaria o única
de esa tabla**, así cada cambio identifica exactamente una fila. Una consulta sobre una vista edita la tabla base de la vista.

Quedan de solo lectura, con el motivo en el panel de mensajes:

- Las columnas que calcula la consulta (`UPPER(name)`, `COUNT(*)`, literales).
- Las columnas identity, calculadas y rowversion.
- Las tablas sin llave primaria o única, o cuya llave la consulta no incluye.
- Los tipos sin forma literal: `geography`, `geometry`, `hierarchyid`, `sql_variant`.
- Los resultados de un lote con más de una sentencia y los valores con saltos de línea.

### Guardar

- Cada fila cambiada se convierte en un `UPDATE` por su llave, con el nombre completo de la tabla.
- El script es una sola transacción. Si una fila se borró o su llave cambió desde que se ejecutó la consulta, el
  `UPDATE` no encuentra ninguna fila, la transacción se revierte y **no se guarda nada**.
- Ante cualquier error, los cambios siguen pendientes para que puedas corregirlos o deshacerlos.
- El script queda en el [registro de actividad](/lazytsql/es/guides/activity-log/).
- Ejecutar otra consulta o salir con ediciones sin guardar pregunta antes.

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
