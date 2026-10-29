using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public sealed class ActivityLogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-log-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", now.Offset, "test", "test");
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 28, 20, 15, 4, 123, TimeSpan.FromHours(-5));

    [Fact]
    public void Entries_are_appended_to_one_private_file_per_day_with_time_and_source()
    {
        var logs = Path.Combine(_directory, "logs");
        var log = new ActivityLog(logs, new FixedTime(Now));

        Assert.True(log.Write("tui", "Connected to dev."));
        Assert.True(log.Write("cli", "query on dev:\nSELECT 1\nGO"));

        var file = Path.Combine(logs, "2026-10-28.log");
        Assert.Equal(
            "2026-10-28 20:15:04.123 -05:00 [tui] Connected to dev.\n" +
            "2026-10-28 20:15:04.123 -05:00 [cli] query on dev:\n    SELECT 1\n    GO\n",
            File.ReadAllText(file));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(logs));
        }
    }

    [Fact]
    public void A_log_that_cannot_be_written_reports_it_instead_of_failing()
    {
        var blocked = Path.Combine(_directory, "file");
        File.WriteAllText(blocked, string.Empty);

        Assert.False(new ActivityLog(blocked, new FixedTime(Now)).Write("tui", "lost"));
    }

    [Fact]
    public void Logs_older_than_30_days_are_deleted()
    {
        foreach (var name in new[] { "2026-09-27.log", "2026-09-28.log", "2026-10-28.log", "notes.txt" })
            File.WriteAllText(Path.Combine(_directory, name), string.Empty);

        new ActivityLog(_directory, new FixedTime(Now)).Prune();

        Assert.Equal(["2026-09-28.log", "2026-10-28.log", "notes.txt"], Directory.GetFiles(_directory).Select(Path.GetFileName).Order());
        new ActivityLog(Path.Combine(_directory, "missing"), new FixedTime(Now)).Prune();
    }

    [Theory]
    [InlineData("/custom/logs", "/state", "/custom/logs")]
    [InlineData(null, "/state", "/state/lazytsql/logs")]
    [InlineData(null, null, "/home/me/.local/state/lazytsql/logs")]
    public void The_directory_follows_LAZYTSQL_LOG_DIR_then_XDG_STATE_HOME(string? logDir, string? stateHome, string expected)
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = ActivityLog.DefaultDirectory(name => name switch
        {
            ActivityLog.EnvironmentVariable => logDir,
            "XDG_STATE_HOME" => stateHome,
            "HOME" => "/home/me",
            _ => null,
        });

        Assert.Equal(expected, directory);
    }
}
