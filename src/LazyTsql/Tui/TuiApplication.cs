using LazyTsql.Cli;
using LazyTsql.Core;
using LazyTsql.Infrastructure;
using Terminal.Gui.App;

namespace LazyTsql.Tui;

public static class TuiApplication
{
    public static int Run(IDatabaseConnector connector, IBacpacTool bacpac, FileConnectionStore store, ActivityLog activity, string? initialConnection, string? scriptPath, string? directory = null)
    {
        // Resolve before the terminal switches to the alternate screen so errors stay readable.
        var connectionString = initialConnection is null && Environment.GetEnvironmentVariable(ConnectionResolver.EnvironmentVariable) is null
            ? null
            : ConnectionResolver.Resolve(initialConnection, store, Environment.GetEnvironmentVariable);

        // A missing script opens empty; an unreadable one fails here, before the screen switches.
        (ScriptFile Script, string Text)? script = scriptPath is null ? null : ScriptFile.Open(scriptPath);
        using var app = Application.Create();
        app.Init();
        // ANSI palette indices instead of fixed RGB, so the UI follows the terminal's color scheme.
        if (app.Driver is { } driver) driver.Force16Colors = true;
        using var view = new MainView(connector, bacpac, store, activity, FolderStore.Default());
        // The files panel shows the saved folders; a folder given here is saved too, and a script's folder shows for
        // this session only.
        view.ShowSavedFolders();
        if (directory is not null) view.AddFolder(directory, save: true);
        else if (script is { } inFolder) view.AddFolder(Path.GetDirectoryName(inFolder.Script.Path)!, save: false);
        if (script is { } opened) view.OpenScript(opened.Script, opened.Text);
        view.CheckForUpdatesOnStart(token => UpdateCheck.Default(Environment.GetEnvironmentVariable)
            .MessageAsync(typeof(TuiApplication).Assembly.GetName().Version ?? new Version(0, 0, 0), Environment.ProcessPath, token));
        if (connectionString is not null) view.ConnectOnStart(connectionString, ConnectionResolver.DisplayName(initialConnection, store));
        app.Run(view);
        return 0;
    }
}
