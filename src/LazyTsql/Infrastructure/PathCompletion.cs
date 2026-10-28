namespace LazyTsql.Infrastructure;

public sealed record PathEntry(string Name, bool IsDirectory);

/// <summary>
/// Shell-like path completion for typed paths: the directory part is kept as typed (including <c>~</c>)
/// and the last segment is matched case-insensitively against that directory's entries.
/// </summary>
public static class PathCompletion
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static string ExpandHome(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/')))
            : Path.GetFullPath(path);

    /// <summary>Text up to and including the last separator, as typed.</summary>
    public static string DirectoryPart(string input) => input[..(input.LastIndexOfAny(Separators) + 1)];

    /// <summary>
    /// Entries of the directory the input points into whose names start with the last segment: directories first,
    /// then files with <paramref name="extension"/> (any file when null). Hidden entries need a typed leading dot.
    /// </summary>
    public static IReadOnlyList<PathEntry> Matches(string input, string? extension)
    {
        var directoryPart = DirectoryPart(input);
        var prefix = input[directoryPart.Length..];
        string directory;
        try
        {
            directory = ExpandHome(directoryPart.Length == 0 ? "." : directoryPart);
            if (!Directory.Exists(directory)) return [];

            var showHidden = prefix.StartsWith('.');
            bool Visible(string name) =>
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (showHidden || !name.StartsWith('.'));

            var directories = Directory.EnumerateDirectories(directory)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(Visible)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(name => new PathEntry(name, IsDirectory: true));
            var files = Directory.EnumerateFiles(directory)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => Visible(name) && (extension is null || name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(name => new PathEntry(name, IsDirectory: false));
            return [.. directories, .. files];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>Tab completion: a single match completes fully (directories get a trailing separator), several complete their common prefix.</summary>
    public static string Complete(string input, string? extension)
    {
        var matches = Matches(input, extension);
        var directoryPart = DirectoryPart(input);
        if (matches.Count == 0) return input;
        if (matches.Count == 1)
        {
            var only = matches[0];
            return directoryPart + only.Name + (only.IsDirectory ? Path.DirectorySeparatorChar.ToString() : string.Empty);
        }

        var first = matches[0].Name;
        var length = matches.Skip(1).Aggregate(first.Length, (common, entry) =>
        {
            var i = 0;
            while (i < common && i < entry.Name.Length && char.ToLowerInvariant(first[i]) == char.ToLowerInvariant(entry.Name[i])) i++;
            return i;
        });
        var typed = input.Length - directoryPart.Length;
        return length > typed ? directoryPart + first[..length] : input;
    }
}
