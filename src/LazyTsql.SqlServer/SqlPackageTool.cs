using System.Data.Common;
using System.Diagnostics;
using LazyTsql.Core;
using Microsoft.Data.SqlClient;

namespace LazyTsql.SqlServer;

/// <summary>
/// Exports and imports .bacpac files with sqlpackage, found through <see cref="EnvironmentVariable"/>, the PATH or the
/// global .NET tools folder. The connection string goes in a response file only the user can read, because a command
/// line is visible to every user of the machine.
/// </summary>
public sealed class SqlPackageTool(Func<string, string?> getEnvironmentVariable) : IBacpacTool
{
    public const string EnvironmentVariable = "LAZYTSQL_SQLPACKAGE";

    private const int ErrorLines = 5;

    public Task ExportAsync(string connectionString, string database, string file, Action<string> output, CancellationToken cancellationToken) =>
        RunAsync("Export", $"/SourceConnectionString:{Quote(WithDatabase(connectionString, database))}", $"/TargetFile:{Quote(Path.GetFullPath(file))}", output, cancellationToken);

    public Task ImportAsync(string connectionString, string database, string file, Action<string> output, CancellationToken cancellationToken) =>
        RunAsync("Import", $"/TargetConnectionString:{Quote(WithDatabase(connectionString, database))}", $"/SourceFile:{Quote(Path.GetFullPath(file))}", output, cancellationToken);

    private async Task RunAsync(string action, string connection, string file, Action<string> output, CancellationToken cancellationToken)
    {
        var executable = Locate();
        var responseFile = Path.Combine(Path.GetTempPath(), $"lazytsql-{Guid.NewGuid():N}.rsp");
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var writer = new StreamWriter(responseFile, options))
            {
                await writer.WriteAsync($"/Action:{action}\n{connection}\n{file}\n");
            }

            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add($"@{responseFile}");
            using var process = Process.Start(start) ?? throw new BacpacException($"Could not start {executable}.");
            var errors = new Queue<string>();
            process.OutputDataReceived += (_, e) => { if (e.Data is { } line) output(line); };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not { } line) return;
                lock (errors)
                {
                    errors.Enqueue(line);
                    if (errors.Count > ErrorLines) errors.Dequeue();
                }

                output(line);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }

            if (process.ExitCode != 0)
            {
                string detail;
                lock (errors) detail = errors.Count > 0 ? string.Join(" ", errors) : "no error output";
                throw new BacpacException($"sqlpackage {action.ToLowerInvariant()} failed (exit code {process.ExitCode}): {detail}");
            }
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    private string Locate()
    {
        if (getEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? configured : throw new BacpacException($"{EnvironmentVariable} points to '{configured}', which does not exist.");
        }

        var name = OperatingSystem.IsWindows() ? "sqlpackage.exe" : "sqlpackage";
        var home = getEnvironmentVariable(OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME");
        var directories = (getEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(home is null ? null : Path.Combine(home, ".dotnet", "tools"));
        return directories.OfType<string>().Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists)
            ?? throw new BacpacException($"sqlpackage not found. Install it with 'dotnet tool install -g microsoft.sqlpackage' or set {EnvironmentVariable} to its path.");
    }

    /// <summary>
    /// The connection string for <paramref name="database"/> with every value that needs it in single quotes:
    /// SqlConnectionStringBuilder uses double quotes, which a response file cannot hold inside a value.
    /// </summary>
    private static string WithDatabase(string connectionString, string database)
    {
        var normalized = new DbConnectionStringBuilder { ConnectionString = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString };
        return string.Join(';', normalized.Keys.Cast<string>().Select(key =>
        {
            var value = normalized[key]?.ToString() ?? string.Empty;
            var plain = value.Length > 0 && value.Trim() == value && value.IndexOfAny([';', '\'', '"', '=']) < 0;
            return $"{key}={(plain ? value : $"'{value.Replace("'", "''")}'")}";
        }));
    }

    // sqlpackage reads a response file value between double quotes and has no escape for one inside it.
    private static string Quote(string value) => value.Contains('"')
        ? throw new BacpacException("sqlpackage cannot read a connection string or file path containing a double quote (\"). Change that password or path.")
        : $"\"{value}\"";
}
