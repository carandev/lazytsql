---
title: Registro de actividad
description: Un registro diario privado de cada script que ejecutó lazytsql, sus mensajes, conexiones, exportaciones e importaciones.
---

lazytsql guarda un historial de lo que hizo, para que puedas encontrar la consulta que ejecutaste el martes pasado o ver exactamente qué falló.

## Dónde

Un archivo por día:

| Sistema operativo | Directorio |
| --- | --- |
| Linux, macOS | `~/.local/state/lazytsql/logs/` (o `$XDG_STATE_HOME/lazytsql/logs/`) |
| Windows | `%LOCALAPPDATA%\lazytsql\logs\` |

Define `LAZYTSQL_LOG_DIR` para escribir en otro lugar.

```bash
cat ~/.local/state/lazytsql/logs/2026-10-28.log
grep -A3 "Running on prod" ~/.local/state/lazytsql/logs/*.log
```

## Qué contiene

```text
2026-10-28 23:01:11.298 -05:00 [tui] Connecting to dev…
2026-10-28 23:01:11.683 -05:00 [tui] Connected to dev (SQL Server 16.00.4305), database shop.
2026-10-28 23:01:17.287 -05:00 [tui] Running on dev › shop:
    SELECT name FROM sys.databases
2026-10-28 23:01:17.380 -05:00 [tui] Done in 32 ms.
2026-10-28 23:01:10.807 -05:00 [cli] ✗ Login failed for user 'sa'.
```

- **Desde la TUI** (`[tui]`): cada mensaje del panel de mensajes, y el SQL de cada ejecución con la conexión y la
  base de datos en que se ejecutó.
- **Desde la línea de comandos** (`[cli]`): cada `query` con su SQL, mensajes y código de salida; exportaciones e importaciones;
  conexiones agregadas o eliminadas; y los errores que detuvieron un comando.

Cada entrada empieza con la hora local y su desfase; las líneas de una entrada de varias líneas van con sangría.

## Privacidad

- Los archivos y el directorio son legibles solo por ti (0600 y 0700).
- **Las cadenas de conexión nunca se escriben.** Las entradas nombran la conexión guardada o el servidor, porque una cadena
  de conexión puede contener una contraseña.
- El SQL se escribe tal como se ejecutó. Evita secretos literales en los scripts (`CREATE LOGIN ... WITH PASSWORD = '...'`), o apunta
  `LAZYTSQL_LOG_DIR` a un lugar que limpies.
- Los registros con más de **30 días** se eliminan cuando lazytsql inicia.

Si el registro no se puede escribir (un disco lleno, falta de permisos), la TUI lo indica una vez en el panel de mensajes y
continúa; el registro nunca detiene tu trabajo.
