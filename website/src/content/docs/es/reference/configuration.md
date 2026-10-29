---
title: Configuración
description: Todos los archivos y variables de entorno que lazytsql lee o escribe.
---

lazytsql no tiene archivo de ajustes: se configura con variables de entorno y guarda dos tipos de archivos.

## Archivos

| Ruta | Contiene | Permisos (Linux, macOS) |
| --- | --- | --- |
| `$XDG_CONFIG_HOME/lazytsql/connections.json` (por defecto `~/.config/lazytsql/connections.json`) | Conexiones guardadas | Solo el propietario (0600) |
| `$XDG_STATE_HOME/lazytsql/logs/<date>.log` (por defecto `~/.local/state/lazytsql/logs/`) | [Registro de actividad](/lazytsql/es/guides/activity-log/), conservado 30 días | Solo el propietario (0600, directorio 0700) |
| `$XDG_STATE_HOME/lazytsql/latest-version` | La versión más reciente vista en NuGet.org y cuándo | |

En Windows, los archivos de estado están en `%LOCALAPPDATA%\lazytsql\` y las conexiones en
`%USERPROFILE%\.config\lazytsql\connections.json`.

## Variables de entorno

| Variable | Propósito |
| --- | --- |
| `LAZYTSQL_CONNECTION` | Conexión que se usa cuando no se indica `-c`: un nombre guardado o una cadena de conexión |
| `${VAR}` en una cadena de conexión | Se reemplaza por esa variable al conectar, para que los secretos no queden en `connections.json` |
| `LAZYTSQL_SQLPACKAGE` | Ruta a sqlpackage cuando no está en el `PATH` ni en `~/.dotnet/tools` |
| `LAZYTSQL_LOG_DIR` | Escribe el registro de actividad en este directorio |
| `LAZYTSQL_NO_UPDATE_CHECK` | Cualquier valor no vacío evita que la TUI consulte NuGet.org en busca de una versión más reciente |
| `XDG_CONFIG_HOME`, `XDG_STATE_HOME` | Directorios base estándar para los archivos anteriores |

## Valores por defecto que conviene conocer

| Ajuste | TUI | Línea de comandos |
| --- | --- | --- |
| Filas por conjunto de resultados | 1000, cámbialo con `m` (`all` para todas las filas) | 1000, cámbialo con `--max-rows` (al menos 1) |
| Tiempo de espera del comando | Ninguno; cancela con `Ctrl+K` | 30 segundos, cámbialo con `--timeout` |
| Codificación CSV | UTF-8 con BOM (para Excel) | UTF-8 sin BOM (para tuberías) |
