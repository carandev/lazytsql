---
title: Panel de archivos
description: Explora una carpeta de scripts .sql y ábrelos en el editor.
---

El panel de archivos (`2`) muestra una carpeta de scripts como un árbol, así que las consultas, migraciones o reportes de un proyecto están a una
tecla de distancia.

## Tus carpetas

El panel muestra las carpetas que agregas, cada una como un árbol, y **las recuerda**: aparecen cada vez que abres
lazytsql, hasta que las quitas.

- `o` agrega una carpeta: escríbela o navega hasta ella en el selector (`Enter` sobre una carpeta de la lista entra en
  ella) y presiona `Enter` en la ruta, o *Add*.
- Una carpeta indicada en la línea de comandos también se agrega y se recuerda: `lazytsql -c dev ./scripts`.
- La carpeta de un script indicado en la línea de comandos (`lazytsql -c dev scripts/report.sql`) se muestra solo en esa
  sesión.
- `d` sobre una carpeta del primer nivel la quita del panel, después de confirmar. La carpeta y sus archivos siguen en
  el disco.
- Una carpeta recordada que se movió o se borró aparece como `nombre/ (not found)`, para que puedas quitarla.

Sin carpetas, el panel queda vacío y no lee nada del disco. Agregar solo las carpetas en las que trabajas mantiene
pequeños los árboles y la búsqueda. La lista se guarda en `~/.config/lazytsql/folders.json`.

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
| `o` | Agregar una carpeta (se recuerda) |
| `d` | Quitar del panel la carpeta del primer nivel seleccionada |
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

La búsqueda cubre todas las carpetas del panel. Corre en segundo plano, así que escribir nunca espera al disco, y cada tecla cancela la búsqueda anterior.
No distingue mayúsculas ni minúsculas, omite las carpetas ocultas, no sigue enlaces a otras carpetas y se detiene
después de 20 000 entradas.
