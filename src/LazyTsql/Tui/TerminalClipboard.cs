using LazyTsql.Infrastructure;
using Terminal.Gui.App;

namespace LazyTsql.Tui;

/// <summary>
/// The clipboard Terminal.Gui's text views copy to and paste from (Ctrl+C, Ctrl+X, Ctrl+V). Terminal.Gui only knows
/// xclip on Linux, which cannot see a Wayland session's clipboard, so copying from the editor did nothing there. This
/// goes through <see cref="SystemClipboard"/> first and falls back to the driver's own clipboard (Windows). It never
/// throws: a view that copies with nowhere to copy to just copies nothing.
/// </summary>
public sealed class TerminalClipboard(SystemClipboard system, IClipboard? fallback) : IClipboard
{
    public bool IsSupported => true;

    public string GetClipboardData() => TryGetClipboardData(out var text) ? text : string.Empty;

    public void SetClipboardData(string text) => TrySetClipboardData(text);

    public bool TryGetClipboardData(out string result)
    {
        if (system.Read() is { } text)
        {
            result = text;
            return true;
        }

        if (fallback is { IsSupported: true } && fallback.TryGetClipboardData(out var other))
        {
            result = other;
            return true;
        }

        result = string.Empty;
        return false;
    }

    public bool TrySetClipboardData(string text) =>
        system.Write(text) || (fallback is { IsSupported: true } && fallback.TrySetClipboardData(text));
}
