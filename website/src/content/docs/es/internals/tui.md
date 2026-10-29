---
title: Dentro de la TUI
description: Cómo están construidos sobre Terminal.Gui la vista principal, el despacho de teclas, el editor, el trabajo en segundo plano y el registro de teclas.
---

La TUI está construida sobre [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 2.5 y está en `src/LazyTsql/Tui`.

## Vista principal

`MainView` es un `Runnable` con seis paneles `FrameView`: una columna izquierda del 28 % del ancho (conexiones, archivos, esquema) y,
a la derecha, el editor (38 % del alto), los resultados y un panel de mensajes de siete filas. Los índices de los paneles son constantes con nombre
(`ConnectionsPanel = 0` … `MessagesPanel = 5`), y las teclas numéricas se asignan a ellos.

## Despacho de teclas

Todas las teclas de la vista principal pasan por un único manejador en el teclado de la aplicación, `OnGlobalKeyDown`, en un orden fijo:

1. Si hay un diálogo abierto, solo se maneja la ayuda (`F1`, o `?` cuando no se está escribiendo); el diálogo se encarga de todas las demás teclas.
2. Comandos globales que funcionan en todas partes: `F5`/`Ctrl+E`, `Ctrl+K`, `F1`, `Ctrl+S`, `Ctrl+O`, `Ctrl+Q`.
3. `Esc` en el editor (cuando la ventana emergente de autocompletado está cerrada) sale de él; `Alt+dígito` enfoca un panel.
4. Fuera del editor, los movimientos de vim se traducen a las teclas que las vistas ya tienen asignadas: `j`→`↓`, `k`→`↑`, `h`→`←`,
   `l`→`→`, `g`→`Home`, `G`→`End`, `Ctrl+D`/`Ctrl+U`→`PgDn`/`PgUp`, y se reenvían a la vista enfocada. Así, el comportamiento
   es exactamente el de las flechas, tanto en árboles como en tablas y listas.
5. El editor y el panel de mensajes reciben todo lo demás como texto; los otros paneles reciben dígitos, `q`, `Tab`, `?` y
   sus propias teclas.

## Un único registro de teclas

`KeyHelp` contiene cada tecla una sola vez, agrupada por `KeyContext` (cada panel y cada diálogo). El pie de página, el diálogo de ayuda
y la [referencia de teclas](/lazytsql/es/reference/keys/) se generan a partir de él, así que una tecla nueva se documenta en un solo lugar.
Los diálogos se etiquetan con su contexto, de modo que el pie de página los sigue.

## El editor

`SqlEditor` extiende el `TextView` de Terminal.Gui:

- **Resaltado**: el texto se clasifica por carácter (palabra clave, cadena, comentario, número, variable) y se guarda en caché
  hasta que cambia; al dibujar se elige el color de cada carácter.
- **Autocompletado**: una ventana emergente de autocompletado alimentada por `SqlSuggestionGenerator`, que pasa todo el texto y el cursor a
  `SqlCompletion.Suggest`. Al aceptar se reemplaza solo el prefijo escrito, y la ventana emergente no se vuelve a abrir justo después de
  aceptar.
- **Dibujo**: el `TextView` de Terminal.Gui 2.5 sigue dibujando por debajo de la vista, línea por línea hasta el final
  del texto, lo que hacía que cada tecla y cada guardado en un script de 20 000 líneas tardara segundos. `SqlEditor`
  sobrescribe `OnDrawingContent` para dibujar solo las filas visibles (`EditorLayout` ubica los tabs, los glifos anchos y
  la parte desplazada horizontalmente); ahí una tecla tarda unos 0,4 s y un guardado unos 0,15 s.
- **Pegado**: `TextView` no redibuja después de pegar salvo que reemplace una selección, así que `SqlEditor` pide el
  redibujo; `Ctrl+V` lee el portapapeles del sistema por su cuenta (`wl-paste`, `xclip`, `xsel`, `pbpaste`), porque
  Terminal.Gui solo conoce `xclip`, que no ve el portapapeles de Wayland.
- **Nombres desconocidos**: `Diagnose` se ejecuta cuando cambian el texto o el cursor; los rangos marcados se dibujan subrayados en rojo,
  con prioridad sobre los colores de sintaxis.

## Trabajo en segundo plano

El trabajo con la base de datos se ejecuta en `RunInBackground`: `Task.Run` y luego un `SemaphoreSlim(1, 1)`, para que las operaciones sobre la única
conexión se ejecuten una a la vez y en orden. Los resultados vuelven al hilo de la interfaz con `App.Invoke`. Los errores se registran como
`✗ message`, y las cancelaciones como `Cancelled.`.

La comprobación de actualizaciones se ejecuta en su propia tarea, fuera del semáforo, porque no usa la conexión.

## Tema

`Force16Colors` hace que Terminal.Gui use los 16 colores ANSI de la terminal en lugar de valores RGB fijos, y los paneles conservan
el fondo predeterminado, así que lazytsql sigue el tema de la terminal. `Theme` define los pocos roles que usa: acento
(verde), atenuado (gris oscuro), selección (azul), colores de sintaxis y el subrayado rojo para los nombres desconocidos. Los títulos y las etiquetas
desactivan el marcador de tecla rápida `_` de Terminal.Gui, de modo que nombres como `top_customers.sql` conservan sus guiones bajos.

## Diálogos

`ConfirmDialog` (`y`/`n`, con el botón seguro enfocado por defecto en las acciones destructivas), `ConnectionDialog` (dos
modos: cadena de conexión o campos), `FilePicker` (autocompletado de rutas más una lista filtrada), `NameDialog`,
`RowLimitDialog` y `HelpDialog`. Cada uno se ejecuta de forma modal y devuelve su respuesta.
