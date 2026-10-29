---
title: Compilación y publicación
description: Cómo se compilan y publican los binarios nativos y la herramienta de .NET en cada push a main.
---

## Native AOT

`src/LazyTsql/LazyTsql.csproj` se publica con Native AOT: el código C# y las partes de .NET que usa se compilan
de antemano en un único ejecutable nativo, sin JIT y sin runtime que instalar.

| Configuración | Efecto |
| --- | --- |
| `PublishAot=true` | Compilación nativa; el trimming (recorte) elimina el código que no se usa |
| `OptimizationPreference=Size` | Prioriza un binario más pequeño frente a una velocidad apenas mayor |
| `StripSymbols`, `CopyOutputSymbolsToPublishDirectory=false` | Los símbolos de depuración quedan fuera del binario y del paquete |
| `EventSourceSupport=false` | Elimina la infraestructura de trazas que lazytsql no usa |
| GC de estación de trabajo, no concurrente | Menos memoria para una herramienta de un solo usuario |

El resultado ocupa unos 21 MB por plataforma. AOT solo compila para el sistema operativo en el que se ejecuta, así que cada plataforma se
compila en su propio runner. Microsoft.Data.SqlClient emite advertencias de trimming para funciones que lazytsql no usa (se
muestran como advertencias, no como errores); las pruebas de integración se ejecutan contra SQL Server para detectar una falla real.

Los binarios de Linux necesitan ICU (`libicu`) en tiempo de ejecución para el texto sensible a la cultura, algo que SqlClient requiere.

## La herramienta de .NET

lazytsql también es una [herramienta de .NET](https://learn.microsoft.com/dotnet/core/tools/global-tools) en NuGet.org, que usa los
paquetes de herramienta específicos por RID de .NET 10:

- `PackAsTool` con `ToolPackageRuntimeIdentifiers=linux-x64;linux-arm64;win-x64;osx-arm64;any`.
- Un paquete por plataforma (`lazytsql.linux-x64`, …) contiene el **binario nativo AOT** de esa plataforma.
- Un pequeño paquete puntero, `lazytsql`, es el que instalan los usuarios; la CLI de .NET elige el paquete de la plataforma
  actual.
- `lazytsql.any` es una compilación dependiente del framework (`PublishAot=false`) para cualquier otra plataforma con el runtime de
  .NET 10.

## El workflow de publicación

`.github/workflows/release.yml` se ejecuta en cada push a `main` (y de forma manual). La versión es `0.1.<run number>`.

```text
test ──► build (linux-x64 · linux-arm64 · win-x64 · osx-arm64) ──► publish
```

1. **test** (`ubuntu-24.04`): inicia SQL Server 2022 como contenedor de servicio, instala sqlpackage, compila, espera
   hasta que el servidor responde a `SELECT 1` y ejecuta todas las pruebas, incluidas las de integración y un ciclo real de exportación e
   importación de `.bacpac`.
2. **build** (una matriz, cada uno en su propio sistema operativo, incluido un runner Linux ARM): `dotnet publish -r <rid>` para el
   binario, `dotnet pack -r <rid>` para el paquete de herramienta de la plataforma, y un `lazytsql-<rid>.tar.gz` (`.zip` en Windows).
3. **publish** (`ubuntu-24.04`, el único trabajo con permiso de escritura):
   - empaqueta el paquete puntero y el respaldo `any`;
   - hace push a NuGet.org con `--skip-duplicate`: primero los paquetes de plataforma y al final el puntero, porque instalar el
     puntero requiere que exista cada paquete de plataforma;
   - crea la release de GitHub `v<version>` con los archivos comprimidos y notas generadas, o los sube a ella si ya existe.

Una publicación fallida se puede volver a ejecutar sin riesgo: NuGet omite los paquetes que ya tiene y la release se actualiza, no se
duplica.

El sitio web se compila y se despliega en GitHub Pages mediante un workflow aparte, solo cuando cambia `website/`.
