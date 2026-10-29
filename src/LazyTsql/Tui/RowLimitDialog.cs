using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Asks how many rows each result set fetches; 0 means every row.</summary>
public static class RowLimitDialog
{
    /// <summary>Returns the new limit, or null when cancelled.</summary>
    public static int? Show(IApplication app, int current)
    {
        int? result = null;
        var dialog = new Dialog { Title = "Row limit", Width = 56, Height = 11, BorderStyle = LineStyle.Rounded, Data = KeyContext.RowLimit };
        var field = new TextField { Text = current == 0 ? "all" : current.ToString(), X = 1, Y = 3, Width = Dim.Fill(2) };
        var status = new Label { Text = string.Empty, X = 1, Y = 4, Width = Dim.Fill(2) };

        void Accept()
        {
            if (Parse(field.Text) is { } limit)
            {
                result = limit;
                app.RequestStop(dialog);
            }
            else
            {
                status.Text = "✗ Type a whole number, or all";
            }
        }

        field.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        field.TextChanged += (_, _) => status.Text = string.Empty;

        var apply = new Button { Text = "Apply", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        apply.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        dialog.Add(new Label { Text = "Rows to fetch per result set (a number, or all).\nApplies to the next run.", X = 1, Y = 1 }, field, status);
        dialog.AddButton(cancel);
        dialog.AddButton(apply);
        Theme.StyleDialog(dialog);
        Theme.StyleButtons(cancel, apply);
        status.SetScheme(Theme.Hints);
        dialog.Initialized += (_, _) =>
        {
            field.SetFocus();
            field.MoveEnd();
        };
        KeySequenceGuard.Run(app, dialog);
        dialog.Dispose();
        return result;
    }

    /// <summary><c>all</c>, empty or 0 mean every row (0); a positive whole number is the limit; anything else is null.</summary>
    public static int? Parse(string text)
    {
        var value = text.Trim();
        if (value.Length == 0 || value.Equals("all", StringComparison.OrdinalIgnoreCase)) return 0;
        return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var limit) ? limit : null;
    }
}
