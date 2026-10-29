using LazyTsql.Core.Connections;
using LazyTsql.SqlServer;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>
/// Add or edit a saved connection, either as a raw connection string or as individual login fields.
/// Profiles always store a connection string; the fields mode builds and parses it.
/// </summary>
public static class ConnectionDialog
{
    private const int LabelWidth = 20;
    private const int StringMode = 0;
    private const int FieldsMode = 1;

    public static ConnectionProfile? Show(IApplication app, ConnectionProfile? existing)
    {
        ConnectionProfile? result = null;
        var parsed = existing is null ? null : SqlServerConnectionFields.TryParse(existing.ConnectionString);

        var dialog = new Dialog
        {
            Title = existing is null ? "New connection" : $"Edit {existing.Name}",
            Width = Dim.Percent(80),
            Height = 20,
            BorderStyle = LineStyle.Rounded,
            Data = KeyContext.ConnectionForm,
            HotKeySpecifier = Theme.NoHotKey,
        };

        var name = Field(dialog, "Name", 1, existing?.Name);
        var mode = new OptionSelector
        {
            X = LabelWidth,
            Y = 3,
            Orientation = Orientation.Horizontal,
            // One Tab stop per selector; arrow keys pick the option.
            TabBehavior = TabBehavior.NoStop,
            Labels = ["Connection string", "Fields"],
            Value = existing is null || parsed is not null ? FieldsMode : StringMode,
        };
        dialog.Add(new Label { Text = "Mode:", X = 1, Y = 3 }, mode);

        var stringPane = new View { X = 0, Y = 5, Width = Dim.Fill(), Height = 9, CanFocus = true };
        var connection = Field(stringPane, "Connection string", 0, existing?.ConnectionString);
        stringPane.Add(new Label
        {
            Text = "Example: Server=localhost,1433;Database=app;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true",
            X = 1,
            Y = 2,
            HotKeySpecifier = Theme.NoHotKey,
        });

        var fieldsPane = new View { X = 0, Y = 5, Width = Dim.Fill(), Height = 9, CanFocus = true };
        var server = Field(fieldsPane, "Server", 0, parsed?.Server ?? "localhost", width: Dim.Percent(50));
        var port = new TextField { Text = parsed?.Port ?? string.Empty, X = Pos.Right(server) + 8, Y = 0, Width = 8 };
        fieldsPane.Add(new Label { Text = "Port:", X = Pos.Right(server) + 2, Y = 0 }, port);
        var database = Field(fieldsPane, "Database", 1, parsed?.Database, width: Dim.Fill(17));
        fieldsPane.Add(new Label { Text = "(empty = all)", X = Pos.AnchorEnd(15), Y = 1 });
        var authentication = new OptionSelector
        {
            X = LabelWidth,
            Y = 2,
            Orientation = Orientation.Horizontal,
            TabBehavior = TabBehavior.NoStop,
            Labels = ["SQL Server login", "Integrated (Windows/Kerberos)"],
            Value = parsed?.Authentication == SqlServerAuthentication.Integrated ? 1 : 0,
        };
        fieldsPane.Add(new Label { Text = "Authentication:", X = 1, Y = 2 }, authentication);
        var user = Field(fieldsPane, "User", 3, parsed?.User);
        var password = Field(fieldsPane, "Password", 4, parsed?.Password);
        password.Secret = true;
        var encrypt = new CheckBox { Text = "Encrypt", X = LabelWidth, Y = 5, Value = parsed is null || parsed.Encrypt ? CheckState.Checked : CheckState.UnChecked };
        var trust = new CheckBox
        {
            Text = "Trust server certificate",
            X = Pos.Right(encrypt) + 3,
            Y = 5,
            Value = parsed?.TrustServerCertificate ?? true ? CheckState.Checked : CheckState.UnChecked,
        };
        fieldsPane.Add(encrypt, trust);
        fieldsPane.Add(new Label { Text = "Tip: type ${VAR} as the password to read it from an environment variable.", X = 1, Y = 7 });

        var error = new Label { Text = string.Empty, X = 1, Y = 15, HotKeySpecifier = Theme.NoHotKey };
        dialog.Add(stringPane, fieldsPane, error);

        SqlServerConnectionFields ReadFields() => new(
            server.Text,
            port.Text,
            database.Text,
            authentication.Value == 1 ? SqlServerAuthentication.Integrated : SqlServerAuthentication.SqlLogin,
            user.Text,
            password.Text,
            trust.Value == CheckState.Checked,
            encrypt.Value == CheckState.Checked);

        void ApplyMode()
        {
            stringPane.Visible = mode.Value == StringMode;
            fieldsPane.Visible = mode.Value == FieldsMode;
            user.Enabled = password.Enabled = authentication.Value != 1;
        }

        mode.ValueChanged += (_, _) =>
        {
            error.Text = string.Empty;
            if (mode.Value == StringMode)
            {
                if (!string.IsNullOrWhiteSpace(server.Text)) connection.Text = ReadFields().ToConnectionString();
            }
            else if (!string.IsNullOrWhiteSpace(connection.Text))
            {
                if (SqlServerConnectionFields.TryParse(connection.Text) is { } fields)
                {
                    server.Text = fields.Server;
                    port.Text = fields.Port;
                    database.Text = fields.Database;
                    authentication.Value = fields.Authentication == SqlServerAuthentication.Integrated ? 1 : 0;
                    user.Text = fields.User;
                    password.Text = fields.Password;
                    encrypt.Value = fields.Encrypt ? CheckState.Checked : CheckState.UnChecked;
                    trust.Value = fields.TrustServerCertificate ? CheckState.Checked : CheckState.UnChecked;
                }
                else
                {
                    error.Text = "This connection string has settings the form cannot show; keep editing it as text.";
                    mode.Value = StringMode;
                }
            }

            ApplyMode();
        };
        authentication.ValueChanged += (_, _) => ApplyMode();
        ApplyMode();

        var save = new Button { Text = "Save", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            var trimmedName = name.Text.Trim();
            var connectionString = mode.Value == FieldsMode ? BuildFromFields() : connection.Text.Trim();
            if (trimmedName.Length == 0 || string.IsNullOrEmpty(connectionString))
            {
                error.Text = mode.Value == FieldsMode ? "Name and server are required." : "Name and connection string are required.";
                return;
            }

            result = new ConnectionProfile(trimmedName, connectionString);
            app.RequestStop(dialog);
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        string? BuildFromFields() => string.IsNullOrWhiteSpace(server.Text) ? null : ReadFields().ToConnectionString();

        dialog.AddButton(cancel);
        dialog.AddButton(save);
        Theme.StyleDialog(dialog);
        app.Run(dialog);
        dialog.Dispose();
        return result;
    }

    private static TextField Field(View container, string label, int y, string? value, Dim? width = null)
    {
        var field = new TextField { Text = value ?? string.Empty, X = LabelWidth, Y = y, Width = width ?? Dim.Fill(2) };
        container.Add(new Label { Text = $"{label}:", X = 1, Y = y }, field);
        return field;
    }
}
