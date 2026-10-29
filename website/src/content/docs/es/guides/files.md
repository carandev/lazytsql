---
title: Panel de archivos
description: Explora una carpeta de scripts .sql y ábrelos en el editor.
---

El panel de archivos (`2`) muestra una carpeta de scripts como un árbol, así que las consultas, migraciones o reportes de un proyecto están a una
tecla de distancia.

## Qué carpeta

1. La carpeta indicada en la línea de comandos: `lazytsql -c dev ./scripts`.
2. Si no, la carpeta del script indicado en la línea de comandos: `lazytsql -c dev scripts/report.sql`.
3. Si no, el panel queda vacío, sin leer nada del disco, hasta que presionas `o`.

`o` abre un selector de carpetas en cualquier momento: escribe o navega hasta una carpeta (`Enter` sobre una carpeta de
la lista entra en ella) y presiona `Enter` en la ruta, o *Open*, para mostrarla en el panel. Abrir solo la carpeta en la
que trabajas mantiene pequeños el árbol y la búsqueda.

El árbol muestra primero las subcarpetas y luego los archivos `.sql` (sin importar mayúsculas: también `.SQL`), cada grupo ordenado por nombre. Las carpetas ocultas
como `.git` se omiten, y una carpeta que no se puede leer aparece vacía.

## Teclas

| Tecla | Acción |
| --- | --- |
| `Enter` | Abrir el script seleccionado en el editor, o expandir/contraer una carpeta |
| `l` / `h` | Expandir / contraer una carpeta |
| `j` / `k` | Moverse |
| `/` | Buscar, como en vim (ver abajo) |
| `n` / `N` | Coincidencia siguiente / anterior de la última búsqueda |
| `o` | Abrir otra carpeta |
| `r` | Actualizar después de que los archivos cambien en el disco |

Abrir un script pregunta primero cuando el editor tiene cambios sin guardar. Guardar un script nuevo con `Ctrl+S` actualiza el
árbol, y una actualización mantiene expandidas las mismas carpetas.

## Búsqueda

`/` busca carpetas y scripts **en cualquier nivel**, no solo los visibles, como en vim:

1. Escribe parte de un nombre; la primera coincidencia se selecciona mientras escribes y se expanden las carpetas que
   la contienen. El título muestra `/consulta 1/3`, o `no match`.
2. `Enter` se queda en la coincidencia; `Esc` vuelve a donde estabas. `Backspace` borra un carácter y, con la búsqueda
   vacía, la cancela.
3. Después, `n` y `N` van a la coincidencia siguiente y anterior, y vuelven a empezar al llegar al final.

La búsqueda corre en segundo plano, así que escribir nunca espera al disco, y cada tecla cancela la búsqueda anterior.
No distingue mayúsculas ni minúsculas, omite las carpetas ocultas, no sigue enlaces a otras carpetas y se detiene
después de 20 000 entradas.
