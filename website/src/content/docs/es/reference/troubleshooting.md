---
title: Solución de problemas
description: Errores comunes y cómo resolverlos.
---

| Síntoma | Solución |
| --- | --- |
| `lazytsql: command not found` después de `dotnet tool install` | Agrega `~/.dotnet/tools` a tu `PATH` y abre una terminal nueva. |
| `Globalization Invariant Mode is not supported` o errores de ICU en Linux | Instala ICU: `sudo apt install libicu-dev` (Debian/Ubuntu) o `sudo dnf install libicu` (Fedora). |
| `The certificate chain was issued by an authority that is not trusted` | El servidor usa un certificado autofirmado. Para un servidor de desarrollo, agrega `TrustServerCertificate=true` a la cadena de conexión; para cualquier otro caso, instala en su lugar la autoridad de certificación del servidor. |
| `Environment variable 'X' referenced in the connection string is not set` | La cadena de conexión hace referencia a `${X}`; expórtala antes de ejecutar lazytsql. |
| `sqlpackage not found` | `dotnet tool install -g microsoft.sqlpackage`, o define `LAZYTSQL_SQLPACKAGE` con su ruta. |
| La importación falla con `contains one or more user objects` | Importa con un nombre de base de datos nuevo; sqlpackage solo importa en una base de datos vacía. |
| `sqlpackage cannot read a connection string or file path containing a double quote` | El archivo de respuesta de sqlpackage no puede contener `"`; cambia esa contraseña o ruta. |
| macOS dice que el binario "cannot be opened" | Se descargó con un navegador: `xattr -d com.apple.quarantine /usr/local/bin/lazytsql`. |
| El autocompletado no muestra nada para una tabla | Sus nombres se cargan en segundo plano la primera vez; espera un momento, o presiona `r` en el panel de esquema después de cambios en el esquema. |
| Una consulta en la TUI se ejecuta indefinidamente | La TUI no tiene tiempo de espera de comando; presiona `Ctrl+K` para cancelar. |
| `Could not write the activity log` | No se puede escribir en el directorio del registro; corrige sus permisos o define `LAZYTSQL_LOG_DIR`. |

La autenticación integrada (Windows) y la de Microsoft Entra ID aún no se han verificado en el binario nativo.

¿Sigues con problemas? [Abre un issue](https://github.com/carandev/lazytsql/issues) con el comando, el error y la salida
de `lazytsql --version`.
