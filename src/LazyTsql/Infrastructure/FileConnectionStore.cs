using System.Text.Json;
using System.Text.Json.Serialization;
using LazyTsql.Core.Connections;

namespace LazyTsql.Infrastructure;

/// <summary>Stores named connections in <c>$XDG_CONFIG_HOME/lazytsql/connections.json</c>, readable only by the owner.</summary>
public sealed class FileConnectionStore(string path)
{
    public string Path { get; } = path;

    public static FileConnectionStore Default()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome))
        {
            configHome = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return new FileConnectionStore(System.IO.Path.Combine(configHome, "lazytsql", "connections.json"));
    }

    public IReadOnlyList<ConnectionProfile> Load()
    {
        if (!File.Exists(Path)) return [];
        using var stream = File.OpenRead(Path);
        return JsonSerializer.Deserialize(stream, ConnectionStoreJsonContext.Default.ListConnectionProfile) ?? [];
    }

    public void Save(IReadOnlyList<ConnectionProfile> profiles)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + ".tmp";
        using (var stream = File.Create(temporary))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            JsonSerializer.Serialize(stream, profiles.ToList(), ConnectionStoreJsonContext.Default.ListConnectionProfile);
        }

        File.Move(temporary, Path, overwrite: true);
    }

    /// <summary>Adds or replaces a profile by name; <paramref name="replacing"/> also drops the old name, which renames it.</summary>
    public void Upsert(ConnectionProfile profile, string? replacing = null)
    {
        var profiles = Load()
            .Where(p => !p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))
            .Where(p => replacing is null || !p.Name.Equals(replacing, StringComparison.OrdinalIgnoreCase))
            .Append(profile);
        Save(profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public bool Remove(string name)
    {
        var profiles = Load();
        var remaining = profiles.Where(p => !p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (remaining.Count == profiles.Count) return false;
        Save(remaining);
        return true;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<ConnectionProfile>))]
internal sealed partial class ConnectionStoreJsonContext : JsonSerializerContext;
