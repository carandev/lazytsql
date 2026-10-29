---
title: La TUI
description: Los seis paneles, cómo funcionan el foco y las teclas, y cómo iniciar lazytsql con una conexión, un script o una carpeta.
---

## Cómo iniciarla

```bash
lazytsql                        # pick or create a connection in panel [1]
lazytsql -c dev                 # connect on start (a saved name or a raw connection string)
lazytsql -c dev migration.sql   # edit a script (created on the first save if it does not exist)
lazytsql -c dev ./scripts       # browse a folder of scripts in panel [2]
```

Sin `-c`, lazytsql se conecta al iniciar cuando `LAZYTSQL_CONNECTION` está definida.

## Diseño

```text
┌ [1] Connections ┐┌ [4] Query · file.sql · dev › shop ───────────┐
│                 ││                                              │
├ [2] Files ──────┤└──────────────────────────────────────────────┘
│                 │┌ [5] Results · 5 rows ────────────────────────┐
├ [3] Schema ─────┤│                                              │
│                 │└──────────────────────────────────────────────┘
│                 │┌ [6] Messages ────────────────────────────────┐
└─────────────────┘└──────────────────────────────────────────────┘
 key hints for the focused panel                     │ global keys
```

| Panel | Muestra | Título |
| --- | --- | --- |
| `[1]` Connections | Conexiones guardadas, ordenadas por nombre | |
| `[2]` Files | Carpetas y archivos `.sql` de un directorio | |
| `[3]` Schema | Bases de datos → Tables / Views / Procedures / Functions → columnas | |
| `[4]` Query | El editor SQL | El archivo abierto (`*` cuando no está guardado), la conexión y la base de datos actual |
| `[5]` Results | Un conjunto de resultados a la vez | Su posición (`2/3 [ ]`) y el número de filas, `first N rows` cuando el límite de filas lo recorta |
| `[6]` Messages | Mensajes del servidor, errores y los mensajes propios de lazytsql | `· update available` cuando hay una versión más reciente |

## Foco y teclas

- `1`-`6` enfocan un panel; `Tab` pasa al siguiente. Dentro del editor, los dígitos son texto, así que usa `Alt+1`-`Alt+6`.
- `Esc` sale del editor y enfoca los resultados.
- Fuera del editor, los movimientos de vim funcionan en todas partes: `h` `j` `k` `l`, `g`/`G` para ir al inicio y al final, `Ctrl+D`/`Ctrl+U`
  para avanzar o retroceder una página.
- La línea inferior siempre muestra las teclas del panel enfocado o del diálogo abierto, seguidas de las teclas globales que
  siguen funcionando ahí. `?` (o `F1` dentro del editor) abre la ayuda del panel actual; `a` en la ayuda cambia a todas
  las teclas.
- `q` y `Esc` salen fuera del editor (dentro de él, `Esc` sale del editor), `Ctrl+Q` en cualquier lugar. Salir siempre pide confirmación y avisa cuando el script abierto tiene cambios
  sin guardar.

Todas las teclas están en la [referencia de teclas](/lazytsql/es/reference/keys/).

## Trabajo en segundo plano

Las consultas, la carga del esquema, las exportaciones y las importaciones se ejecutan en segundo plano, así que la pantalla sigue respondiendo. Comparten
la única conexión, por lo que se ejecutan de una en una, en orden. `Ctrl+K` cancela la consulta, exportación o importación en curso.

## Colores

La interfaz conserva el fondo de la terminal y usa sus 16 colores ANSI, así que sigue el tema de tu terminal: verde de acento,
gris atenuado, azul de selección y nombres desconocidos subrayados en rojo.

## Selector de archivos

Toda ruta (abrir, guardar, exportar) pasa por un selector de archivos:

- `Tab` completa la ruta escrita; `~` es tu directorio personal.
- La lista de abajo muestra las carpetas y los archivos que coinciden, y se filtra mientras escribes. Las entradas ocultas aparecen cuando escribes
  el punto inicial.
- `↓` entra en la lista, donde `j`/`k` mueven, `Enter`/`l` abre una carpeta o elige un archivo, `h`/`Backspace` sube un nivel,
  y `k` en la primera fila regresa a la ruta.
- Un nombre sin extensión recibe la esperada (`.sql`, `.csv`, `.bacpac`).
