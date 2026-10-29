using System.Globalization;
using System.Text;

namespace LazyTsql.Infrastructure;

/// <summary>
/// History of what lazytsql did, one file per day (<c>2026-10-28.log</c>) in
/// <c>$XDG_STATE_HOME/lazytsql/logs</c>, readable only by the owner and kept for 30 days. Entries carry SQL and
/// messages, never connection strings. A log that cannot be written never stops the work being logged.
/// </summary>
public sealed class ActivityLog(string directory, TimeProvider time)
{
    public const string EnvironmentVariable = "LAZYTSQL_LOG_DIR";
    private const int KeepDays = 30;
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly Lock _gate = new();

    public string Directory { get; } = directory;

    public static ActivityLog Default(Func<string, string?> getEnvironmentVariable) =>
        new(DefaultDirectory(getEnvironmentVariable), TimeProvider.System);

    public static string DefaultDirectory(Func<string, string?> getEnvironmentVariable)
    {
        if (getEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } custom) return custom;
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lazytsql", "logs");
        var stateHome = getEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } state
            ? state
            : Path.Combine(getEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        return Path.Combine(stateHome, "lazytsql", "logs");
    }

    /// <summary>Appends one entry; lines after the first are indented so each entry starts with its time.</summary>
    public bool Write(string source, string text)
    {
        var now = time.GetLocalNow();
        var entry = $"{now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} [{source}] "
            + string.Join("\n    ", text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n')) + "\n";
        try
        {
            lock (_gate)
            {
                if (OperatingSystem.IsWindows()) System.IO.Directory.CreateDirectory(Directory);
                else System.IO.Directory.CreateDirectory(Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.ReadWrite };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                using var stream = new FileStream(Path.Combine(Directory, $"{now:yyyy-MM-dd}.log"), options);
                stream.Write(Utf8WithoutBom.GetBytes(entry));
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Deletes the daily logs older than 30 days; other files in the directory are left alone.</summary>
    public void Prune()
    {
        var oldest = DateOnly.FromDateTime(time.GetLocalNow().DateTime).AddDays(-KeepDays);
        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.log"))
            {
                if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < oldest)
                    File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pruning again on the next start is enough.
        }
    }
}
