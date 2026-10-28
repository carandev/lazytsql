namespace LazyTsql.Cli;

public sealed class CliException(string message) : Exception(message);

/// <summary>Minimal parser: a command, positional values and <c>-x value</c> / <c>--flag</c> options.</summary>
public sealed class CliArguments
{
    private static readonly HashSet<string> Flags = ["--json", "-h", "--help", "--version"];

    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public string? Command { get; private init; }

    public List<string> Positionals { get; } = [];

    public static CliArguments Parse(IReadOnlyList<string> args)
    {
        var parsed = new CliArguments { Command = args.Count > 0 && !args[0].StartsWith('-') ? args[0] : null };
        for (var i = parsed.Command is null ? 0 : 1; i < args.Count; i++)
        {
            var arg = args[i];
            if (Flags.Contains(arg))
            {
                parsed._flags.Add(arg);
            }
            else if (arg.StartsWith('-') && arg.Length > 1)
            {
                if (i + 1 >= args.Count) throw new CliException($"Option '{arg}' requires a value.");
                parsed._options[arg] = args[++i];
            }
            else
            {
                parsed.Positionals.Add(arg);
            }
        }

        return parsed;
    }

    public bool Has(string flag) => _flags.Contains(flag);

    public string? Get(string shortName, string longName) =>
        _options.TryGetValue(shortName, out var value) || _options.TryGetValue(longName, out value) ? value : null;

    public int GetInt(string shortName, string longName, int defaultValue)
    {
        var value = Get(shortName, longName);
        if (value is null) return defaultValue;
        return int.TryParse(value, out var number) && number > 0
            ? number
            : throw new CliException($"Option '{longName}' expects a positive integer, got '{value}'.");
    }
}
