using System.Globalization;

namespace LazyTsql.Tui;

/// <summary>
/// Panel sizes, changed with Alt+arrows: the left column's width and the connections and files heights as percentages
/// of the screen, the query editor's height as a percentage, and the messages panel in rows. Schema and results fill
/// what is left, so growing them shrinks files and the query.
/// </summary>
public sealed record PanelLayout
{
    public const int Step = 2, DefaultLeft = 28, DefaultConnections = 20, DefaultFiles = 30, DefaultQuery = 38, DefaultMessages = 7;
    private const int MaxLeftPanels = 85;

    public int Left { get; private set; } = DefaultLeft;
    public int Connections { get; private set; } = DefaultConnections;
    public int Files { get; private set; } = DefaultFiles;
    public int Query { get; private set; } = DefaultQuery;
    public int MessagesRows { get; private set; } = DefaultMessages;

    /// <summary>Makes the panel of <paramref name="panel"/> taller (<paramref name="direction"/> 1) or shorter (-1).</summary>
    public void Resize(KeyContext panel, int direction)
    {
        switch (panel)
        {
            case KeyContext.Connections:
                Connections = Math.Clamp(Connections + direction * Step, 8, Math.Min(50, MaxLeftPanels - Files));
                break;
            case KeyContext.Files or KeyContext.FileSearch:
                Files = Math.Clamp(Files + direction * Step, 8, Math.Min(60, MaxLeftPanels - Connections));
                break;
            case KeyContext.Schema:
                Files = Math.Clamp(Files - direction * Step, 8, Math.Min(60, MaxLeftPanels - Connections));
                break;
            case KeyContext.Query:
                Query = Math.Clamp(Query + direction * Step, 15, 80);
                break;
            case KeyContext.Results:
                Query = Math.Clamp(Query - direction * Step, 15, 80);
                break;
            case KeyContext.Messages:
                MessagesRows = Math.Clamp(MessagesRows + direction, 3, 20);
                break;
        }
    }

    /// <summary>Makes the left column wider (1) or narrower (-1).</summary>
    public void ResizeLeft(int direction) => Left = Math.Clamp(Left + direction * Step, 15, 60);

    /// <summary>"left connections files query messages", as saved between sessions.</summary>
    public override string ToString() => string.Join(' ', Left, Connections, Files, Query, MessagesRows);

    /// <summary>A saved layout, or the defaults when the text is not one this version would write.</summary>
    public static PanelLayout Parse(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var numbers = parts.Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1).ToArray();
        if (numbers is not [var left, var connections, var files, var query, var messages]) return new PanelLayout();
        var layout = new PanelLayout { Left = left, Connections = connections, Files = files, Query = query, MessagesRows = messages };
        var valid = left is >= 15 and <= 60 && connections is >= 8 and <= 50 && files is >= 8 and <= 60 && connections + files <= MaxLeftPanels
            && query is >= 15 and <= 80 && messages is >= 3 and <= 20;
        return valid ? layout : new PanelLayout();
    }
}
