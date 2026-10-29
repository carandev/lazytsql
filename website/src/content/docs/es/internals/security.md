---
title: Seguridad
description: Cómo maneja lazytsql las contraseñas, los archivos, el cifrado, los registros y las versiones, y lo que todavía no protege.
---

lazytsql se conecta a bases de datos que a menudo contienen datos sensibles, así que esta página explica con claridad qué protege y
qué no.

## Contraseñas y cadenas de conexión

- **Fuera de los archivos cuando lo prefieras**: `${VAR}` en una cadena de conexión se reemplaza por una variable de entorno al
  conectar. El archivo guarda la referencia, nunca el valor.
- **Archivo solo para el propietario**: `connections.json` se escribe con permisos 0600 en Linux y macOS.
- **Nunca en una línea de comandos**: sqlpackage recibe la cadena de conexión mediante un archivo de respuesta temporal (0600,
  creado de forma exclusiva y eliminado después), así que no aparece en `ps` ni en el historial de la shell. Los valores se entrecomillan para que
  una contraseña no pueda inyectar opciones de sqlpackage; un `"`, que el formato del archivo de respuesta no puede contener, se rechaza.
- **Nunca en registros ni mensajes**: el registro de actividad y el panel de mensajes nombran la conexión guardada o el servidor,
  obtenidos sin imprimir la cadena de conexión.
- **Entrada enmascarada**: el campo de contraseña del formulario de conexión oculta lo que escribes.

## Cifrado en tránsito

Las conexiones usan Microsoft.Data.SqlClient, el controlador oficial. En el formulario de conexión, **Encrypt** está activado por defecto
(TLS obligatorio). **Trust server certificate** omite la validación del certificado, que es lo que necesitan los servidores de desarrollo con
certificados autofirmados, pero también permite que cualquiera entre tú y el servidor se haga pasar por él. Desactívalo para
servidores a los que llegas por redes que no controlas, e instala en su lugar la autoridad de certificación del servidor.

## Archivos locales

| Archivo | Permisos |
| --- | --- |
| Conexiones | 0600 |
| Registro de actividad | 0600, directorio 0700 |
| Archivo de respuesta de sqlpackage | 0600, se elimina después de usarlo |

El registro de actividad contiene el SQL que ejecutaste **tal como lo escribiste**, lo que puede incluir datos o secretos escritos en un script; se
elimina después de 30 días.

## Red

Además de la base de datos, la única solicitud de red es la [comprobación de actualizaciones](/lazytsql/es/guides/updates/): un `GET` anónimo
a NuGet.org como máximo una vez al día, por HTTPS, que no envía nada sobre ti ni sobre tus bases de datos. Nunca descarga ni
ejecuta código. `LAZYTSQL_NO_UPDATE_CHECK=1` la desactiva.

## Dependencias y versiones

- Tres dependencias en tiempo de ejecución: Microsoft.Data.SqlClient, Terminal.Gui y, a través de SqlClient, los paquetes de
  criptografía de Microsoft. `dotnet list package --vulnerable` no informa ninguna vulnerabilidad.
- Las versiones las compila GitHub Actions desde el repositorio público, en el runner propio de cada plataforma. El workflow se ejecuta
  con permisos de solo lectura, excepto el trabajo final de publicación; la clave de API de NuGet es un secreto del repositorio.

## Limitaciones conocidas

Están registradas para mejorarse:

| Limitación | Impacto | Mitigación actual |
| --- | --- | --- |
| *Trust server certificate* viene marcado en el formulario | Una conexión nueva por una red no confiable podría ser interceptada | Desmárcalo, o usa una cadena de conexión sin `TrustServerCertificate=true` |
| Las contraseñas escritas directamente en una conexión se guardan en texto plano | Cualquiera con acceso a tu cuenta de usuario puede leerlas | Usa `${VAR}`; el archivo tiene permisos 0600 |
| Los binarios de las versiones no tienen sumas de comprobación, firmas ni atestaciones de procedencia publicadas | Una descarga alterada no se detectaría | Instala con `dotnet tool` (NuGet.org firma cada paquete que sirve) o compila desde el código fuente |
| No hay confirmación antes de las sentencias destructivas | Un `DELETE` sin `WHERE` se ejecuta de inmediato | Revisa antes de `F5`; usa un inicio de sesión con solo los permisos que necesitas |
| El SQL se registra tal como se escribió | Los secretos escritos en scripts terminan en el registro | Evita los secretos literales; configura `LAZYTSQL_LOG_DIR` en una ubicación que administres |

## Cómo informar una vulnerabilidad

Por favor, no abras un issue público para un problema de seguridad. En su lugar, usa el
[informe privado de vulnerabilidades de GitHub](https://github.com/carandev/lazytsql/security/advisories/new).
