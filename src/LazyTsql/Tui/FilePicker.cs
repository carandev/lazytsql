using System.Collections.ObjectModel;
using LazyTsql.Infrastructure;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace LazyTsql.Tui;

/// <summary>
/// Path input with Tab completion over a browsable list of the directory being typed, like a GUI file dialog:
/// typing filters the list, Enter on a folder opens it and Enter on a file picks it.
/// </summary>
public static class FilePicker
{
    /// <summary>Returns the chosen absolute path, or null when cancelled. A name without extension gets <paramref name="extension"/>.</summary>
    public static string? Show(IApplication app, string title, string acceptLabel, string initialPath, string extension) =>
        Show(app, title, acceptLabel, initialPath, extension, folders: false, otherLabel: null).Path;

    /// <summary>
    /// Like <see cref="Show(IApplication, string, string, string, string)"/>, with a second action beside the accept
    /// button (Alt+C), such as copying instead of saving: <see cref="Choice.Other"/> is true when it was chosen, with no path.
    /// </summary>
    public static Choice ShowWithOther(IApplication app, string title, string acceptLabel, string otherLabel, string initialPath, string extension) =>
        Show(app, title, acceptLabel, initialPath, extension, folders: false, otherLabel);

    /// <summary>
    /// Picks a folder: the list shows only folders, Enter on one browses into it, and Enter on the path (or the accept
    /// button) chooses the folder typed there. Returns its absolute path, or null when cancelled.
    /// </summary>
    public static string? ShowFolder(IApplication app, string title, string acceptLabel, string initialPath) =>
        Show(app, title, acceptLabel, initialPath, extension: string.Empty, folders: true, otherLabel: null).Path;

    /// <summary>What the picker ended with: a path, the other action, or neither when cancelled.</summary>
    public readonly record struct Choice(string? Path, bool Other);

    private static Choice Show(IApplication app, string title, string acceptLabel, string initialPath, string extension, bool folders, string? otherLabel)
    {
        string? result = null;
        var other = false;
        var separator = Path.DirectorySeparatorChar;
        var dialog = new Dialog { Title = title, Width = Dim.Percent(70), Height = Dim.Percent(70), BorderStyle = LineStyle.Rounded, Data = KeyContext.FilePicker, HotKeySpecifier = Theme.NoHotKey };
        var field = new TextField { Text = Display(initialPath), X = 7, Y = 1, Width = Dim.Fill(2) };
        var names = new ObservableCollection<string>();
        var entries = new List<PathEntry?>();
        // Typing in the path field filters; letters in the list are vim keys, not ListView's search-as-you-type.
        var list = new ListView { X = 1, Y = 3, Width = Dim.Fill(2), Height = Dim.Fill(2), KeystrokeNavigator = null };
        list.SetSource(names);
        // Keys are in the app footer; this line only reports errors.
        var status = new Label { Text = string.Empty, X = 1, Y = Pos.AnchorEnd(1), Width = Dim.Fill(2), HotKeySpecifier = Theme.NoHotKey };

        void Refresh()
        {
            names.Clear();
            entries.Clear();
            if (CurrentDirectory() is { Parent: not null })
            {
                names.Add("..");
                entries.Add(null);
            }

            foreach (var entry in PathCompletion.Matches(field.Text, extension))
            {
                if (folders && !entry.IsDirectory) continue;
                names.Add(entry.IsDirectory ? entry.Name + separator : entry.Name);
                entries.Add(entry);
            }

            if (names.Count > 0) list.SelectedItem = 0;
        }

        DirectoryInfo? CurrentDirectory()
        {
            try
            {
                var directoryPart = PathCompletion.DirectoryPart(field.Text);
                var directory = new DirectoryInfo(PathCompletion.ExpandHome(directoryPart.Length == 0 ? "." : directoryPart));
                return directory.Exists ? directory : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        void SetPath(string text)
        {
            field.Text = text;
            field.MoveEnd();
        }

        // Focusing a TextField selects its text; put the cursor at the end so typing extends the path.
        void FocusField()
        {
            field.SetFocus();
            field.MoveEnd();
        }

        void Up()
        {
            if (CurrentDirectory()?.Parent is { } parent) SetPath(Display(parent.FullName).TrimEnd(separator) + separator);
        }

        void Activate()
        {
            if (list.SelectedItem is not { } index || index >= entries.Count) return;
            if (entries[index] is not { } entry)
            {
                Up();
                return;
            }

            SetPath(PathCompletion.DirectoryPart(field.Text) + entry.Name + (entry.IsDirectory ? separator : string.Empty));
            if (entry.IsDirectory) list.SetFocus();
            else Accept();
        }

        void Accept()
        {
            var text = field.Text.Trim();
            if (text.Length == 0) return;
            try
            {
                var path = PathCompletion.ExpandHome(text);
                if (folders)
                {
                    if (!Directory.Exists(path))
                    {
                        status.Text = $"✗ Not a folder: {text}";
                        return;
                    }

                    result = Path.GetFullPath(path);
                    app.RequestStop(dialog);
                    return;
                }

                if (Directory.Exists(path))
                {
                    SetPath(text.TrimEnd(separator) + separator);
                    return;
                }

                result = Path.HasExtension(path) ? path : path + extension;
                app.RequestStop(dialog);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                status.Text = $"✗ {ex.Message}";
            }
        }

        field.TextChanged += (_, _) =>
        {
            status.Text = string.Empty;
            Refresh();
        };
        field.KeyDown += (_, key) =>
        {
            if (key == Key.Tab) SetPath(PathCompletion.Complete(field.Text, extension));
            else if (key == Key.CursorDown && names.Count > 0) list.SetFocus();
            else return;
            key.Handled = true;
        };
        field.Accepting += (_, e) =>
        {
            e.Handled = true;
            Accept();
        };
        list.KeyDown += (_, key) =>
        {
            var rune = key.AsRune.Value;
            if (key == Key.Enter || rune == 'l') Activate();
            else if (key == Key.Backspace || rune is 'h' or '-') Up();
            else if (rune == 'j') list.NewKeyDownEvent(Key.CursorDown);
            else if ((rune == 'k' || key == Key.CursorUp) && list.SelectedItem is null or 0) FocusField();
            else if (rune == 'k') list.NewKeyDownEvent(Key.CursorUp);
            else return;
            key.Handled = true;
        };

        var accept = new Button { Text = acceptLabel, IsDefault = true };
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

        dialog.Add(new Label { Text = "Path:", X = 1, Y = 1 }, field, list, status);
        dialog.AddButton(cancel);
        if (otherLabel is not null)
        {
            var otherButton = new Button { Text = $"{otherLabel} (Alt+C)", HotKeySpecifier = Theme.NoHotKey };
            void ChooseOther()
            {
                other = true;
                app.RequestStop(dialog);
            }

            otherButton.Accepting += (_, e) =>
            {
                e.Handled = true;
                ChooseOther();
            };
            dialog.KeyDown += (_, key) =>
            {
                if (key != Key.C.WithAlt) return;
                key.Handled = true;
                ChooseOther();
            };
            dialog.AddButton(otherButton);
            Theme.StyleButtons(otherButton);
        }

        dialog.AddButton(accept);
        Theme.StyleDialog(dialog);
        Theme.StyleButtons(cancel, accept);
        status.SetScheme(Theme.Hints);
        Refresh();
        dialog.Initialized += (_, _) => FocusField();
        KeySequenceGuard.Run(app, dialog);
        dialog.Dispose();
        return new Choice(other ? null : result, other);
    }

    /// <summary>Paths under the home directory are shown with <c>~</c>.</summary>
    private static string Display(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "~" + path[home.Length..] : path;
    }
}
