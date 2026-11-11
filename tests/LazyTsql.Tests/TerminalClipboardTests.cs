using LazyTsql.Infrastructure;
using LazyTsql.Tui;
using Terminal.Gui.App;

namespace LazyTsql.Tests;

public class TerminalClipboardTests
{
    private readonly List<(string Command, string Input)> _writes = [];

    private SystemClipboard Wayland(string? contents, bool writes) => new("linux", name => name == "WAYLAND_DISPLAY" ? "wayland-1" : null,
        (file, args) => file == "wl-paste" ? contents : null,
        (file, args, input) =>
        {
            _writes.Add((string.Join(' ', [file, .. args]), input));
            return writes && file == "wl-copy";
        });

    private sealed class FakeClipboard(string? contents) : IClipboard
    {
        public string? Contents { get; private set; } = contents;
        public bool IsSupported => Contents is not null;
        public string GetClipboardData() => Contents ?? throw new NotSupportedException();
        public void SetClipboardData(string text) => Contents = text;

        public bool TryGetClipboardData(out string result)
        {
            result = Contents ?? string.Empty;
            return Contents is not null;
        }

        public bool TrySetClipboardData(string text)
        {
            Contents = text;
            return true;
        }
    }

    [Fact]
    public void The_editor_copies_with_the_desktop_tool()
    {
        var fallback = new FakeClipboard("old");
        IClipboard clipboard = new TerminalClipboard(Wayland(null, writes: true), fallback);

        clipboard.SetClipboardData("SELECT 1;");

        Assert.Equal([("wl-copy", "SELECT 1;")], _writes);
        Assert.Equal("old", fallback.Contents);
        Assert.True(clipboard.IsSupported);
    }

    [Fact]
    public void The_editor_pastes_with_the_desktop_tool()
    {
        IClipboard clipboard = new TerminalClipboard(Wayland("SELECT 2;", writes: true), new FakeClipboard("old"));

        Assert.Equal("SELECT 2;", clipboard.GetClipboardData());
        Assert.True(clipboard.TryGetClipboardData(out var text));
        Assert.Equal("SELECT 2;", text);
    }

    [Fact]
    public void Without_a_desktop_tool_it_uses_the_terminal_clipboard()
    {
        var fallback = new FakeClipboard("old");
        IClipboard clipboard = new TerminalClipboard(Wayland(null, writes: false), fallback);

        Assert.True(clipboard.TrySetClipboardData("x"));
        Assert.Equal("x", fallback.Contents);
        Assert.Equal("x", clipboard.GetClipboardData());
    }

    [Fact]
    public void Without_any_clipboard_copying_fails_without_throwing()
    {
        IClipboard clipboard = new TerminalClipboard(Wayland(null, writes: false), null);

        clipboard.SetClipboardData("x");

        Assert.False(clipboard.TrySetClipboardData("x"));
        Assert.False(clipboard.TryGetClipboardData(out var text));
        Assert.Equal(string.Empty, text);
        Assert.Equal(string.Empty, clipboard.GetClipboardData());
    }
}
