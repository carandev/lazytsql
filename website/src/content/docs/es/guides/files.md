---
title: Panel de archivos
description: Explora una carpeta de scripts .sql y ábrelos en el editor.
---

El panel de archivos (`2`) muestra una carpeta de scripts como un árbol, así que las consultas, migraciones o reportes de un proyecto están a una
tecla de distancia.

## Qué carpeta

1. La carpeta indicada en la línea de comandos: `lazytsql -c dev ./scripts`.
2. Si no, la carpeta del script indicado en la línea de comandos: `lazytsql -c dev scripts/report.sql`.
3. Si no, el directorio de trabajo.

El árbol muestra primero las subcarpetas y luego los archivos `.sql` (sin importar mayúsculas: también `.SQL`), cada grupo ordenado por nombre. Las carpetas ocultas
como `.git` se omiten, y una carpeta que no se puede leer aparece vacía.

## Teclas

| Tecla | Acción |
| --- | --- |
| `Enter` | Abrir el script seleccionado en el editor, o expandir/contraer una carpeta |
| `l` / `h` | Expandir / contraer una carpeta |
| `j` / `k` | Moverse |
| `r` | Actualizar después de que los archivos cambien en el disco |

Abrir un script pregunta primero cuando el editor tiene cambios sin guardar. Guardar un script nuevo con `Ctrl+S` actualiza el
árbol, y una actualización mantiene expandidas las mismas carpetas.
