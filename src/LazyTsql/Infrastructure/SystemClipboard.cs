using System.Diagnostics;

namespace LazyTsql.Infrastructure;

/// <summary>
/// Reads and writes the operating system clipboard with the tool each desktop provides. Terminal.Gui only knows xclip
/// on Linux, which cannot see the clipboard of a Wayland session, so Ctrl+V pasted nothing there.
/// </summary>
public sealed class SystemClipboard(
    string platform,
    Func<string, string?> getEnvironmentVariable,
    Func<string, string[], string?> run,
    Func<string, string[], string, bool> write)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static SystemClipboard Default() =>
        new(OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsWindows() ? "windows" : "linux", Environment.GetEnvironmentVariable, Run, WriteTo);

    /// <summary>The clipboard text, or null when no tool could read it (Windows is left to Terminal.Gui).</summary>
    public string? Read()
    {
        foreach (var (file, args) in Readers())
        {
            if (run(file, args) is { } text) return text;
        }

        return null;
    }

    /// <summary>Puts <paramref name="text"/> on the clipboard; false when no tool could (Windows is left to Terminal.Gui).</summary>
    public bool Write(string text) => Writers().Any(tool => write(tool.File, tool.Args, text));

    private IEnumerable<(string File, string[] Args)> Writers()
    {
        if (platform == "macos")
        {
            yield return ("pbcopy", []);
            yield break;
        }

        if (platform != "linux") yield break;
        if (getEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 }) yield return ("wl-copy", []);
        if (getEnvironmentVariable("DISPLAY") is { Length: > 0 } || getEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 })
        {
            yield return ("xclip", ["-selection", "clipboard"]);
            yield return ("xsel", ["--clipboard", "--input"]);
        }
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

    /// <summary>
    /// Runs a tool with <paramref name="input"/> on its standard input; true when it took it. Its output is redirected
    /// and never read, so nothing reaches the screen: wl-copy and xclip leave a process serving the clipboard that keeps
    /// those pipes open, and waiting for them to close would never end.
    /// </summary>
    private static bool WriteTo(string file, string[] args, string input)
    {
        try
        {
            var start = new ProcessStartInfo(file)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return false;
            process.StandardInput.Write(input);
            process.StandardInput.Close();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
