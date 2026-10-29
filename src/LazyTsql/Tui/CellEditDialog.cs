using LazyTsql.Core.Editing;
using LazyTsql.Output;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Asks for a cell's new value, typed as text and checked against the column's type before it is kept.</summary>
public static class CellEditDialog
{
    /// <summary>The text a value is edited as: what the grid shows, but empty for NULL.</summary>
    public static string Text(object? value) => value is null ? string.Empty : ValueFormatter.Format(value);

    /// <summary>Returns false when cancelled; otherwise <paramref name="value"/> is the new value, null for NULL.</summary>
    public static bool Show(IApplication app, string column, string typeName, object? current, out object? value)
    {
        var accepted = false;
        object? result = null;
        var dialog = new Dialog { Title = $"Edit {column}", Width = Dim.Percent(60), Height = 11, BorderStyle = LineStyle.Rounded, Data = KeyContext.CellEdit, HotKeySpecifier = Theme.NoHotKey };
        var hint = new Label { Text = $"{typeName} · Ctrl+N sets NULL", X = 1, Y = 1, Width = Dim.Fill(2), HotKeySpecifier = Theme.NoHotKey };
        var field = new TextField { Text = Text(current), X = 1, Y = 3, Width = Dim.Fill(2) };
        var status = new Label { Text = string.Empty, X = 1, Y = 4, Width = Dim.Fill(2), HotKeySpecifier = Theme.NoHotKey };

        void Close(object? value)
        {
            (accepted, result) = (true, value);
            app.RequestStop(dialog);
        }

        void Accept()
        {
            if (SqlValue.TryParse(field.Text, typeName, out var parsed, out var error)) Close(parsed);
            else status.Text = $"✗ {error}";
        }

        field.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        field.TextChanged += (_, _) => status.Text = string.Empty;
        field.KeyDown += (_, key) =>
        {
            if (key != Key.N.WithCtrl) return;
            key.Handled = true;
            Close(null);
        };

        var set = new Button { Text = "Set", IsDefault = true, HotKeySpecifier = Theme.NoHotKey };
        var setNull = new Button { Text = "NULL", HotKeySpecifier = Theme.NoHotKey };
        var cancel = new Button { Text = "Cancel", HotKeySpecifier = Theme.NoHotKey };
        set.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        setNull.Accepting += (_, e) =>
        {
            e.Handled = true;
            Close(null);
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        dialog.Add(hint, field, status);
        dialog.AddButton(cancel);
        dialog.AddButton(setNull);
        dialog.AddButton(set);
        Theme.StyleDialog(dialog);
        Theme.StyleButtons(cancel, setNull, set);
        hint.SetScheme(Theme.Hints);
        status.SetScheme(Theme.Hints);
        dialog.Initialized += (_, _) =>
        {
            field.SetFocus();
            field.MoveEnd();
        };
        app.Run(dialog);
        dialog.Dispose();
        value = result;
        return accepted;
    }
}
