using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Asks for the name of the database a .bacpac is imported into.</summary>
public static class NameDialog
{
    /// <summary>Returns the trimmed name, or null when cancelled.</summary>
    public static string? Show(IApplication app, string title, string prompt, string initial)
    {
        string? result = null;
        var dialog = new Dialog { Title = title, Width = 56, Height = 10, BorderStyle = LineStyle.Rounded, Data = KeyContext.Name, HotKeySpecifier = Theme.NoHotKey };
        var field = new TextField { Text = initial, X = 1, Y = 2, Width = Dim.Fill(2) };
        var status = new Label { Text = string.Empty, X = 1, Y = 3, Width = Dim.Fill(2), HotKeySpecifier = Theme.NoHotKey };

        void Accept()
        {
            if (Parse(field.Text) is { } name)
            {
                result = name;
                app.RequestStop(dialog);
            }
            else
            {
                status.Text = "✗ Type a name";
            }
        }

        field.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        field.TextChanged += (_, _) => status.Text = string.Empty;

        var accept = new Button { Text = "Import", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        accept.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        dialog.Add(new Label { Text = prompt, X = 1, Y = 1 }, field, status);
        dialog.AddButton(cancel);
        dialog.AddButton(accept);
        Theme.StyleDialog(dialog);
        status.SetScheme(Theme.Hints);
        dialog.Initialized += (_, _) =>
        {
            field.SetFocus();
            field.MoveEnd();
        };
        app.Run(dialog);
        dialog.Dispose();
        return result;
    }

    /// <summary>The trimmed name, or null when nothing is left.</summary>
    public static string? Parse(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static string DefaultDatabaseName(string file) => Path.GetFileNameWithoutExtension(file);
}
