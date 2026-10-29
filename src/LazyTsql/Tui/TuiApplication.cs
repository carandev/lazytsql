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
        using var view = new MainView(connector, bacpac, store, activity);
        // The files panel shows the folder given, else the opened script's folder, else the working directory.
        if ((directory ?? (script is { } inFolder ? Path.GetDirectoryName(inFolder.Script.Path) : null)) is { } folder) view.ShowFiles(folder);
        if (script is { } opened) view.OpenScript(opened.Script, opened.Text);
        if (connectionString is not null) view.ConnectOnStart(connectionString, ConnectionResolver.DisplayName(initialConnection, store));
        app.Run(view);
        return 0;
    }
}
