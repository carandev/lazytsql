---
title: Conexiones
description: Guarda conexiones desde la TUI o la línea de comandos, mantén las contraseñas fuera del archivo y elige qué bases de datos aparecen.
---

## Guardar conexiones

Desde la línea de comandos:

```bash
lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
lazytsql connections list
lazytsql connections remove dev
```

Desde la TUI, en el panel de conexiones (`1`): `a` agrega, `e` edita, `d` elimina (tras confirmar) y `Enter`
conecta.

Las conexiones se guardan en `~/.config/lazytsql/connections.json` (o `$XDG_CONFIG_HOME/lazytsql/connections.json`),
legible solo por ti. Consulta [almacenamiento](/lazytsql/es/internals/storage/) para ver el formato.

## El formulario de conexión

El diálogo de agregar/editar tiene dos modos; cambia entre ellos con `←`/`→` y `Space` sobre *Mode*:

- **Connection string**: cualquier cadena de conexión de SqlClient.
- **Fields**: servidor, puerto, base de datos, autenticación (inicio de sesión de SQL o integrada), usuario, contraseña, *Encrypt* y
  *Trust server certificate*.

Una cadena de conexión con opciones que el formulario no puede mostrar se abre en modo de cadena de conexión, así que editarla nunca
las descarta. El campo de contraseña está enmascarado.

## Mantener las contraseñas fuera del archivo

Escribe `${VAR}` en cualquier parte de una cadena de conexión y lazytsql lo reemplaza por esa variable de entorno al
conectarse:

```bash
export DEV_DB_PASSWORD='s3cret'
lazytsql connections add dev 'Server=db;User Id=app;Password=${DEV_DB_PASSWORD}'
```

El archivo conserva `${DEV_DB_PASSWORD}`; una variable no definida produce un error al conectarse.

## Elegir una conexión

`-c` acepta un nombre guardado (sin distinguir mayúsculas y minúsculas) o una cadena de conexión directa. Sin `-c`, se usa `LAZYTSQL_CONNECTION`:

```bash
lazytsql -c dev
lazytsql -c 'Server=localhost;Database=app;Integrated Security=true'
LAZYTSQL_CONNECTION=dev lazytsql query -q "SELECT 1"
```

Los mensajes y el registro de actividad nombran la conexión por su nombre guardado o por su servidor, nunca por la cadena de conexión,
porque puede contener una contraseña.

## Qué bases de datos aparecen

Cuando la conexión indica una base de datos (`Database=` o `Initial Catalog=`), el panel de esquema muestra solo esa
base de datos. De lo contrario, lista todas las bases de datos en línea a las que tienes acceso.

## Cifrado

*Encrypt* está activado de forma predeterminada. *Trust server certificate* acepta un certificado de servidor que ninguna autoridad de confianza
firmó, algo habitual en los servidores de desarrollo; déjalo desactivado para servidores a los que llegas por redes que no
controlas. Consulta [seguridad](/lazytsql/es/internals/security/).

La autenticación integrada (Windows) y la de Microsoft Entra ID todavía no están verificadas en el binario nativo.
