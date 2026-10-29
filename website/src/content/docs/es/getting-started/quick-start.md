---
title: Inicio rápido
description: Guarda una conexión, abre la TUI, ejecuta una consulta y ubícate en cinco minutos.
---

## 1. Guarda una conexión

Guarda la contraseña en una variable de entorno para que nunca quede en el archivo:

```bash
export DEV_DB_PASSWORD='your-password'
lazytsql connections add dev 'Server=localhost,1433;Database=app;User Id=sa;Password=${DEV_DB_PASSWORD};TrustServerCertificate=true'
```

`${DEV_DB_PASSWORD}` se reemplaza cuando lazytsql se conecta, no cuando guarda. También puedes agregar conexiones
desde la TUI: ejecuta `lazytsql`, presiona `a` en el panel de conexiones y llena el formulario.

## 2. Abre la TUI

```bash
lazytsql -c dev
```

La pantalla tiene seis paneles numerados. Presiona un número para ir a uno:

| Tecla | Panel | Qué muestra |
| --- | --- | --- |
| `1` | Connections | Las conexiones guardadas; `Enter` conecta |
| `2` | Files | Los scripts `.sql` de una carpeta |
| `3` | Schema | Bases de datos, tablas, vistas, procedimientos y funciones |
| `4` | Query | El editor de SQL |
| `5` | Results | Las filas de la última ejecución, un conjunto de resultados a la vez |
| `6` | Messages | Mensajes del servidor, errores y lo que hizo lazytsql |

## 3. Ejecuta una consulta

Presiona `4`, escribe una consulta y presiona `F5`:

```sql
SELECT TOP (10) name, create_date FROM sys.objects ORDER BY create_date DESC;
```

Mientras escribes, el editor autocompleta nombres de tablas y columnas desde la base de datos. Las filas aparecen
en el panel `5` y los mensajes en el `6`. `F5` ejecuta la selección si hay una; si no, todo el script, lote por lote
(`GO` separa los lotes, como en SSMS).

## 4. Explora

- En el panel de esquema (`3`), `Enter` expande un nodo, `s` ejecuta `SELECT TOP (100)` sobre una tabla y `e` abre
  la definición de un procedimiento.
- En el panel de resultados (`5`), `[` y `]` cambian de conjunto de resultados y `x` exporta el actual a CSV.
- `Esc` sale del editor; fuera de él, `h` `j` `k` `l` se mueven como en vim.
- `?` muestra las teclas del panel en el que estás. `q` sale.

## 5. Úsalo desde un script

La misma conexión funciona sin la TUI:

```bash
lazytsql query -c dev -q "SELECT TOP 5 name FROM sys.tables" --json
```

Consulta el [modo headless](/lazytsql/es/guides/headless/).

## Siguientes pasos

- [La TUI en detalle](/lazytsql/es/guides/tui/)
- [Escribir consultas y autocompletado](/lazytsql/es/guides/writing-queries/)
- [Todas las teclas](/lazytsql/es/reference/keys/)
