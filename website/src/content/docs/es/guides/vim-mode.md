---
title: Modo vim
description: "Edita consultas con modos, movimientos, operadores y comandos : al estilo de Neovim."
---

El editor de consultas puede funcionar como Neovim: un modo **normal** para moverse y editar con comandos, un modo
**insert** para escribir, modos **visual** para seleccionar y una **línea de comandos** para los comandos `:`. Es
opcional y viene apagado; apagado, el editor funciona como siempre.

## Activarlo

Pulsa `F2` en cualquier parte, o escribe `:set vim` (`:set novim` lo apaga). lazytsql recuerda la elección para la
siguiente sesión. El pie muestra el modo (`-- NORMAL --`, `-- INSERT --`, `-- VISUAL --`, `-- VISUAL LINE --`), las
teclas de un comando aún sin terminar y lo que escribes después de `:`, `/` o `?`.

En modo vim `Esc` es de vim, así que ya no sale del editor. Muévete entre paneles como entre ventanas de vim:
`Ctrl+H`, `Ctrl+J`, `Ctrl+K` y `Ctrl+L` van al panel de la izquierda, de abajo, de arriba y de la derecha, desde
cualquier panel y en cualquier modo (`Alt+1`-`Alt+6` también siguen funcionando). Como `Ctrl+K` mueve, **`Ctrl+C`
cancela** la consulta en curso (o `:cancel`). `F5`, `Ctrl+S`, `Ctrl+T`, `Ctrl+W` y las demás teclas globales siguen
funcionando en todos los modos.

## Modo normal

Los conteos funcionan como en vim: `3j`, `2dd`, `d2w`.

| Teclas | Acción |
| --- | --- |
| `h` `j` `k` `l` (o las flechas) | Moverse |
| `w` `b` `e`, `W` `B` `E` | Palabra siguiente / anterior / final de palabra (las palabras `W` son todo menos espacios) |
| `0` `^` `$` | Inicio de la línea / primer carácter no blanco / final de la línea |
| `gg` / `G`, `5G` | Primera / última línea, línea 5 |
| `f` `F` `t` `T` + carácter, `;` `,` | Saltar a un carácter de la línea, repetir hacia adelante / atrás |
| `%` | Paréntesis o corchete pareja |
| `Ctrl+D` / `Ctrl+U` | Media página abajo / arriba |
| `/texto`, `?texto`, `n` / `N`, `*` | Buscar hacia adelante / atrás (sin distinguir mayúsculas), coincidencia siguiente / anterior, la palabra bajo el cursor |
| `i` `a` `I` `A` `o` `O` | Insertar antes / después del cursor, al inicio / final de la línea, en una línea nueva abajo / arriba |
| `x` `X`, `s` `S`, `r` + carácter | Borrar un carácter después / antes, cambiar un carácter / la línea, reemplazar un carácter |
| `d` `c` `y` + movimiento | Borrar / cambiar / copiar (yank): `dw`, `c$`, `y2j`, `dfx` |
| `dd` `cc` `yy`, `D` `C` `Y` | Lo mismo con líneas completas; `D` y `C` hasta el final de la línea |
| `iw` `aw`, `i"` `a"`, `i'` `a'`, `i(` `a(` (`ib`), `i[`, `i{` | Objetos de texto después de un operador: `ciw`, `di(`, `ya'` |
| `p` / `P` | Pegar después / antes (las líneas van abajo / arriba) |
| `J`, `~`, `>>` / `<<` | Unir líneas, alternar mayúsculas, indentar / desindentar |
| `u` / `Ctrl+R` | Deshacer / rehacer; una estadía completa en modo insert es un solo paso |
| `gt` / `gT` | Pestaña de consulta siguiente / anterior |
| `ZZ` / `ZQ` | Igual que `:x` / `:q!` |

El texto copiado también va al portapapeles del sistema, así que puedes pegarlo en otras aplicaciones.

## Modo insert

Escribir funciona como en el editor normal, autocompletado incluido. `Esc` cierra la lista de autocompletado y vuelve
al modo normal.

## Modos visual

`v` selecciona caracteres y `V` líneas completas. Los movimientos y objetos de texto (`viw`, `vi(`) amplían la
selección; `o` pasa a su otro extremo. Después `y`, `d` o `x`, `c`, `>`, `<`, `~` o `p` actúan sobre ella, y `Esc`
sale.

## Comandos

| Comando | Acción |
| --- | --- |
| `:w`, `:w ruta` | Guardar la consulta (pidiendo una ruta la primera vez), o guardarla como `ruta`; `:w! ruta` reemplaza un archivo existente |
| `:q`, `:q!` | Cerrar la pestaña, rechazando cambios sin guardar salvo con `!`; en la última pestaña, salir (tras confirmar) |
| `:wq`, `:x` | Guardar y cerrar (`:x` solo guarda si hay cambios) |
| `:qa` | Salir |
| `:e ruta`, `:tabnew [ruta]` | Abrir un script en una pestaña / abrir una pestaña nueva |
| `:tabn`, `:tabp` | Pestaña siguiente / anterior |
| `:12` | Ir a la línea 12 |
| `:run` | Ejecutar la consulta, como `F5` |
| `:cancel` | Cancelar la consulta en curso, como `Ctrl+C` |
| `:set vim`, `:set novim` | Activar / desactivar el modo vim |

Las rutas pueden empezar con `~` para tu carpeta personal. Un comando desconocido muestra
`E492: Not an editor command`.

No incluido: repetir con `.`, macros, marcas, registros con nombre, modo visual de bloque y divisiones de ventana.
