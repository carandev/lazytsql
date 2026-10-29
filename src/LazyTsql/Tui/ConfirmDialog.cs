using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>
/// Yes/no question with the app theme. Each button shows its key: <c>y</c> confirms, <c>n</c> and Esc cancel, and the
/// arrows (or h/l) choose a button for Enter.
/// </summary>
public static class ConfirmDialog
{
    /// <summary>A button's label with the key that presses it, so y and n are never a guess.</summary>
    public static string ButtonText(string label, char key) => $"{label} ({key})";

    public static bool Show(IApplication app, string title, string text, string confirmLabel, bool confirmIsDefault)
    {
        var confirmed = false;
        var lines = text.Split('\n');
        var dialog = new Dialog
        {
            Title = title,
            Width = Math.Max(40, lines.Max(line => line.Length) + 6),
            Height = lines.Length + 7,
            BorderStyle = LineStyle.Rounded,
            Data = KeyContext.Confirm,
            HotKeySpecifier = Theme.NoHotKey,
        };
        var confirm = new Button { Text = ButtonText(confirmLabel, 'y'), IsDefault = confirmIsDefault, HotKeySpecifier = Theme.NoHotKey };
        var cancel = new Button { Text = ButtonText("Cancel", 'n'), IsDefault = !confirmIsDefault, HotKeySpecifier = Theme.NoHotKey };

        void Close(bool result)
        {
            confirmed = result;
            app.RequestStop(dialog);
        }

        confirm.Accepting += (_, e) =>
        {
            e.Handled = true;
            Close(true);
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            Close(false);
        };
        dialog.KeyDown += (_, key) =>
        {
            if (key == Key.Y) Close(true);
            else if (key == Key.N) Close(false);
            else if (key == Key.CursorLeft || key == Key.CursorRight || key == Key.H || key == Key.L) (confirm.HasFocus ? cancel : confirm).SetFocus();
            else return;
            key.Handled = true;
        };

        dialog.Add(new Label { Text = text, X = 1, Y = 1, HotKeySpecifier = Theme.NoHotKey });
        // The dialog marks its last button as the default, so the default goes last.
        dialog.AddButton(confirmIsDefault ? cancel : confirm);
        dialog.AddButton(confirmIsDefault ? confirm : cancel);
        Theme.StyleDialog(dialog);
        Theme.StyleButtons(confirm, cancel);
        // Focus the default button so Enter picks it.
        dialog.Initialized += (_, _) => (confirmIsDefault ? confirm : cancel).SetFocus();
        app.Run(dialog);
        dialog.Dispose();
        return confirmed;
    }
}
