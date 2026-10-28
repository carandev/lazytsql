using LazyTsql.Cli;
using LazyTsql.Core;
using LazyTsql.Infrastructure;
using Terminal.Gui.App;

namespace LazyTsql.Tui;

public static class TuiApplication
{
    public static int Run(IDatabaseConnector connector, FileConnectionStore store, string? initialConnection)
    {
        // Resolve before the terminal switches to the alternate screen so errors stay readable.
        var connectionString = initialConnection is null && Environment.GetEnvironmentVariable(ConnectionResolver.EnvironmentVariable) is null
            ? null
            : ConnectionResolver.Resolve(initialConnection, store, Environment.GetEnvironmentVariable);

        using var app = Application.Create();
        app.Init();
        // ANSI palette indices instead of fixed RGB, so the UI follows the terminal's color scheme.
        if (app.Driver is { } driver) driver.Force16Colors = true;
        using var view = new MainView(connector, store);
        if (connectionString is not null) view.ConnectOnStart(connectionString, initialConnection ?? ConnectionResolver.EnvironmentVariable);
        app.Run(view);
        return 0;
    }
}
