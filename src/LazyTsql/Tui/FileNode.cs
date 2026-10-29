namespace LazyTsql.Tui;

/// <summary>
/// A folder or <c>.sql</c> script in the files panel. Children are read from disk each time the tree asks, so a
/// refresh shows new files; nodes compare by path, so a refresh keeps the same folders expanded.
/// </summary>
public sealed record FileNode(string Path, bool IsDirectory)
{
    public static FileNode Directory(string path) => new(System.IO.Path.GetFullPath(path), IsDirectory: true);

    public string Label => IsDirectory ? $"{System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar))}/" : System.IO.Path.GetFileName(Path);

    /// <summary>Folders first, then scripts, each by name; hidden folders such as <c>.git</c> are left out.</summary>
    public IReadOnlyList<FileNode> Children()
    {
        if (!IsDirectory) return [];
        try
        {
            var directory = new DirectoryInfo(Path);
            var folders = directory.EnumerateDirectories()
                .Where(folder => !folder.Name.StartsWith('.') && !folder.Attributes.HasFlag(FileAttributes.Hidden))
                .Select(folder => new FileNode(folder.FullName, IsDirectory: true));
            var scripts = directory.EnumerateFiles()
                .Where(file => file.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase))
                .Select(file => new FileNode(file.FullName, IsDirectory: false));
            return [.. folders.OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase), .. scripts.OrderBy(node => node.Path, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    public override string ToString() => Label;
}
