using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public class SystemClipboardTests
{
    private readonly List<string> _runs = [];
    private readonly List<(string Command, string Input)> _writes = [];

    // Each tool answers with its output, or null when it is missing or fails; a writer succeeds when it is in `writers`.
    private SystemClipboard Clipboard(string platform, Dictionary<string, string?> answers, string? wayland = null, string? display = null, string[]? writers = null) =>
        new(platform, name => name switch
        {
            "WAYLAND_DISPLAY" => wayland,
            "DISPLAY" => display,
            _ => null,
        }, (file, args) =>
        {
            var command = string.Join(' ', [file, .. args]);
            _runs.Add(command);
            return answers.GetValueOrDefault(command);
        }, (file, args, input) =>
        {
            var command = string.Join(' ', [file, .. args]);
            _writes.Add((command, input));
            return writers?.Contains(command) == true;
        });

    [Fact]
    public void Wayland_reads_with_wl_paste()
    {
        var clipboard = Clipboard("linux", new() { ["wl-paste --no-newline"] = "SELECT 1;" }, wayland: "wayland-1", display: ":0");

        Assert.Equal("SELECT 1;", clipboard.Read());
        Assert.Equal(["wl-paste --no-newline"], _runs);
    }

    [Fact]
    public void Without_wl_paste_it_falls_back_to_the_X11_tools()
    {
        var clipboard = Clipboard("linux", new() { ["xsel --clipboard --output"] = "x" }, wayland: "wayland-1", display: ":0");

        Assert.Equal("x", clipboard.Read());
        Assert.Equal(["wl-paste --no-newline", "xclip -selection clipboard -o", "xsel --clipboard --output"], _runs);
    }

    [Fact]
    public void X11_skips_wl_paste()
    {
        Assert.Equal("x", Clipboard("linux", new() { ["xclip -selection clipboard -o"] = "x" }, display: ":0").Read());
        Assert.Equal(["xclip -selection clipboard -o"], _runs);
    }

    [Fact]
    public void macOS_reads_with_pbpaste()
    {
        Assert.Equal("x", Clipboard("macos", new() { ["pbpaste"] = "x" }).Read());
    }

    [Fact]
    public void Nothing_to_read_is_null()
    {
        Assert.Null(Clipboard("linux", [], wayland: "wayland-1").Read());
        Assert.Null(Clipboard("windows", []).Read());
        Assert.Null(Clipboard("linux", []).Read());
    }

    [Fact]
    public void Wayland_writes_with_wl_copy()
    {
        var clipboard = Clipboard("linux", [], wayland: "wayland-1", display: ":0", writers: ["wl-copy"]);

        Assert.True(clipboard.Write("id,name\r\n1,Ana\r\n"));
        Assert.Equal([("wl-copy", "id,name\r\n1,Ana\r\n")], _writes);
    }

    [Fact]
    public void Without_wl_copy_it_writes_with_the_X11_tools()
    {
        var clipboard = Clipboard("linux", [], wayland: "wayland-1", display: ":0", writers: ["xsel --clipboard --input"]);

        Assert.True(clipboard.Write("x"));
        Assert.Equal(["wl-copy", "xclip -selection clipboard", "xsel --clipboard --input"], _writes.Select(write => write.Command));
    }

    [Fact]
    public void macOS_writes_with_pbcopy()
    {
        Assert.True(Clipboard("macos", [], writers: ["pbcopy"]).Write("x"));
    }

    [Fact]
    public void Nothing_to_write_with_is_false()
    {
        Assert.False(Clipboard("linux", [], wayland: "wayland-1").Write("x"));
        Assert.False(Clipboard("windows", [], writers: ["clip"]).Write("x"));
        Assert.False(Clipboard("linux", []).Write("x"));
    }
}
