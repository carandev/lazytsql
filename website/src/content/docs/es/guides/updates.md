---
title: Aviso de actualización
description: Cómo detecta la TUI que hay una versión más reciente, y cómo desactivarlo.
---

Cuando existe una versión más reciente, la TUI lo indica en el panel de mensajes y en su título:

```text
Update available: 0.1.2 → 0.1.5 · dotnet tool update -g lazytsql
```

## Cómo funciona

1. Una vez que la pantalla está lista, una tarea en segundo plano le pide a NuGet.org las versiones publicadas de `lazytsql`
   (`https://api.nuget.org/v3-flatcontainer/lazytsql/index.json`). No se envía nada más.
2. Las versiones preliminares se ignoran. Cuando la versión más reciente es mayor que la tuya, aparece el aviso.
3. La respuesta se guarda en caché en `~/.local/state/lazytsql/latest-version`, así que se consulta a NuGet **como máximo una vez al día**.
4. Sin conexión, una respuesta lenta (tiempo de espera de 5 segundos) o una inesperada no muestran nada.

El aviso sugiere la forma correcta de actualizar:

- instalado como herramienta de .NET (el ejecutable está en `~/.dotnet/tools`): `dotnet tool update -g lazytsql`;
- un binario descargado: la [página de la última release](https://github.com/carandev/lazytsql/releases/latest).

lazytsql nunca descarga ni reemplaza nada por sí mismo.

## Desactivarlo

```bash
export LAZYTSQL_NO_UPDATE_CHECK=1
```

Cualquier valor no vacío desactiva la comprobación.
