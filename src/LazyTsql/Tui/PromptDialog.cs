using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>Single-line input dialog; returns null when cancelled or left empty.</summary>
public static class PromptDialog
{
    public static string? Show(IApplication app, string title, string label, string initialValue)
    {
        string? result = null;
        var dialog = new Dialog { Title = title, Width = Dim.Percent(70), Height = 7, BorderStyle = LineStyle.Rounded };
        var caption = new Label { Text = label, X = 1, Y = 1 };
        var input = new TextField { Text = initialValue, X = Pos.Right(caption) + 1, Y = 1, Width = Dim.Fill(2) };
        var ok = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        ok.Accepting += (_, e) =>
        {
            e.Handled = true;
            result = string.IsNullOrWhiteSpace(input.Text) ? null : input.Text.Trim();
            app.RequestStop(dialog);
        };
        cancel.Accepting += (_, e) =>
        {
            e.Handled = true;
            app.RequestStop(dialog);
        };

        dialog.Add(caption, input);
        dialog.AddButton(cancel);
        dialog.AddButton(ok);
        app.Run(dialog);
        dialog.Dispose();
        return result;
    }
}
