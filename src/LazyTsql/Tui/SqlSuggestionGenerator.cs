using LazyTsql.Core.Completion;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// Feeds the editor popup from <see cref="SqlCompletion"/>. The popup only passes the current line, so the generator
/// reads the whole script from the editor to see the statement around the cursor. The popup asks twice per keystroke,
/// so the last answer is reused while the script, the cursor and the catalog are the same.
/// </summary>
internal sealed class SqlSuggestionGenerator(SqlEditor editor, Func<(SchemaCatalog Catalog, string Database)?> source) : ISuggestionGenerator
{
    private (IReadOnlyList<string> Lines, int Row, int Column, SchemaCatalog Catalog, string Database, Suggestion[] Suggestions)? _last;

    public IEnumerable<Suggestion> GenerateSuggestions(AutocompleteContext context)
    {
        if (source() is not var (catalog, database)) return [];
        var (lines, row, column) = (editor.ScriptLines, editor.CurrentRow, context.CursorPosition);
        if (_last is { } last && ReferenceEquals(last.Lines, lines) && (last.Row, last.Column) == (row, column)
            && ReferenceEquals(last.Catalog, catalog) && last.Database == database)
        {
            return last.Suggestions;
        }

        var result = SqlCompletion.Suggest(lines, row, column, catalog, database);
        Suggestion[] suggestions = [.. result.Items.Select(item => new Suggestion(result.Remove, item.Text, Title(item)))];
        _last = (lines, row, column, catalog, database, suggestions);
        return suggestions;
    }

    /// <summary>Drops the reused answer, for when names finished loading into the same catalog.</summary>
    public void Forget() => _last = null;

    public bool IsWordChar(string text) => text.Length == 1 && (char.IsLetterOrDigit(text[0]) || text[0] is '_' or '#' or '.' or '[');

    private static string Title(CompletionItem item) => $"{item.Text}  {item.Detail ?? Kind(item.Kind)}";

    private static string Kind(CompletionKind kind) => kind switch
    {
        CompletionKind.Procedure => "proc",
        CompletionKind.Function => "fn",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
