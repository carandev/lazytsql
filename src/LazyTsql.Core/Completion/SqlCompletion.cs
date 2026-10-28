using LazyTsql.Core.Schema;
using LazyTsql.Core.Syntax;

namespace LazyTsql.Core.Completion;

public enum CompletionKind
{
    Join,
    Column,
    Alias,
    Table,
    View,
    Function,
    Procedure,
    Schema,
    Database,
    Keyword,
}

/// <summary><paramref name="Text"/> is inserted; <paramref name="Detail"/> is shown next to it (a column type).</summary>
public sealed record CompletionItem(string Text, CompletionKind Kind, string? Detail = null);

/// <summary>A name the catalog does not have, on <paramref name="Row"/> from Start to End (exclusive).</summary>
public sealed record Diagnostic(int Row, int Start, int End, string Message);

/// <summary>How many characters before the cursor the chosen item replaces, and the candidates, best first.</summary>
public sealed record CompletionResult(int Remove, IReadOnlyList<CompletionItem> Items)
{
    public static CompletionResult None { get; } = new(0, []);
}

/// <summary>
/// T-SQL completion from the text around the cursor. It does not parse: incomplete SQL is the normal case while
/// typing, so it looks at the words before the cursor (skipping strings and comments) and at the current statement.
/// </summary>
public static class SqlCompletion
{
    private const int MaxItems = 200;

    private static readonly HashSet<string> ObjectKeywords = new(StringComparer.OrdinalIgnoreCase) { "FROM", "JOIN", "UPDATE", "INTO", "APPLY", "TABLE" };
    private static readonly HashSet<string> RoutineKeywords = new(StringComparer.OrdinalIgnoreCase) { "EXEC", "EXECUTE" };

    public static CompletionResult Suggest(IReadOnlyList<string> lines, int row, int column, SchemaCatalog catalog, string database)
    {
        if (row < 0 || row >= lines.Count) return CompletionResult.None;
        var kinds = SqlTokenizer.Classify(lines);
        column = Math.Clamp(column, 0, lines[row].Length);
        if (column > 0 && kinds[row][column - 1] is not (SqlTokenKind.Plain or SqlTokenKind.Keyword)) return CompletionResult.None;

        var tokens = SqlLexer.Tokenize(lines, kinds);
        var (prefixIndex, prefix, remove) = PrefixAt(tokens, row, column);
        var insertAt = prefixIndex ?? tokens.FindIndex(t => t.Row > row || (t.Row == row && t.Start >= column)) switch { -1 => tokens.Count, var i => i };

        // A qualifier counts only when its dots touch the word being typed: "dbo.cu", not "dbo. cu".
        var qualifier = new List<string>();
        var k = insertAt - 1;
        var (touchRow, touchColumn) = prefixIndex is { } p ? (tokens[p].Row, tokens[p].Start) : (row, column);
        while (k >= 1 && tokens[k].Type == TokenType.Dot && tokens[k - 1].Type == TokenType.Word
               && tokens[k].Row == touchRow && tokens[k].End == touchColumn && tokens[k - 1].End == tokens[k].Start)
        {
            qualifier.Insert(0, tokens[k - 1].Text);
            (touchRow, touchColumn) = (tokens[k - 1].Row, tokens[k - 1].Start);
            k -= 2;
        }

        var context = k >= 0 && tokens[k].Type == TokenType.Word ? tokens[k].Text : null;
        // Right after JOIN or ON the related tables and their conditions are suggested before anything is typed.
        var joining = qualifier.Count == 0 && context is not null && (Same(context, "JOIN") || Same(context, "ON"));
        if (prefix.Length == 0 && qualifier.Count == 0 && !joining) return CompletionResult.None;

        var candidates = new Candidates(prefix);
        var references = References(tokens, insertAt);
        if (joining)
        {
            var before = references.Where(r => r.Position < insertAt).ToList();
            if (Same(context!, "JOIN")) AddJoins(candidates, before, catalog, database);
            else AddConditions(candidates, before, catalog, database);
            if (prefix.Length == 0) return new CompletionResult(remove, candidates.Ordered());
        }

        if (qualifier.Count == 0) SuggestUnqualified(candidates, context, references, catalog, database);
        else SuggestQualified(candidates, qualifier, context, references, catalog, database);
        return new CompletionResult(remove, candidates.Ordered());
    }

    private static void SuggestUnqualified(Candidates candidates, string? context, List<Reference> references, SchemaCatalog catalog, string database)
    {
        if (context is not null && ObjectKeywords.Contains(context))
        {
            var objects = catalog.Objects(database) ?? [];
            foreach (var item in objects)
            {
                if (item.Kind != DbObjectKind.Procedure) candidates.Add(new(Qualified(item, objects), KindOf(item)), item.Name, $"{item.Schema}.{item.Name}");
            }

            foreach (var name in catalog.Databases) candidates.Add(new(Quote(name), CompletionKind.Database), name);
        }
        else if (context is not null && RoutineKeywords.Contains(context))
        {
            var objects = catalog.Objects(database) ?? [];
            foreach (var item in objects)
            {
                if (item.Kind == DbObjectKind.Procedure) candidates.Add(new(Qualified(item, objects), CompletionKind.Procedure), item.Name, $"{item.Schema}.{item.Name}");
            }
        }
        else
        {
            foreach (var reference in references)
            {
                AddColumns(candidates, Resolve(reference.Parts, catalog, database), catalog);
                if (reference.Alias is { } alias) candidates.Add(new(Quote(alias), CompletionKind.Alias, reference.Name), alias);
            }

            var lower = candidates.Prefix.All(c => !char.IsLetter(c) || char.IsLower(c));
            foreach (var keyword in SqlTokenizer.KeywordNames)
            {
                candidates.Add(new(lower ? keyword.ToLowerInvariant() : keyword, CompletionKind.Keyword), keyword);
            }
        }
    }

    private static void SuggestQualified(
        Candidates candidates,
        List<string> qualifier,
        string? context,
        List<Reference> references,
        SchemaCatalog catalog,
        string database)
    {
        if (qualifier.Count == 1)
        {
            // An alias, or a table of the statement, wins over a schema or table of the same name. A name being typed
            // after FROM (FROM sales.) is not a table yet, so an unaliased name counts only when it resolves.
            var aliased = references.FirstOrDefault(r => r.Alias is { } alias && Same(alias, qualifier[0]));
            var target = aliased is not null
                ? Resolve(aliased.Parts, catalog, database)
                : references.Where(r => r.Alias is null && Same(r.Name, qualifier[0])).Select(r => Resolve(r.Parts, catalog, database)).FirstOrDefault(t => t is not null);
            if (aliased is not null || target is not null)
            {
                AddColumns(candidates, target, catalog);
                return;
            }
        }

        var routines = context is not null && RoutineKeywords.Contains(context);
        switch (qualifier.Count)
        {
            // schema. → its objects; database. → its schemas; table. → its columns.
            case 1 when catalog.Objects(database) is { } objects && objects.Any(item => Same(item.Schema, qualifier[0])):
                AddObjects(candidates, objects, qualifier[0], routines);
                break;
            case 1 when catalog.Databases.FirstOrDefault(name => Same(name, qualifier[0])) is { } other:
                foreach (var schema in (catalog.Objects(other) ?? []).Select(item => item.Schema).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(new(Quote(schema), CompletionKind.Schema), schema);
                }

                break;
            case 1:
                AddColumns(candidates, Resolve(qualifier, catalog, database), catalog);
                break;

            // schema.table. → its columns; database.schema. → that schema's objects.
            case 2 when Resolve(qualifier, catalog, database) is { } table:
                AddColumns(candidates, table, catalog);
                break;
            case 2 when catalog.Databases.FirstOrDefault(name => Same(name, qualifier[0])) is { } other:
                AddObjects(candidates, catalog.Objects(other) ?? [], qualifier[1], routines);
                break;

            case 3:
                AddColumns(candidates, Resolve(qualifier, catalog, database), catalog);
                break;
        }
    }

    /// <summary>Tables related by a foreign key to a table already in the statement: "orders o ON o.customer_id = c.id".</summary>
    private static void AddJoins(Candidates candidates, List<Reference> before, SchemaCatalog catalog, string database)
    {
        var used = before.Select(r => r.Alias ?? r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in before)
        {
            if (Resolve(reference.Parts, catalog, database) is not var (owner, table)) continue;
            var objects = catalog.Objects(owner) ?? [];
            foreach (var (schema, name, pairs) in Links(table, catalog.ForeignKeys(owner) ?? []))
            {
                var alias = Alias(name, used);
                var target = objects.FirstOrDefault(item => Same(item.Schema, schema) && Same(item.Name, name)) ?? new DbObject(schema, name, DbObjectKind.Table);
                var display = Same(owner, database) ? Qualified(target, objects) : $"{Quote(owner)}.{Quote(schema)}.{Quote(name)}";
                var condition = Condition(alias, pairs.Select(p => (p.Theirs, p.Mine)), Quote(reference.Alias ?? reference.Name));
                candidates.Add(new($"{display} {alias} ON {condition}", CompletionKind.Join), name, $"{schema}.{name}");
            }
        }
    }

    /// <summary>After ON: the foreign key conditions between the table just joined and the ones before it.</summary>
    private static void AddConditions(Candidates candidates, List<Reference> before, SchemaCatalog catalog, string database)
    {
        if (before.Count < 2 || Resolve(before[^1].Parts, catalog, database) is not var (owner, table)) return;
        var keys = catalog.ForeignKeys(owner) ?? [];
        var joined = Quote(before[^1].Alias ?? before[^1].Name);
        foreach (var earlier in before.SkipLast(1))
        {
            if (Resolve(earlier.Parts, catalog, database) is not var (otherOwner, other) || !Same(otherOwner, owner)) continue;
            foreach (var (schema, name, pairs) in Links(table, keys))
            {
                if (!Same(schema, other.Schema) || !Same(name, other.Name)) continue;
                var condition = Condition(joined, pairs, Quote(earlier.Alias ?? earlier.Name));
                candidates.Add(new(condition, CompletionKind.Join), condition);
            }
        }
    }

    /// <summary>The tables a foreign key links to <paramref name="table"/>, either way, pairing its columns with theirs.</summary>
    private static IEnumerable<(string Schema, string Name, List<(string Mine, string Theirs)> Pairs)> Links(DbObject table, IReadOnlyList<ForeignKey> keys)
    {
        foreach (var key in keys)
        {
            if (Same(key.FromSchema, table.Schema) && Same(key.FromTable, table.Name))
            {
                yield return (key.ToSchema, key.ToTable, [.. key.Columns.Select(c => (c.From, c.To))]);
            }

            if (Same(key.ToSchema, table.Schema) && Same(key.ToTable, table.Name))
            {
                yield return (key.FromSchema, key.FromTable, [.. key.Columns.Select(c => (c.To, c.From))]);
            }
        }
    }

    private static string Condition(string left, IEnumerable<(string Left, string Right)> pairs, string right) =>
        string.Join(" AND ", pairs.Select(p => $"{left}.{Quote(p.Left)} = {right}.{Quote(p.Right)}"));

    /// <summary>The initials of a table name ("order_lines" → "ol"), numbered when that alias or name is in use.</summary>
    private static string Alias(string name, IReadOnlySet<string> used)
    {
        var initials = new string([.. name.Where((c, i) => char.IsLetter(c)
            && (i == 0 || name[i - 1] is '_' or ' ' || (char.IsUpper(c) && char.IsLower(name[i - 1]))))]).ToLowerInvariant();
        if (initials.Length == 0) initials = "t";
        var alias = initials;
        for (var n = 2; used.Contains(alias) || Quote(alias) != alias; n++) alias = $"{initials}{n}";
        return alias;
    }

    /// <summary>
    /// Tables and alias.column names the catalog does not have. Only what is certain is marked: names not loaded yet,
    /// temporary tables, CTEs, tables the script creates, system schemas, other servers and unlisted databases are not,
    /// nor the word touching <paramref name="cursor"/>, which is still being typed.
    /// </summary>
    public static IReadOnlyList<Diagnostic> Diagnose(IReadOnlyList<string> lines, SchemaCatalog catalog, string database, (int Row, int Column)? cursor)
    {
        var tokens = SqlLexer.Tokenize(lines, SqlTokenizer.Classify(lines));
        var known = DefinedNames(tokens);
        var diagnostics = new List<Diagnostic>();
        bool Typing(Token token) => cursor is var (row, column) && token.Row == row && token.Start <= column && column <= token.End;

        for (var start = 0; start < tokens.Count; start++)
        {
            if (tokens[start].Type == TokenType.Separator) continue;
            var end = tokens.FindIndex(start, t => t.Type == TokenType.Separator) switch { -1 => tokens.Count, var i => i };
            var references = References(tokens, start);
            var names = new HashSet<int>();
            foreach (var reference in references)
            {
                for (var part = 0; part < reference.Parts.Count; part++) names.Add(reference.Position + 2 * part);
                var first = tokens[reference.Position];
                var last = tokens[reference.Position + 2 * (reference.Parts.Count - 1)];
                if (Typing(last) || !Checked(reference, references, tokens, known, catalog, database)) continue;
                var (owner, schema) = (reference.Parts.Count == 3 ? reference.Parts[0] : database, reference.Parts.Count >= 2 ? reference.Parts[^2] : null);
                if (catalog.Objects(owner) is not { } objects) continue;
                if (objects.Any(item => Same(item.Name, reference.Name) && (schema is null || Same(item.Schema, schema)))) continue;
                var span = first.Row == last.Row ? first.Start : last.Start;
                diagnostics.Add(new(last.Row, span, last.End, $"Unknown table or view: {string.Join('.', reference.Parts)}"));
            }

            // alias.column, where the alias (or an unaliased table name) resolves to a table whose columns are loaded.
            for (var i = start; i + 2 < end; i++)
            {
                if (names.Contains(i) || tokens[i].Type != TokenType.Word || tokens[i + 1].Type != TokenType.Dot || tokens[i + 2].Type != TokenType.Word) continue;
                if ((i > 0 && tokens[i - 1].Type == TokenType.Dot) || (i + 3 < end && tokens[i + 3].Type == TokenType.Dot)) continue;
                var (qualifier, column) = (tokens[i], tokens[i + 2]);
                if (qualifier.End != tokens[i + 1].Start || tokens[i + 1].End != column.Start || Typing(column)) continue;
                var owner = references.FirstOrDefault(r => r.Alias is { } alias && Same(alias, qualifier.Text))
                    ?? references.FirstOrDefault(r => r.Alias is null && Same(r.Name, qualifier.Text));
                if (owner is null || Resolve(owner.Parts, catalog, database) is not var (db, table)) continue;
                if (catalog.Columns(db, table) is not { } columns || columns.Any(c => Same(c.Name, column.Text))) continue;
                diagnostics.Add(new(column.Row, column.Start, column.End, $"Unknown column: {table.Schema}.{table.Name}.{column.Text}"));
            }

            start = end;
        }

        return diagnostics;
    }

    /// <summary>Whether a table reference names something the catalog should have.</summary>
    private static bool Checked(Reference reference, List<Reference> statement, List<Token> tokens, HashSet<string> known, SchemaCatalog catalog, string database)
    {
        var keyword = tokens[reference.Keyword].Text;
        // CREATE/ALTER/DROP TABLE and SELECT ... INTO name tables that may not exist yet.
        if (Same(keyword, "TABLE")) return false;
        if (Same(keyword, "INTO") && !(reference.Keyword > 0 && tokens[reference.Keyword - 1] is { Type: TokenType.Word } before
                                       && (Same(before.Text, "INSERT") || Same(before.Text, "MERGE")))) return false;
        if (reference.Parts.Count > 3 || reference.Name.StartsWith('#') || known.Contains(reference.Name)) return false;
        if (reference.Parts.Count >= 2 && (Same(reference.Parts[^2], "sys") || Same(reference.Parts[^2], "INFORMATION_SCHEMA"))) return false;
        // UPDATE c ... FROM customers c names an alias.
        if (reference.Parts.Count == 1 && statement.Any(r => r.Alias is { } alias && Same(alias, reference.Name))) return false;
        return reference.Parts.Count < 3 || Same(reference.Parts[0], database) || catalog.Databases.Any(name => Same(name, reference.Parts[0]));
    }

    /// <summary>Names the script itself defines: CTEs and the tables, views and synonyms it creates.</summary>
    private static HashSet<string> DefinedNames(List<Token> tokens)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Word) continue;
            var before = tokens[i - 1];
            var next = i + 1 < tokens.Count ? tokens[i + 1] : null;
            // WITH name AS (, , name AS (, WITH name (columns) AS (
            var cte = (before.Type == TokenType.Comma || before is { Type: TokenType.Word, Bracketed: false } && Same(before.Text, "WITH"))
                && (next?.Type == TokenType.OpenParen
                    || (next is { Type: TokenType.Word, Bracketed: false } && Same(next.Text, "AS") && i + 2 < tokens.Count && tokens[i + 2].Type == TokenType.OpenParen));
            if (cte) names.Add(tokens[i].Text);

            if (i >= 1 && before.Type == TokenType.Word && (Same(before.Text, "TABLE") || Same(before.Text, "VIEW") || Same(before.Text, "SYNONYM"))
                && i >= 2 && tokens[i - 2] is { Type: TokenType.Word } create && Same(create.Text, "CREATE"))
            {
                var j = i;
                while (j + 2 < tokens.Count && tokens[j + 1].Type == TokenType.Dot && tokens[j + 2].Type == TokenType.Word) j += 2;
                names.Add(tokens[j].Text);
            }
        }

        return names;
    }

    private static void AddObjects(Candidates candidates, IReadOnlyList<DbObject> objects, string schema, bool routines)
    {
        foreach (var item in objects)
        {
            if (Same(item.Schema, schema) && (item.Kind == DbObjectKind.Procedure) == routines) candidates.Add(new(Quote(item.Name), KindOf(item)), item.Name);
        }
    }

    private static void AddColumns(Candidates candidates, (string Database, DbObject Table)? target, SchemaCatalog catalog)
    {
        if (target is not var (database, table)) return;
        foreach (var column in catalog.Columns(database, table) ?? [])
        {
            candidates.Add(new(Quote(column.Name), CompletionKind.Column, column.DataType), column.Name);
        }
    }

    /// <summary>The table or view a name refers to: [[database.]schema.]name, preferring dbo when the schema is omitted.</summary>
    private static (string Database, DbObject Table)? Resolve(IReadOnlyList<string> parts, SchemaCatalog catalog, string database)
    {
        var name = parts[^1];
        var schema = parts.Count >= 2 ? parts[^2] : null;
        var owner = parts.Count >= 3 ? parts[^3] : database;
        if (catalog.Objects(owner) is not { } objects) return null;
        var matches = objects
            .Where(item => item.Kind is DbObjectKind.Table or DbObjectKind.View && Same(item.Name, name) && (schema is null || Same(item.Schema, schema)))
            .ToList();
        var match = matches.FirstOrDefault(item => Same(item.Schema, "dbo")) ?? matches.FirstOrDefault();
        return match is null ? null : (owner, match);
    }

    /// <summary>
    /// Tables named after FROM, JOIN, UPDATE, INTO or APPLY in the statement around <paramref name="position"/>, with their
    /// alias; a FROM list continues after each comma. Statements end at a semicolon or GO.
    /// </summary>
    private static List<Reference> References(List<Token> tokens, int position)
    {
        var from = position == 0 ? -1 : tokens.FindLastIndex(position - 1, t => t.Type == TokenType.Separator);
        var to = position >= tokens.Count ? tokens.Count : tokens.FindIndex(position, t => t.Type == TokenType.Separator);
        if (to < 0) to = tokens.Count;

        var references = new List<Reference>();
        for (var i = from + 1; i < to; i++)
        {
            if (tokens[i].Type != TokenType.Word || tokens[i].Bracketed || !ObjectKeywords.Contains(tokens[i].Text)) continue;
            var j = i + 1;
            while (true)
            {
                var start = j;
                var parts = new List<string>();
                while (j < to && tokens[j].Type == TokenType.Word && !IsKeyword(tokens[j]))
                {
                    parts.Add(tokens[j++].Text);
                    if (j < to && tokens[j].Type == TokenType.Dot) j++;
                    else break;
                }

                // Nothing named, or a table-valued function call; after INTO the parenthesis lists columns.
                if (parts.Count == 0 || (j < to && tokens[j].Type == TokenType.OpenParen && !Same(tokens[i].Text, "INTO"))) break;
                if (j < to && tokens[j] is { Type: TokenType.Word, Bracketed: false } asWord && Same(asWord.Text, "AS")) j++;
                string? alias = null;
                if (j < to && tokens[j].Type == TokenType.Word && !IsKeyword(tokens[j])) alias = tokens[j++].Text;
                references.Add(new Reference(parts, alias, start, i));

                if (!Same(tokens[i].Text, "FROM") || j >= to || tokens[j].Type != TokenType.Comma) break;
                j++;
            }
        }

        return references;
    }

    private static bool IsKeyword(Token token) => !token.Bracketed && SqlTokenizer.IsKeyword(token.Text);

    /// <summary>
    /// A table of the statement; <paramref name="Position"/> is the token index of its name (parts and dots alternate)
    /// and <paramref name="Keyword"/> the one of the FROM, JOIN... before it.
    /// </summary>
    private sealed record Reference(IReadOnlyList<string> Parts, string? Alias, int Position, int Keyword)
    {
        public string Name => Parts[^1];
    }

    /// <summary>The word being typed: its token, the part before the cursor, and how many characters to replace.</summary>
    private static (int? Index, string Prefix, int Remove) PrefixAt(List<Token> tokens, int row, int column)
    {
        var index = tokens.FindIndex(t => t.Type == TokenType.Word && t.Row == row && t.Start < column && column <= t.End);
        if (index < 0) return (null, string.Empty, 0);
        var token = tokens[index];
        var prefix = token.Bracketed ? token.Text[..Math.Max(0, Math.Min(token.Text.Length, column - token.Start - 1))] : token.Text[..(column - token.Start)];
        return (index, prefix, column - token.Start);
    }

    private static CompletionKind KindOf(DbObject item) => item.Kind switch
    {
        DbObjectKind.Table => CompletionKind.Table,
        DbObjectKind.View => CompletionKind.View,
        DbObjectKind.Procedure => CompletionKind.Procedure,
        _ => CompletionKind.Function,
    };

    /// <summary>
    /// The name to insert. An unqualified name resolves in the user's default schema and then in dbo, so a dbo object
    /// whose name no other schema uses needs no schema; anything else keeps it.
    /// </summary>
    private static string Qualified(DbObject item, IReadOnlyList<DbObject> objects) =>
        Same(item.Schema, "dbo") && objects.Count(other => Same(other.Name, item.Name)) == 1
            ? Quote(item.Name)
            : $"{Quote(item.Schema)}.{Quote(item.Name)}";

    /// <summary>Brackets a name unless it is a regular identifier that is not a keyword.</summary>
    public static string Quote(string name)
    {
        var regular = name.Length > 0
            && (char.IsLetter(name[0]) || name[0] is '_' or '#')
            && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '#' or '@' or '$')
            && !SqlTokenizer.IsKeyword(name);
        return regular ? name : $"[{name.Replace("]", "]]")}]";
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Collects matches of the prefix, keeping the first of each text: a name typed in full first, so it shows that it
    /// exists, then by kind and by name.
    /// </summary>
    private sealed class Candidates(string prefix)
    {
        private readonly List<CompletionItem> _items = [];
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<CompletionItem> _exact = [];

        public string Prefix { get; } = prefix;

        public void Add(CompletionItem item, params string[] matchOn)
        {
            var exact = Prefix.Length > 0 && matchOn.Any(name => name.Equals(Prefix, StringComparison.OrdinalIgnoreCase));
            // A keyword typed in full is not a suggestion.
            if (exact && item.Kind == CompletionKind.Keyword) return;
            if (!matchOn.Any(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) || !_seen.Add(item.Text)) return;
            _items.Add(item);
            if (exact) _exact.Add(item);
        }

        public IReadOnlyList<CompletionItem> Ordered() =>
            [.. _items.OrderBy(item => !_exact.Contains(item)).ThenBy(item => item.Kind).ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase).Take(MaxItems)];
    }
}
