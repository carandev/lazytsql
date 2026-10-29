---
title: Archivos y almacenamiento
description: Cómo guarda lazytsql las conexiones, los scripts, el registro de actividad y la caché de actualizaciones.
---

## Conexiones

`~/.config/lazytsql/connections.json` (o `$XDG_CONFIG_HOME/lazytsql/`) es un arreglo JSON:

```json
[
  {
    "name": "dev",
    "connectionString": "Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD}"
  }
]
```

- Se escribe de forma atómica: se crea un archivo `.tmp` con permisos solo para el propietario (0600), se escribe y luego se mueve sobre el
  original, de modo que una caída nunca deja un archivo escrito a medias.
- Los perfiles se mantienen ordenados por nombre; los nombres son únicos, sin distinguir mayúsculas de minúsculas.
- Se serializa con un contexto de `System.Text.Json` generado en código fuente, algo que Native AOT necesita.
- `${VAR}` se expande al conectar, nunca al guardar.

## Carpetas

`folders.json`, junto a `connections.json`, es un arreglo JSON con las rutas absolutas que muestra el panel de
archivos, en el orden en que se agregaron. Las rutas se normalizan (absolutas, sin separador final) para que una
carpeta nunca aparezca dos veces, y el archivo se reemplaza de forma atómica como el de conexiones.

## Scripts

`ScriptFile` lee un script detectando su codificación a partir de la marca de orden de bytes (UTF-8 sin BOM cuando no hay ninguna)
y recuerda si usaba CRLF o LF. El editor trabaja con `\n`; al guardar se vuelve a los finales de línea originales
y se escribe con la codificación original, BOM incluida. Los scripts de SSMS, a menudo en UTF-16 o UTF-8 con BOM y
CRLF, se conservan byte a byte al abrirlos y guardarlos. Una ruta que no existe se abre como un archivo nuevo y vacío.

## Registro de actividad

`ActivityLog` agrega entradas a `<state>/logs/yyyy-MM-dd.log`:

- `<state>` es `$XDG_STATE_HOME/lazytsql` o `~/.local/state/lazytsql` (`%LOCALAPPDATA%\lazytsql` en Windows);
  `LAZYTSQL_LOG_DIR` reemplaza el directorio de registros.
- El directorio se crea con 0700 y los archivos con 0600; las escrituras se serializan con un bloqueo y se abren en modo
  de anexado, así que la TUI y un comando de la CLI pueden registrar al mismo tiempo.
- Una entrada es `yyyy-MM-dd HH:mm:ss.fff zzz [source] text`; las líneas siguientes de una entrada de varias líneas llevan una sangría de
  cuatro espacios.
- `Write` devuelve `false` en lugar de lanzar una excepción; la TUI informa el primer fallo una sola vez.
- `Prune` elimina los archivos diarios de más de 30 días en cada inicio.

## Caché de actualizaciones

`<state>/latest-version` contiene una línea: cuándo se consultó NuGet.org por última vez (ISO 8601, UTC) y la última versión que
devolvió. Una línea de menos de un día responde sin usar la red.

## Archivos temporales

- **Archivo de respuesta de sqlpackage**: se crea en el directorio temporal con `CreateNew` y 0600, y contiene la cadena de conexión;
  siempre se elimina cuando sqlpackage termina, incluso si falla o se cancela.
- **Exportación parcial**: `name.partial-<guid>.bacpac` junto al destino, que se mueve sobre él solo si tiene éxito y se elimina
  en caso contrario.
- **CSV parcial**: se elimina cuando una exportación falla o se cancela.
