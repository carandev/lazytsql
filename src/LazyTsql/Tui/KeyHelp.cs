namespace LazyTsql.Tui;

/// <summary>The <c>?</c> dialog text: sections of keys laid out in two aligned columns.</summary>
public static class KeyHelp
{
    private sealed record Section(string Title, (string Keys, string Action)[] Keys);

    private static readonly Section[] Left =
    [
        new("Navigation", [
            ("1-5 · Alt+1-5", "focus panel"),
            ("Tab", "next panel"),
            ("h j k l", "move"),
            ("g · G", "top · bottom"),
            ("Ctrl+D · Ctrl+U", "page down · up"),
            ("?", "this help"),
            ("q · Ctrl+Q", "quit"),
        ]),
        new("Connections [1]", [("Enter", "connect"), ("a", "add"), ("e", "edit"), ("d", "delete")]),
        new("Files", [("Ctrl+O", "open .sql file"), ("Ctrl+S", "save .sql file")]),
    ];

    private static readonly Section[] Right =
    [
        new("Schema [2]", [
            ("Enter · l", "expand"),
            ("h", "collapse"),
            ("s", "SELECT TOP 100"),
            ("e", "open definition"),
            ("r", "reload"),
        ]),
        new("Query [3]", [("F5 · Ctrl+E", "run selection or all"), ("Ctrl+K", "cancel query"), ("Esc", "leave editor")]),
        new("Results [4]", [("[ · ]", "previous · next set"), ("x", "export to CSV")]),
    ];

    private const string Footer = "Without a selection, F5 runs the whole script batch by batch (GO).";

    public static IReadOnlyList<string> Render()
    {
        var left = Column(Left);
        var right = Column(Right);
        var leftWidth = left.Max(line => line.Length);
        var lines = Enumerable.Range(0, Math.Max(left.Count, right.Count))
            .Select(i => $"{(i < left.Count ? left[i] : string.Empty).PadRight(leftWidth)}    {(i < right.Count ? right[i] : string.Empty)}".TrimEnd())
            .ToList();
        lines.Add(string.Empty);
        lines.Add(Footer);
        return lines;
    }

    private static List<string> Column(Section[] sections)
    {
        var keyWidth = sections.SelectMany(section => section.Keys).Max(key => key.Keys.Length);
        var lines = new List<string>();
        foreach (var section in sections)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add(section.Title);
            lines.AddRange(section.Keys.Select(key => $"  {key.Keys.PadRight(keyWidth)}  {key.Action}"));
        }

        return lines;
    }
}
