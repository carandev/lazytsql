using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Yes/no question with the app theme. <c>y</c> confirms; <c>n</c> and Esc cancel.</summary>
public static class ConfirmDialog
{
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
        };
        var confirm = new Button { Text = confirmLabel, IsDefault = confirmIsDefault };
        var cancel = new Button { Text = "Cancel", IsDefault = !confirmIsDefault };

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
            else return;
            key.Handled = true;
        };

        dialog.Add(new Label { Text = text, X = 1, Y = 1 });
        dialog.AddButton(cancel);
        dialog.AddButton(confirm);
        Theme.StyleDialog(dialog);
        // Focus the default button so Enter picks it.
        dialog.Initialized += (_, _) => (confirmIsDefault ? confirm : cancel).SetFocus();
        app.Run(dialog);
        dialog.Dispose();
        return confirmed;
    }
}
