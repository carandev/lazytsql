using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

// TextView is marked obsolete in favor of tui-cs/Editor; here it is only a scrollable read-only text area.
#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>Keys of the current panel or dialog; <c>a</c> switches to every key in the system and back.</summary>
public static class HelpDialog
{
    public static void Show(IApplication app, KeyContext context)
    {
        KeyContext? shown = context;
        while (ShowOnce(app, shown)) shown = shown is null ? context : null;
    }

    /// <summary>Returns true when the user asked to switch between the context and every key.</summary>
    private static bool ShowOnce(IApplication app, KeyContext? context)
    {
        var lines = KeyHelp.Render(context);
        var toggle = false;
        var dialog = new Dialog
        {
            Title = context is null ? "Keys · all" : $"Keys · {lines[0]}",
            Width = lines.Max(line => line.Length) + 6,
            // Taller than the screen scrolls inside instead of clipping.
            Height = Math.Min(lines.Count + 4, Math.Max(8, app.Screen.Height - 2)),
            BorderStyle = LineStyle.Rounded,
            Data = KeyContext.Help,
        };
        var text = new TextView
        {
            Text = string.Join('\n', context is null ? lines : lines.Skip(1)),
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = false,
        };

        void OnKeyDown(object? sender, Key key)
        {
            if (app.TopRunnableView != dialog) return;
            var rune = key.AsRune.Value;
            if (rune is 'j' or 'k')
            {
                key.Handled = true;
                text.NewKeyDownEvent(rune == 'j' ? Key.CursorDown : Key.CursorUp);
                return;
            }

            if (rune == 'a') toggle = true;
            else if (!(rune is 'q' or '?' || key == Key.Enter || key == Key.F1)) return;
            key.Handled = true;
            app.RequestStop(dialog);
        }

        dialog.Add(text);
        Theme.StyleDialog(dialog);
        app.Keyboard.KeyDown += OnKeyDown;
        try
        {
            KeySequenceGuard.Run(app, dialog);
        }
        finally
        {
            app.Keyboard.KeyDown -= OnKeyDown;
            dialog.Dispose();
        }

        return toggle;
    }
}
