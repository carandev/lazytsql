namespace LazyTsql.Infrastructure;

/// <summary>Where lazytsql keeps what it records over time: <c>$XDG_STATE_HOME/lazytsql</c>, by default <c>~/.local/state/lazytsql</c>.</summary>
public static class StateDirectory
{
    public static string Resolve(Func<string, string?> getEnvironmentVariable)
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lazytsql");
        var stateHome = getEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } state
            ? state
            : Path.Combine(getEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        return Path.Combine(stateHome, "lazytsql");
    }
}
