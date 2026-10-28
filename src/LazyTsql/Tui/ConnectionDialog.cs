using LazyTsql.Core.Connections;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Add or edit a saved connection without leaving the TUI.</summary>
public static class ConnectionDialog
{
    public static ConnectionProfile? Show(IApplication app, ConnectionProfile? existing)
    {
        ConnectionProfile? result = null;
        var dialog = new Dialog
        {
            Title = existing is null ? "New connection" : $"Edit {existing.Name}",
            Width = Dim.Percent(80),
            Height = 13,
            BorderStyle = LineStyle.Rounded,
        };

        var nameLabel = new Label { Text = "Name:", X = 1, Y = 1 };
        var name = new TextField { Text = existing?.Name ?? string.Empty, X = 20, Y = 1, Width = Dim.Fill(2) };
        var connectionLabel = new Label { Text = "Connection string:", X = 1, Y = 3 };
        var connection = new TextField { Text = existing?.ConnectionString ?? string.Empty, X = 20, Y = 3, Width = Dim.Fill(2) };
        var hint = new Label
        {
            Text = "Example: Server=localhost,1433;Database=app;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true\n"
                + "Use ${VAR} to keep secrets in environment variables instead of the config file.",
            X = 1,
            Y = 5,
            // '_' marks hotkeys by default and would hide the underscore in ${DB_PASSWORD}.
            HotKeySpecifier = new System.Text.Rune(0xFFFF),
        };
        var error = new Label { Text = string.Empty, X = 1, Y = 8 };

        var save = new Button { Text = "Save", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            var trimmedName = name.Text.Trim();
            var trimmedConnection = connection.Text.Trim();
            if (trimmedName.Length == 0 || trimmedConnection.Length == 0)
            {
                error.Text = "Name and connection string are required.";
                return;
            }

            result = new ConnectionProfile(trimmedName, trimmedConnection);
            app.RequestStop(dialog);
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        dialog.Add(nameLabel, name, connectionLabel, connection, hint, error);
        dialog.AddButton(cancel);
        dialog.AddButton(save);
        app.Run(dialog);
        dialog.Dispose();
        return result;
    }
}
