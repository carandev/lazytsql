namespace LazyTsql.Tui;

/// <summary>Where the keyboard is: a panel of the main view or an open dialog.</summary>
public enum KeyContext
{
    Global,
    Connections,
    Schema,
    Query,
    Results,
    Messages,
    FilePicker,
    Confirm,
    ConnectionForm,
    RowLimit,
    Help,
}

/// <summary>
/// The single registry of keys. The footer shows the current context's keys and the help dialog shows the same
/// context or every key, so a new key is documented once here.
/// </summary>
public static class KeyHelp
{
    private sealed record Section(KeyContext Context, string Title, (string Keys, string Action)[] Keys);

    private static readonly Section[] Sections =
    [
        new(KeyContext.Global, "Global", [
            ("1-5/Alt+1-5", "focus panel"),
            ("Tab", "next panel"),
            ("h j k l", "move"),
            ("g/G", "top/bottom"),
            ("Ctrl+D/Ctrl+U", "page down/up"),
            ("F5/Ctrl+E", "run query"),
            ("Ctrl+O/Ctrl+S", "open/save .sql"),
            ("?/F1", "help"),
            ("q/Ctrl+Q", "quit"),
        ]),
        new(KeyContext.Connections, "Connections [1]", [("Enter", "connect"), ("a", "add"), ("e", "edit"), ("d", "delete")]),
        new(KeyContext.Schema, "Schema [2]", [
            ("Enter", "expand"),
            ("h", "collapse"),
            ("s", "SELECT TOP 100"),
            ("e", "open definition"),
            ("r", "reload"),
        ]),
        new(KeyContext.Query, "Query [3]", [
            ("F5", "run selection or all"),
            ("Ctrl+K", "cancel"),
            ("Ctrl+S", "save"),
            ("Tab/Ctrl+Space", "complete"),
            ("Esc", "leave editor"),
        ]),
        new(KeyContext.Results, "Results [4]", [
            ("[/]", "prev/next set"),
            ("x", "export to CSV"),
            ("m", "row limit"),
            ("h j k l", "move"),
        ]),
        new(KeyContext.Messages, "Messages [5]", [("j/k", "scroll")]),
        new(KeyContext.FilePicker, "File picker", [
            ("Tab", "complete"),
            ("↓", "browse"),
            ("Enter/l", "open/pick"),
            ("h/Bksp", "up"),
            ("Esc", "cancel"),
        ]),
        new(KeyContext.Confirm, "Confirmation", [("y", "yes"), ("n/Esc", "no"), ("Enter", "focused button")]),
        new(KeyContext.ConnectionForm, "Connection form", [
            ("Tab", "next field"),
            ("← →", "choose option"),
            ("Enter", "save"),
            ("Esc", "cancel"),
        ]),
        new(KeyContext.RowLimit, "Row limit", [("Enter", "apply"), ("Esc", "cancel")]),
        new(KeyContext.Help, "Help", [("a", "context/all keys"), ("j/k", "scroll"), ("Esc", "close")]),
    ];

    /// <summary>One line: the context's keys, then the global keys that still work there.</summary>
    public static string Footer(KeyContext context)
    {
        var keys = string.Join(" · ", SectionFor(context).Keys.Select(key => $"{key.Keys} {key.Action}"));
        var global = context switch
        {
            KeyContext.Global or KeyContext.Connections or KeyContext.Schema or KeyContext.Results or KeyContext.Messages =>
                "1-5 panels · ? help · q quit",
            KeyContext.Query => "Alt+1-5 panels · F1 help · Ctrl+Q quit",
            KeyContext.FilePicker or KeyContext.ConnectionForm or KeyContext.RowLimit => "F1 help",
            KeyContext.Confirm => "? help",
            _ => null,
        };
        return global is null ? keys : $"{keys} │ {global}";
    }

    /// <summary>Help text for one context, or for every context in two columns when <paramref name="context"/> is null.</summary>
    public static IReadOnlyList<string> Render(KeyContext? context)
    {
        if (context is { } only)
        {
            return [.. Lines(SectionFor(only), KeyWidth([SectionFor(only)])), string.Empty, "a  show every key · Esc close"];
        }

        var total = Sections.Sum(section => section.Keys.Length + 2);
        var leftCount = 0;
        for (var lines = 0; lines < total / 2; leftCount++) lines += Sections[leftCount].Keys.Length + 2;

        var left = Column(Sections[..leftCount]);
        var right = Column(Sections[leftCount..]);
        var leftWidth = left.Max(line => line.Length);
        var rows = Enumerable.Range(0, Math.Max(left.Count, right.Count))
            .Select(i => $"{(i < left.Count ? left[i] : string.Empty).PadRight(leftWidth)}   {(i < right.Count ? right[i] : string.Empty)}".TrimEnd())
            .ToList();
        rows.Add(string.Empty);
        rows.Add("a  show only the current panel or dialog · Esc close");
        return rows;
    }

    private static Section SectionFor(KeyContext context) => Sections.Single(section => section.Context == context);

    private static int KeyWidth(IEnumerable<Section> sections) => sections.SelectMany(section => section.Keys).Max(key => key.Keys.Length);

    private static List<string> Column(Section[] sections)
    {
        var keyWidth = KeyWidth(sections);
        var lines = new List<string>();
        foreach (var section in sections)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.AddRange(Lines(section, keyWidth));
        }

        return lines;
    }

    private static IEnumerable<string> Lines(Section section, int keyWidth) =>
        [section.Title, .. section.Keys.Select(key => $"  {key.Keys.PadRight(keyWidth)}  {key.Action}")];
}
