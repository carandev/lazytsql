---
title: Copias de seguridad con .bacpac
description: Exporta una base de datos completa a un archivo .bacpac e impórtala como una base de datos nueva, desde la TUI o la línea de comandos.
---

Un `.bacpac` contiene una base de datos completa, esquema y datos, en un archivo portable que tanto Azure SQL como SQL Server pueden leer.
lazytsql ejecuta [sqlpackage](https://learn.microsoft.com/sql/tools/sqlpackage/sqlpackage) de Microsoft para esto, así que instálalo
primero:

```bash
dotnet tool install -g microsoft.sqlpackage
```

## Desde la línea de comandos

```bash
lazytsql export -c dev -d app app.bacpac         # the database to a file
lazytsql import -c dev -d app_copy app.bacpac    # the file into a new, empty database
```

- `-d` toma por defecto la base de datos de la cadena de conexión.
- Un nombre de archivo sin extensión recibe `.bacpac`.
- El progreso de sqlpackage va a stderr; la línea final (`Exported app to /path/app.bacpac`) va a stdout.
- `--json` y `--csv` no están soportados aquí.

## Desde la TUI

En el panel de esquema (`3`), selecciona una base de datos y pulsa `x` para exportarla, o pulsa `i` para importar un archivo. La importación
pide el nombre de la nueva base de datos y sugiere el nombre del archivo. El progreso se muestra en el panel de mensajes y `Ctrl+K` cancela.

## Qué esperar

- **Las importaciones necesitan una base de datos nueva o vacía.** sqlpackage se niega a importar en una base de datos que tenga objetos.
- **Una exportación fallida o cancelada conserva el archivo anterior.** La exportación se escribe junto al destino como
  `name.partial-<id>.bacpac` y reemplaza el destino solo cuando está completa.
- **Una importación cancelada puede dejar una base de datos importada parcialmente**, que puedes eliminar.
- **La cadena de conexión nunca aparece en una línea de comandos.** Llega a sqlpackage a través de un archivo temporal que solo
  tú puedes leer y que se elimina después. sqlpackage no puede leer una comilla doble (`"`) en ese archivo, así que una contraseña
  que contenga una se rechaza antes de que sqlpackage arranque.

## Cómo se encuentra sqlpackage

lazytsql lo busca en este orden:

1. `$LAZYTSQL_SQLPACKAGE`, una ruta al ejecutable;
2. el `PATH`;
3. `~/.dotnet/tools`, donde lo coloca `dotnet tool install -g`.
