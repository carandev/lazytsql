namespace LazyTsql.Tui;

/// <summary>The search behind <c>/</c> in the files panel: folders and scripts whose name contains the query, at any depth.</summary>
public static class FileSearch
{
    // A search from a huge working directory stops here instead of walking the whole disk.
    private const int MaxVisited = 20_000;

    /// <summary>
    /// Matches in the order the tree shows them: depth first, folders before scripts, ignoring case. Linked folders are
    /// listed but not entered, so a link back to a parent cannot loop. A cancelled search returns what it found so far.
    /// </summary>
    public static IReadOnlyList<FileNode> Matches(FileNode root, string query, CancellationToken cancellationToken = default)
    {
        var matches = new List<FileNode>();
        if (string.IsNullOrEmpty(query)) return matches;

        var visited = 0;
        void Walk(FileNode folder)
        {
            foreach (var node in folder.Children())
            {
                if (++visited > MaxVisited || cancellationToken.IsCancellationRequested) return;
                if (System.IO.Path.GetFileName(node.Path).Contains(query, StringComparison.OrdinalIgnoreCase)) matches.Add(node);
                if (node.IsDirectory && !IsLink(node.Path)) Walk(node);
            }
        }

        if (!cancellationToken.IsCancellationRequested) Walk(root);
        return matches;
    }

    private static bool IsLink(string path)
    {
        try
        {
            return new DirectoryInfo(path).LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>The folders to expand, from the root down to the one holding <paramref name="node"/>.</summary>
    public static IReadOnlyList<FileNode> Ancestors(FileNode root, FileNode node)
    {
        var folders = new List<FileNode> { root };
        var relative = System.IO.Path.GetRelativePath(root.Path, System.IO.Path.GetDirectoryName(node.Path)!);
        if (relative == ".") return folders;

        var path = root.Path;
        foreach (var part in relative.Split(System.IO.Path.DirectorySeparatorChar))
        {
            path = System.IO.Path.Combine(path, part);
            folders.Add(FileNode.Directory(path));
        }

        return folders;
    }

    /// <summary>The next (<paramref name="step"/> 1) or previous (-1) match, wrapping around like vim's n and N.</summary>
    public static int Step(int current, int step, int count) => ((current + step) % count + count) % count;
}
