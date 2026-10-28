using LazyTsql.Core.Completion;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// Feeds the editor popup from <see cref="SqlCompletion"/>. The popup only passes the current line, so the generator
/// reads the whole script from the editor to see the statement around the cursor.
/// </summary>
internal sealed class SqlSuggestionGenerator(TextView editor, Func<(SchemaCatalog Catalog, string Database)?> source) : ISuggestionGenerator
{
    public IEnumerable<Suggestion> GenerateSuggestions(AutocompleteContext context)
    {
        if (source() is not var (catalog, database)) return [];
        var lines = editor.Text.ReplaceLineEndings("\n").Split('\n');
        var result = SqlCompletion.Suggest(lines, editor.CurrentRow, context.CursorPosition, catalog, database);
        return result.Items.Select(item => new Suggestion(result.Remove, item.Text, Title(item)));
    }

    public bool IsWordChar(string text) => text.Length == 1 && (char.IsLetterOrDigit(text[0]) || text[0] is '_' or '#' or '.' or '[');

    private static string Title(CompletionItem item) => $"{item.Text}  {item.Detail ?? Kind(item.Kind)}";

    private static string Kind(CompletionKind kind) => kind switch
    {
        CompletionKind.Procedure => "proc",
        CompletionKind.Function => "fn",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
