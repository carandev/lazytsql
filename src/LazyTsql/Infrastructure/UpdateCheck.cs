using System.Globalization;
using System.Text.Json;

namespace LazyTsql.Infrastructure;

/// <summary>
/// Asks NuGet.org for the latest lazytsql release, at most once a day, and words the notice for how lazytsql was
/// installed. Any failure (offline, slow, unexpected answer) means no notice: the check never gets in the way.
/// </summary>
public sealed class UpdateCheck(HttpClient http, string stateDirectory, TimeProvider time, Func<string, string?> getEnvironmentVariable)
{
    public const string OptOutVariable = "LAZYTSQL_NO_UPDATE_CHECK";
    private const string VersionsUrl = "https://api.nuget.org/v3-flatcontainer/lazytsql/index.json";
    private const string ReleasesUrl = "https://github.com/carandev/lazytsql/releases/latest";
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private string CachePath => Path.Combine(stateDirectory, "latest-version");

    public static UpdateCheck Default(Func<string, string?> getEnvironmentVariable) =>
        new(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, StateDirectory.Resolve(getEnvironmentVariable), TimeProvider.System, getEnvironmentVariable);

    public async Task<string?> MessageAsync(Version current, string? processPath, CancellationToken cancellationToken)
    {
        if (getEnvironmentVariable(OptOutVariable) is { Length: > 0 }) return null;
        if ((Cached() ?? await FetchAsync(cancellationToken)) is not { } latest || latest <= Normalize(current)) return null;

        // A dotnet tool lives under ~/.dotnet/tools (or a --tool-path with a .store next to it).
        var tool = processPath is not null && (processPath.Contains($"{Path.DirectorySeparatorChar}.dotnet{Path.DirectorySeparatorChar}tools", StringComparison.Ordinal)
            || processPath.Contains($"{Path.DirectorySeparatorChar}.store{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        return $"Update available: {Normalize(current).ToString(3)} → {latest.ToString(3)} · {(tool ? "dotnet tool update -g lazytsql" : ReleasesUrl)}";
    }

    /// <summary>The latest version found less than a day ago: "&lt;checked at, round-trip&gt; &lt;version&gt;".</summary>
    private Version? Cached()
    {
        try
        {
            var parts = File.ReadAllText(CachePath).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2
                && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var checkedAt)
                && time.GetUtcNow() - checkedAt < Interval
                && Version.TryParse(parts[1], out var version)
                ? version
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<Version?> FetchAsync(CancellationToken cancellationToken)
    {
        Version? latest;
        try
        {
            await using var stream = await http.GetStreamAsync(VersionsUrl, cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            // Prereleases ("0.2.0-preview.1") are not offered.
            latest = json.RootElement.GetProperty("versions").EnumerateArray()
                .Select(item => Version.TryParse(item.GetString(), out var version) ? Normalize(version) : null)
                .Max();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }

        if (latest is null) return null;
        try
        {
            Directory.CreateDirectory(stateDirectory);
            File.WriteAllText(CachePath, $"{time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)} {latest.ToString(3)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Asking again on the next start is enough.
        }

        return latest;
    }

    /// <summary>Major.minor.patch only, so "0.1.15" and the assembly's "0.1.15.0" compare equal.</summary>
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
