using LazyTsql.Core.Schema;
using LazyTsql.Core.Syntax;

namespace LazyTsql.Core.Completion;

public enum CompletionKind
{
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

        if (prefix.Length == 0 && qualifier.Count == 0) return CompletionResult.None;

        var context = k >= 0 && tokens[k].Type == TokenType.Word ? tokens[k].Text : null;
        var candidates = new Candidates(prefix);
        if (qualifier.Count == 0) SuggestUnqualified(candidates, context, catalog, database);
        else SuggestQualified(candidates, qualifier, context, catalog, database);
        return new CompletionResult(remove, candidates.Ordered());
    }

    private static void SuggestUnqualified(Candidates candidates, string? context, SchemaCatalog catalog, string database)
    {
        if (context is not null && ObjectKeywords.Contains(context))
        {
            foreach (var item in catalog.Objects(database) ?? [])
            {
                if (item.Kind != DbObjectKind.Procedure) candidates.Add(new(Qualified(item), KindOf(item)), item.Name, $"{item.Schema}.{item.Name}");
            }
        }
        else if (context is not null && RoutineKeywords.Contains(context))
        {
            foreach (var item in catalog.Objects(database) ?? [])
            {
                if (item.Kind == DbObjectKind.Procedure) candidates.Add(new(Qualified(item), CompletionKind.Procedure), item.Name, $"{item.Schema}.{item.Name}");
            }
        }
        else
        {
            var lower = candidates.Prefix.All(c => !char.IsLetter(c) || char.IsLower(c));
            foreach (var keyword in SqlTokenizer.KeywordNames)
            {
                candidates.Add(new(lower ? keyword.ToLowerInvariant() : keyword, CompletionKind.Keyword), keyword);
            }
        }
    }

    private static void SuggestQualified(Candidates candidates, List<string> qualifier, string? context, SchemaCatalog catalog, string database)
    {
        if (qualifier.Count != 1 || catalog.Objects(database) is not { } objects) return;

        var schema = qualifier[0];
        var routines = context is not null && RoutineKeywords.Contains(context);
        foreach (var item in objects)
        {
            if (!Same(item.Schema, schema) || (item.Kind == DbObjectKind.Procedure) != routines) continue;
            candidates.Add(new(Quote(item.Name), KindOf(item)), item.Name);
        }
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

    private static string Qualified(DbObject item) => $"{Quote(item.Schema)}.{Quote(item.Name)}";

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

    /// <summary>Collects matches of the prefix, keeping the first of each text, ordered by kind and then by name.</summary>
    private sealed class Candidates(string prefix)
    {
        private readonly List<CompletionItem> _items = [];
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

        public string Prefix { get; } = prefix;

        public void Add(CompletionItem item, params string[] matchOn)
        {
            // The word already typed in full is not a suggestion.
            if (item.Text.Equals(Prefix, StringComparison.OrdinalIgnoreCase)) return;
            if (matchOn.Any(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) && _seen.Add(item.Text)) _items.Add(item);
        }

        public IReadOnlyList<CompletionItem> Ordered() =>
            [.. _items.OrderBy(item => item.Kind).ThenBy(item => item.Text, StringComparer.OrdinalIgnoreCase).Take(MaxItems)];
    }
}
