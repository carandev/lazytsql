namespace LazyTsql.Tui.Vim;

public enum ExCommandName
{
    Write,
    WriteQuit,
    Exit,
    Quit,
    QuitAll,
    Edit,
    TabNew,
    TabNext,
    TabPrevious,
    Set,
    Run,
    Cancel,
}

/// <summary>A <c>:</c> command: its name (any of vim's spellings), whether it ends in <c>!</c>, and its argument.</summary>
public sealed record ExCommand(ExCommandName Name, bool Force, string Argument)
{
    private static readonly Dictionary<string, ExCommandName> Names = new(StringComparer.Ordinal)
    {
        ["w"] = ExCommandName.Write,
        ["write"] = ExCommandName.Write,
        ["wq"] = ExCommandName.WriteQuit,
        ["x"] = ExCommandName.Exit,
        ["xit"] = ExCommandName.Exit,
        ["exit"] = ExCommandName.Exit,
        ["q"] = ExCommandName.Quit,
        ["quit"] = ExCommandName.Quit,
        ["tabc"] = ExCommandName.Quit,
        ["tabclose"] = ExCommandName.Quit,
        ["qa"] = ExCommandName.QuitAll,
        ["qall"] = ExCommandName.QuitAll,
        ["quitall"] = ExCommandName.QuitAll,
        ["e"] = ExCommandName.Edit,
        ["edit"] = ExCommandName.Edit,
        ["tabnew"] = ExCommandName.TabNew,
        ["tabe"] = ExCommandName.TabNew,
        ["tabedit"] = ExCommandName.TabNew,
        ["tabn"] = ExCommandName.TabNext,
        ["tabnext"] = ExCommandName.TabNext,
        ["tabp"] = ExCommandName.TabPrevious,
        ["tabprevious"] = ExCommandName.TabPrevious,
        ["tabN"] = ExCommandName.TabPrevious,
        ["tabNext"] = ExCommandName.TabPrevious,
        ["se"] = ExCommandName.Set,
        ["set"] = ExCommandName.Set,
        ["run"] = ExCommandName.Run,
        ["cancel"] = ExCommandName.Cancel,
    };

    /// <summary>The command, or null for one lazytsql does not have.</summary>
    public static ExCommand? Parse(string text)
    {
        text = text.Trim();
        var end = 0;
        while (end < text.Length && char.IsAsciiLetter(text[end])) end++;
        if (!Names.TryGetValue(text[..end], out var name)) return null;
        var force = end < text.Length && text[end] == '!';
        var rest = text[(force ? end + 1 : end)..];
        // A file name follows after a space; "wq!" or "w!" with nothing else is fine, "wfoo" is not a command.
        if (rest.Length > 0 && !char.IsWhiteSpace(rest[0])) return null;
        return new ExCommand(name, force, rest.Trim());
    }

    /// <summary>The argument as a path, with a leading <c>~</c> for the home folder as the shell would read it.</summary>
    public string Path =>
        Argument == "~" || Argument.StartsWith("~/", StringComparison.Ordinal)
            ? System.IO.Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Argument[1..].TrimStart('/'))
            : Argument;
}
