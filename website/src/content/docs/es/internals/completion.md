---
title: Motor de autocompletado
description: Cómo lazytsql divide SQL en tokens, encuentra el contexto, carga nombres de forma diferida, sugiere joins y marca nombres desconocidos.
---

El motor de autocompletado está en `LazyTsql.Core/Completion` y es puro: recibe las líneas del editor, el cursor y un
`SchemaCatalog`, y devuelve sugerencias o diagnósticos. Nunca se comunica con el servidor por sí mismo.

## Tokens

`SqlLexer` convierte el texto en tokens: palabras (sin corchetes ni comillas y con un indicador `Bracketed`), puntos, comas,
paréntesis de apertura, separadores de sentencias (`;` o una línea `GO`) y todo lo demás. Sabe cuándo el cursor está dentro de una
cadena o de un comentario, donde no se sugiere nada.

El resaltado de sintaxis usa un clasificador aparte, a nivel de carácter (`SqlTokenizer`: palabra clave, cadena, comentario, número,
variable), que sigue los comentarios y las cadenas de varias líneas.

## Encontrar el contexto

`SqlCompletion.Suggest` mira la palabra que se está escribiendo y lo que hay alrededor:

1. **Un calificador**: una cadena de puntos pegada a la palabra (`c.`, `sales.`, `archive.dbo.`).
2. **La palabra anterior**: `FROM`, `JOIN`, `UPDATE`, `INTO`, `APPLY`, `TABLE`, `EXEC`, `ON`…
3. **Las tablas de la sentencia**: `References` recopila cada tabla que aparece después de esas palabras clave en la sentencia actual,
   con su alias (`AS` opcional), y continúa una lista `FROM a, b` después de las comas. Una sentencia termina en `;` o en `GO`.

A partir de eso:

| Contexto | Sugerencias |
| --- | --- |
| Después de `FROM`, `JOIN`, `UPDATE`, `INTO`, `APPLY` | Tablas, vistas, funciones, sinónimos y bases de datos |
| Después de `EXEC` | Procedimientos |
| `x.` | Un alias o una tabla de la sentencia → sus columnas; un esquema → sus objetos; una base de datos → sus esquemas |
| `x.y.` | `schema.table` → columnas, o `database.schema` → objetos |
| En cualquier otro lugar | Columnas de las tablas de la sentencia, sus alias y palabras clave |
| Después de `JOIN` / `ON` | Joins y condiciones a partir de claves foráneas, incluso antes de que escribas una letra |

## Orden

Los candidatos coinciden por prefijo, sin distinguir mayúsculas de minúsculas, y cada texto aparece una sola vez. Una palabra clave escrita completa se descarta (no
tiene sentido sugerir `FROM` después de `FROM`), pero una tabla escrita completa se conserva y se ordena **primero**, para que `Tab` pueda confirmarla.
Luego se ordenan por tipo (join, columna, alias, tabla, vista, función, sinónimo, procedimiento, esquema, base de datos,
palabra clave) y por nombre, hasta 200.

Los nombres insertados se escriben a la medida: un objeto de `dbo` va sin su esquema, salvo que otro esquema tenga un objeto con
el mismo nombre, y un nombre se pone entre corchetes solo cuando no es un identificador regular o es una palabra reservada.

## Joins a partir de claves foráneas

Para cada tabla que ya está en la sentencia, `Links` encuentra las claves foráneas que salen de ella o que apuntan a ella, y empareja las
columnas en el orden de la clave. Una sugerencia es la tabla relacionada, un alias formado por sus iniciales (`order_lines` → `ol`, numerado
si ya está en uso) y la condición con todas las columnas: `shipments s ON s.order_id = l.order_id AND s.line = l.line`. Después de
`ON`, solo se ofrecen las condiciones entre la tabla recién unida y las anteriores.

## Sinónimos

Los sinónimos se leen junto con el objeto al que apuntan (`PARSENAME` de `sys.synonyms.base_object_name`). `Resolve` sigue
el destino una vez, en la misma base de datos o en otra, así que las columnas, las claves foráneas y las comprobaciones de columnas desconocidas de un sinónimo
son las de su tabla. Un sinónimo en un servidor vinculado no tiene un destino legible y solo se autocompleta por nombre.

## Catálogo de esquema diferido

`SchemaCatalog` guarda lo que se ha cargado: la lista de bases de datos, luego, por base de datos, sus objetos y claves foráneas, y,
por tabla, sus columnas. Cuando el motor pide algo que falta, el catálogo devuelve `null` y lanza
`ObjectsNeeded`, `ColumnsNeeded` o `ForeignKeysNeeded`, **una sola vez** por clave. La TUI responde en segundo plano:
lee los nombres con consultas de esquema que se ejecutan en esa base de datos sin cambiar la tuya, los guarda y actualiza la
lista de autocompletado, de modo que las sugerencias aparecen un momento después sin bloquear la escritura.

## Nombres desconocidos

`Diagnose` revisa cada referencia a una tabla y cada `alias.column` del script. Una tabla se marca cuando su base de datos
está cargada y no tiene ningún objeto con ese nombre; una columna, cuando las columnas de su tabla están cargadas y ninguna coincide. Para evitar
falsos positivos, omite:

- el destino de `CREATE`/`ALTER`/`DROP TABLE` y de `SELECT ... INTO` (puede que todavía no exista), pero revisa `INSERT INTO`
  y `MERGE INTO`;
- las tablas temporales, los nombres que define el script (CTE, `CREATE TABLE`, `CREATE VIEW`, `CREATE SYNONYM`), `sys` e
  `INFORMATION_SCHEMA`, y los alias usados como nombres (`UPDATE c ... FROM customers c`);
- los nombres de cuatro partes (servidores vinculados) y las bases de datos que no están en la lista;
- la palabra bajo el cursor y todo lo que todavía no se ha cargado.

El editor dibuja los rangos marcados con un subrayado rojo y los vuelve a calcular cuando cambian el texto o el cursor.
