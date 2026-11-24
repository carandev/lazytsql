using System.Drawing;
using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;
using LazyTsql.Infrastructure;

namespace LazyTsql.Tui;

/// <summary>A result set with the session and batch that produced it, so an export can fetch every row again.</summary>
/// <summary>The database it ran in is where its query is described for editing; its edits stay until saved or discarded.</summary>
public sealed record ShownResult(ResultSet Set, IDatabaseSession Session, SqlBatch Batch, int Index, string Database)
{
    public ResultEdits? Edits { get; set; }
}

/// <summary>
/// One query in the editor: its text, the script it belongs to (none for an untitled query), where the cursor was, and
/// the result sets it last ran, so switching back restores it all.
/// </summary>
public sealed class QueryTab(ScriptFile? script, string text, string savedText)
{
    public ScriptFile? Script { get; set; } = script;
    public string Text { get; set; } = text;
    /// <summary>The text as last saved, or as it opened for an untitled query.</summary>
    public string SavedText { get; set; } = savedText;
    public Point Cursor { get; set; }
    public List<ShownResult> ResultSets { get; set; } = [];
    public int ResultIndex { get; set; }
    /// <summary>The profile name this tab is connected to, or null; several tabs can share the same one.</summary>
    public string? ConnectionName { get; set; }

    public string Name => ConnectionName is { } name && Script?.Path == ConnectionConsole.PathFor(name) ? $"[console] {name}" : Script?.Name ?? "untitled";

    /// <summary>A script whose text differs from the file.</summary>
    public bool IsDirty => Script is not null && Text != SavedText;

    /// <summary>Text that closing the tab would lose: a dirty script, or an edited, non-empty untitled query.</summary>
    public bool HasUnsavedText => IsDirty || (Script is null && Text.Length > 0 && Text != SavedText);

    internal bool IsUntouchedUntitled => Script is null && Text == SavedText;
}

/// <summary>The queries open in the editor, like the result sets: one shown at a time, the others kept as they were left.</summary>
public sealed class QueryTabs
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private readonly List<QueryTab> _tabs;

    public QueryTabs(string text) => _tabs = [new QueryTab(null, text, text)];

    public IReadOnlyList<QueryTab> All => _tabs;
    public int Count => _tabs.Count;
    public int Index { get; private set; }
    public QueryTab Current => _tabs[Index];
    public IEnumerable<QueryTab> Dirty => _tabs.Where(tab => tab.IsDirty);

    /// <summary>Where the current tab is, like the results' <c>1/3</c>; empty with a single tab.</summary>
    public string Position => _tabs.Count > 1 ? $" {Index + 1}/{_tabs.Count}" : string.Empty;

    /// <summary>
    /// A script already open switches to its tab, edits kept; otherwise it takes an untitled tab nobody edited, or a new
    /// tab after the current one. Generated SQL (no script) always gets a new tab, so it never replaces a query.
    /// </summary>
    public QueryTab Open(ScriptFile? script, string text)
    {
        if (script is not null)
        {
            var open = _tabs.FindIndex(tab => string.Equals(tab.Script?.Path, script.Path, PathComparison));
            if (open >= 0)
            {
                Index = open;
                return Current;
            }

            if (Current.IsUntouchedUntitled)
            {
                _tabs[Index] = new QueryTab(script, text, text) { ResultSets = Current.ResultSets, ResultIndex = Current.ResultIndex, ConnectionName = Current.ConnectionName };
                return Current;
            }
        }

        _tabs.Insert(Index + 1, new QueryTab(script, text, text));
        Index++;
        return Current;
    }

    /// <summary>A new, empty untitled tab after the current one.</summary>
    public QueryTab Add() => Open(null, string.Empty);

    /// <summary>Selects a tab, wrapping around both ends.</summary>
    public void Select(int index) => Index = ((index % _tabs.Count) + _tabs.Count) % _tabs.Count;

    /// <summary>Closes the current tab and shows the one before it; closing the last leaves an empty untitled tab.</summary>
    public void Close()
    {
        _tabs.RemoveAt(Index);
        if (_tabs.Count == 0) _tabs.Add(new QueryTab(null, string.Empty, string.Empty));
        Index = Math.Clamp(Index - 1, 0, _tabs.Count - 1);
    }
}
