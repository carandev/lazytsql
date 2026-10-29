---
title: Teclas
description: Todas las teclas de la TUI, por panel y diálogo. La misma lista que lazytsql muestra con ? y en su pie de página.
---

Esta lista refleja el registro que lazytsql usa para su pie de página y su diálogo de ayuda, así que es exactamente lo que
la aplicación muestra con `?` (o `F1`).

## Globales

| Tecla | Acción |
| --- | --- |
| `1`-`6` / `Alt+1`-`Alt+6` | Enfocar un panel (`Alt+` también funciona dentro del editor) |
| `Tab` | Siguiente panel |
| `h` `j` `k` `l` | Moverse (fuera del editor) |
| `g` / `G` | Inicio / final |
| `Alt+↓` / `Alt+↑` | Hacer más alto / más bajo el panel enfocado (`+` / `-` fuera del editor) |
| `Alt+→` / `Alt+←` | Ensanchar / angostar la columna izquierda |
| `Ctrl+D` / `Ctrl+U` | Avanzar / retroceder una página |
| `F5` / `Ctrl+E` | Ejecutar la selección, o todo el script lote por lote |
| `Ctrl+K` | Cancelar la consulta, exportación o importación en curso |
| `Ctrl+O` / `Ctrl+S` | Abrir / guardar un archivo `.sql` |
| `Ctrl+W` | Cerrar el archivo `.sql` abierto (pregunta primero si tiene cambios sin guardar) |
| `?` / `F1` | Ayuda del panel enfocado (`F1` dentro del editor) |
| `q` / `Esc` / `Ctrl+Q` | Salir tras confirmar (`q` y `Esc` solo fuera del editor) |

## Paneles

| Panel | Tecla | Acción |
| --- | --- | --- |
| Conexiones `[1]` | `Enter` | Conectar |
| | `a` / `e` / `d` | Agregar / editar / eliminar una conexión |
| Archivos `[2]` | `Enter` | Abrir el script, o expandir/contraer una carpeta |
| | `h` / `l` | Contraer / expandir |
| | `o` | Agregar una carpeta (se recuerda entre sesiones) |
| | `d` | Quitar del panel la carpeta del primer nivel seleccionada |
| | `/` | Buscar carpetas y scripts en cualquier nivel |
| | `n` / `N` | Coincidencia siguiente / anterior |
| | `r` | Actualizar |
| Búsqueda de archivos | *texto* | Busca mientras escribes; se selecciona la primera coincidencia |
| | `Enter` / `Esc` | Quedarse en la coincidencia / volver a donde estabas |
| | `Backspace` | Borrar un carácter (con la búsqueda vacía, cancela) |
| Esquema `[3]` | `Enter`, `l` / `h` | Expandir / contraer |
| | `s` | `SELECT TOP (100)` de la tabla o vista seleccionada |
| | `e` | Abrir la definición del procedimiento o función seleccionado |
| | `r` | Recargar el esquema y los nombres del autocompletado |
| | `x` / `i` | Exportar la base de datos seleccionada a `.bacpac` / importar un `.bacpac` |
| Consulta `[4]` | `F5` | Ejecutar la selección o todo |
| | `Ctrl+K` | Cancelar |
| | `Ctrl+S` | Guardar |
| | `Ctrl+V` | Pegar el portapapeles del sistema |
| | `Tab` / `Ctrl+Space` | Aceptar / abrir el autocompletado (`Esc` lo cierra) |
| | `Esc` | Salir del editor |
| Resultados `[5]` | `e` / `Enter` | Editar la celda seleccionada |
| | `s` | Guardar las ediciones (muestra el script antes) |
| | `u` / `U` | Deshacer la celda seleccionada / todas las ediciones |
| | `[` / `]` | Conjunto de resultados anterior / siguiente |
| | `x` | Exportar a CSV |
| | `m` | Límite de filas |
| | `h` `j` `k` `l` | Moverse entre celdas |
| Mensajes `[6]` | `j` / `k` | Desplazarse |

## Diálogos

| Diálogo | Tecla | Acción |
| --- | --- | --- |
| Selector de archivos | `Tab` | Completar la ruta |
| | `↓` | Entrar en la lista |
| | `Enter` / `l` | Abrir una carpeta o elegir un archivo |
| | `h` / `Backspace` | Subir una carpeta |
| | `Esc` | Cancelar |
| Confirmación | `y` / `n` | Presionar el botón marcado con `(y)` / `(n)` |
| | `←` `→` (o `h` `l`) | Elegir un botón; el elegido se resalta |
| | `Enter` | Presionar el botón elegido |
| | `Esc` | Cancelar |
| Formulario de conexión | `Tab` | Siguiente campo |
| | `←` `→` | Elegir una opción |
| | `Enter` / `Esc` | Guardar / cancelar |
| Editar celda | `Enter` | Usar el valor escrito |
| | `Ctrl+N` | Dejar en `NULL` |
| | `Esc` | Cancelar |
| Límite de filas | `Enter` / `Esc` | Aplicar / cancelar |
| Nombre de la base de datos | `Enter` / `Esc` | Importar / cancelar |
| Ayuda | `a` | Alternar entre las teclas de este panel y todas las teclas |
| | `j` / `k` | Desplazarse |
| | `Esc` | Cerrar |
