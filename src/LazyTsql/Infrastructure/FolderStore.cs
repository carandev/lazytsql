using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazyTsql.Infrastructure;

/// <summary>
/// The folders shown in the files panel, kept next to the connections in <c>folders.json</c> as a JSON array of
/// absolute paths, in the order they were added.
/// </summary>
public sealed class FolderStore(string path)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string Path { get; } = path;

    public static FolderStore Default() =>
        new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FileConnectionStore.Default().Path)!, "folders.json"));

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(Path)) return [];
        using var stream = File.OpenRead(Path);
        return JsonSerializer.Deserialize(stream, FolderStoreJsonContext.Default.ListString) ?? [];
    }

    /// <summary>Saves a folder unless it is already there; returns whether it was added.</summary>
    public bool Add(string folder)
    {
        var folders = Load();
        var normalized = Normalize(folder);
        if (folders.Contains(normalized, PathComparer)) return false;
        Save([.. folders, normalized]);
        return true;
    }

    public bool Remove(string folder)
    {
        var folders = Load();
        var normalized = Normalize(folder);
        var remaining = folders.Where(saved => !PathComparer.Equals(saved, normalized)).ToList();
        if (remaining.Count == folders.Count) return false;
        Save(remaining);
        return true;
    }

    /// <summary>An absolute path without a trailing separator, so "scripts/" and "./scripts" are the same folder.</summary>
    public static string Normalize(string folder)
    {
        var full = System.IO.Path.GetFullPath(folder);
        var root = System.IO.Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) : full;
    }

    private void Save(List<string> folders)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + ".tmp";
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, folders, FolderStoreJsonContext.Default.ListString);
        }

        File.Move(temporary, Path, overwrite: true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class FolderStoreJsonContext : JsonSerializerContext;
