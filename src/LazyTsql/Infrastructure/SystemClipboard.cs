using System.Diagnostics;

namespace LazyTsql.Infrastructure;

/// <summary>
/// Reads the operating system clipboard with the tool each desktop provides. Terminal.Gui only knows xclip on Linux,
/// which cannot see the clipboard of a Wayland session, so Ctrl+V pasted nothing there.
/// </summary>
public sealed class SystemClipboard(string platform, Func<string, string?> getEnvironmentVariable, Func<string, string[], string?> run)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static SystemClipboard Default() =>
        new(OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "linux", Environment.GetEnvironmentVariable, Run);

    /// <summary>The clipboard text, or null when no tool could read it (Windows is left to Terminal.Gui).</summary>
    public string? Read()
    {
        foreach (var (file, args) in Readers())
        {
            if (run(file, args) is { } text) return text;
        }

        return null;
    }

    private IEnumerable<(string File, string[] Args)> Readers()
    {
        if (platform == "macos")
        {
            yield return ("pbpaste", []);
            yield break;
        }

        if (platform != "linux") yield break;
        if (getEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 }) yield return ("wl-paste", ["--no-newline"]);
        if (getEnvironmentVariable("DISPLAY") is { Length: > 0 } || getEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 })
        {
            yield return ("xclip", ["-selection", "clipboard", "-o"]);
            yield return ("xsel", ["--clipboard", "--output"]);
        }
    }

    /// <summary>Runs a tool and returns its output, or null when it is missing, fails or hangs.</summary>
    private static string? Run(string file, string[] args)
    {
        try
        {
            var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
