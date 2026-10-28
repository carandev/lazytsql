using System.Collections.ObjectModel;
using LazyTsql.Core;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Infrastructure;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using LazyTsql.Core.Connections;

// TextView is marked obsolete in favor of tui-cs/Editor; kept until that editor is evaluated for SQL highlighting.
#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// lazygit-style layout: numbered panels on the left (connections, schema), editor and results on the right,
/// a message log at the bottom and context key hints in the status bar.
/// </summary>
public sealed class MainView : Runnable
{
    private const string HelpText = """
        Global                         Schema [2]
          1-5 / Alt+1-5  focus panel     Enter  expand / collapse
          Tab            next panel      s      SELECT TOP 100 from table
          F5 / Ctrl+E    run query       e      open definition in editor
          Ctrl+K         cancel query    r      reload schema  
          ?              help
          q / Ctrl+Q     quit          Results [4]
                                         [ ]    previous / next result set
        Connections [1]
          Enter          connect       Query [3]
                                         Esc    leave editor
        Selected text runs alone; otherwise the whole script runs, batch by batch (GO).
        """;

    private readonly IDatabaseConnector _connector;
    private readonly FileConnectionStore _store;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly ObservableCollection<string> _connectionNames = [];
    private readonly FrameView[] _panels;
    private readonly ListView _connections;
    private readonly TreeView<SchemaNode> _schema;
    private readonly TextView _editor;
    private readonly TableView _results;
    private readonly TextView _messages;

    private IReadOnlyList<ConnectionProfile> _profiles = [];
    private IDatabaseSession? _session;
    private string? _sessionName;
    private CancellationTokenSource? _queryCancellation;
    private List<ResultSet> _resultSets = [];
    private int _resultIndex;

    public MainView(IDatabaseConnector connector, FileConnectionStore store)
    {
        _connector = connector;
        _store = store;

        _connections = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };
        _connections.SetSource(_connectionNames);

        _schema = new TreeView<SchemaNode>
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowLetterBasedNavigation = false,
            TreeBuilder = new DelegateTreeBuilder<SchemaNode>(GetChildren, node => node.CanExpand),
        };

        _editor = new TextView { Width = Dim.Fill(), Height = Dim.Fill(), Text = "SELECT @@VERSION;" };
        _results = new TableView { Width = Dim.Fill(), Height = Dim.Fill(), FullRowSelect = false };
        _results.Style.ExpandLastColumn = false;
        _results.Style.ShowHorizontalHeaderOverline = false;
        _results.Style.ShowHorizontalBottomLine = false;
        _messages = new TextView { Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true };

        var connectionsPanel = Panel("[1] Connections", _connections, 0, 0, Dim.Percent(28), Dim.Percent(25));
        var schemaPanel = Panel("[2] Schema", _schema, 0, Pos.Bottom(connectionsPanel), Dim.Percent(28), Dim.Fill(1));
        var editorPanel = Panel("[3] Query", _editor, Pos.Right(connectionsPanel), 0, Dim.Fill(), Dim.Percent(38));
        var messagesPanel = Panel("[5] Messages", _messages, Pos.Right(schemaPanel), Pos.AnchorEnd(8), Dim.Fill(), 7);
        var resultsPanel = Panel("[4] Results", _results, Pos.Right(schemaPanel), Pos.Bottom(editorPanel), Dim.Fill(), Dim.Fill(8));
        _panels = [connectionsPanel, schemaPanel, editorPanel, resultsPanel, messagesPanel];

        var statusBar = new StatusBar(
        [
            new Shortcut(Key.F5, "Run", RunQuery),
            new Shortcut(Key.K.WithCtrl, "Cancel", CancelQuery),
            new Shortcut(Key.Tab, "Next panel", () => FocusPanel((CurrentPanel() + 1) % _panels.Length)),
            new Shortcut(Key.Empty, "1-5 Panels", () => { }),
            new Shortcut(Key.Empty, "? Help", ShowHelp),
            new Shortcut(Key.Q.WithCtrl, "Quit", () => App?.RequestStop()),
        ]);

        Add(connectionsPanel, schemaPanel, editorPanel, resultsPanel, messagesPanel, statusBar);
        Initialized += (_, _) =>
        {
            App!.Keyboard.KeyDown += OnGlobalKeyDown;
            ReloadConnections();
        };
    }

    public void ConnectOnStart(string connectionString, string displayName) =>
        Initialized += (_, _) => Connect(connectionString, displayName);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _queryCancellation?.Cancel();
            _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _sessionGate.Dispose();
        }

        base.Dispose(disposing);
    }

    private static FrameView Panel(string title, View content, Pos x, Pos y, Dim width, Dim height)
    {
        var frame = new FrameView { Title = title, X = x, Y = y, Width = width, Height = height, BorderStyle = LineStyle.Rounded };
        frame.Add(content);
        return frame;
    }

    private void OnGlobalKeyDown(object? sender, Key key)
    {
        var inEditor = _editor.HasFocus;
        var handled = true;

        if (key == Key.F5 || key == Key.E.WithCtrl) RunQuery();
        else if (key == Key.K.WithCtrl) CancelQuery();
        else if (key == Key.Q.WithCtrl) App?.RequestStop();
        else if (key == Key.Esc && inEditor) FocusPanel(3);
        else if (key.IsAlt && PanelIndex(key.NoAlt) is { } altPanel) FocusPanel(altPanel);
        else if (inEditor || _messages.HasFocus && key != Key.Tab) handled = false;
        else if (PanelIndex(key) is { } panel) FocusPanel(panel);
        else if (key == Key.Q) App?.RequestStop();
        else if (key == Key.Tab) FocusPanel((CurrentPanel() + 1) % _panels.Length);
        else if (key.AsRune.Value == '?') ShowHelp();
        else if (_connections.HasFocus && key == Key.Enter) ConnectSelected();
        else if (_schema.HasFocus && key == Key.S) SelectTopFromTable();
        else if (_schema.HasFocus && key == Key.E) OpenDefinition();
        else if (_schema.HasFocus && key == Key.R) ReloadSchema();
        else if (_schema.HasFocus && key == Key.Enter) ToggleSchemaNode();
        else if (_results.HasFocus && key.AsRune.Value == ']') ShowResultSet(_resultIndex + 1);
        else if (_results.HasFocus && key.AsRune.Value == '[') ShowResultSet(_resultIndex - 1);
        else handled = false;

        key.Handled = handled;
    }

    private static int? PanelIndex(Key key) =>
        key.AsRune.Value is >= '1' and <= '5' and var digit ? digit - '1' : null;

    private int CurrentPanel() => Math.Max(0, Array.FindIndex(_panels, p => p.HasFocus));

    private void FocusPanel(int index) => _panels[index].SubViews.First().SetFocus();

    private void ReloadConnections()
    {
        try
        {
            _profiles = _store.Load();
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Log($"Could not read {_store.Path}: {ex.Message}");
            _profiles = [];
        }

        _connectionNames.Clear();
        foreach (var profile in _profiles) _connectionNames.Add(profile.Name);
        if (_profiles.Count > 0) _connections.SelectedItem = 0;
        if (_profiles.Count == 0) Log($"No saved connections. Add one with: lazytsql connections add <name> \"<connection string>\"");
    }

    private void ConnectSelected()
    {
        var index = _connections.SelectedItem ?? 0;
        if (index >= _profiles.Count) return;
        var profile = _profiles[index];
        try
        {
            Connect(ConnectionStringResolver.Resolve(profile.ConnectionString, Environment.GetEnvironmentVariable), profile.Name);
        }
        catch (InvalidOperationException ex)
        {
            Log(ex.Message);
        }
    }

    private void Connect(string connectionString, string displayName)
    {
        Log($"Connecting to {displayName}…");
        RunInBackground(async token =>
        {
            var session = await _connector.OpenAsync(connectionString, token);
            var databases = await session.GetDatabasesAsync(token);
            var previous = _session;
            _session = session;
            _sessionName = displayName;
            if (previous is not null) await previous.DisposeAsync();

            OnUi(() =>
            {
                _schema.ClearObjects();
                _schema.AddObjects(databases.Select(name => (SchemaNode)new DatabaseNode(name)));
                UpdateEditorTitle();
                Log($"Connected to {displayName} (SQL Server {session.ServerVersion}), database {session.CurrentDatabase}.");
                FocusPanel(1);
            });
        });
    }

    private IEnumerable<SchemaNode> GetChildren(SchemaNode node)
    {
        if (node.Children is not null) return node.Children;
        if (!node.IsLoading)
        {
            node.IsLoading = true;
            LoadChildren(node);
        }

        return [new MessageNode("loading…")];
    }

    private void LoadChildren(SchemaNode node)
    {
        RunInBackground(async token =>
        {
            var session = RequireSession();
            List<SchemaNode> children = node switch
            {
                DatabaseNode database => await LoadDatabaseAsync(session, database.Name, token),
                ObjectNode table => [.. (await UseDatabaseAsync(session, table.Database, token)
                    .ContinueWith(_ => session.GetColumnsAsync(table.Value, token), token).Unwrap())
                    .Select(c => (SchemaNode)new ColumnNode(c))],
                _ => [],
            };

            if (children.Count == 0) children.Add(new MessageNode("(empty)"));
            OnUi(() =>
            {
                node.Children = children;
                node.IsLoading = false;
                _schema.RefreshObject(node);
                UpdateEditorTitle();
            });
        }, onError: () => node.IsLoading = false);
    }

    private static async Task<List<SchemaNode>> LoadDatabaseAsync(IDatabaseSession session, string database, CancellationToken token)
    {
        await UseDatabaseAsync(session, database, token);
        var objects = await session.GetObjectsAsync(token);
        return objects
            .GroupBy(o => o.Kind)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var folder = new FolderNode(g.Key, g.ToList());
                folder.Children = g.Select(o => (SchemaNode)new ObjectNode(database, o)).ToList();
                return (SchemaNode)folder;
            })
            .ToList();
    }

    private static Task UseDatabaseAsync(IDatabaseSession session, string database, CancellationToken token) =>
        session.CurrentDatabase == database ? Task.CompletedTask : session.ChangeDatabaseAsync(database, token);

    private void ToggleSchemaNode()
    {
        if (_schema.SelectedObject is not { CanExpand: true } node) return;
        if (_schema.IsExpanded(node)) _schema.Collapse(node);
        else _schema.Expand(node);
    }

    private void ReloadSchema()
    {
        if (_session is null) return;
        RunInBackground(async token =>
        {
            var databases = await RequireSession().GetDatabasesAsync(token);
            OnUi(() =>
            {
                _schema.ClearObjects();
                _schema.AddObjects(databases.Select(name => (SchemaNode)new DatabaseNode(name)));
                Log($"Schema reloaded: {databases.Count} databases.");
            });
        });
    }

    private void SelectTopFromTable()
    {
        if (_schema.SelectedObject is not ObjectNode { HasColumns: true } table) return;
        _editor.Text = $"SELECT TOP (100) * FROM [{table.Database}].{table.Value.QualifiedName};";
        RunQuery();
    }

    private void OpenDefinition()
    {
        if (_schema.SelectedObject is not ObjectNode { HasColumns: false } routine) return;
        RunInBackground(async token =>
        {
            var session = RequireSession();
            await UseDatabaseAsync(session, routine.Database, token);
            var definition = await session.GetDefinitionAsync(routine.Value, token);
            OnUi(() =>
            {
                _editor.Text = definition.Trim();
                UpdateEditorTitle();
                FocusPanel(2);
            });
        });
    }

    private void RunQuery()
    {
        if (_session is null)
        {
            Log("Not connected. Pick a connection in [1] and press Enter.");
            return;
        }

        var script = string.IsNullOrEmpty(_editor.SelectedText) ? _editor.Text : _editor.SelectedText;
        if (string.IsNullOrWhiteSpace(script)) return;

        _queryCancellation?.Dispose();
        _queryCancellation = new CancellationTokenSource();
        var cancellation = _queryCancellation;
        _panels[3].Title = "[4] Results · running…";
        Log("Running…");

        RunInBackground(async _ =>
        {
            var resultSets = new List<ResultSet>();
            var total = TimeSpan.Zero;
            await foreach (var result in ScriptRunner.RunAsync(RequireSession(), script, new QueryOptions(), cancellation.Token))
            {
                resultSets.AddRange(result.ResultSets);
                total += result.Elapsed;
                OnUi(() => LogBatch(result));
            }

            OnUi(() =>
            {
                _resultSets = resultSets;
                ShowResultSet(0);
                UpdateEditorTitle();
                Log($"Done in {total.TotalMilliseconds:0} ms.");
            });
        }, cancellation.Token, onError: () => _panels[3].Title = "[4] Results");
    }

    private void CancelQuery()
    {
        if (_queryCancellation is { IsCancellationRequested: false } cancellation)
        {
            cancellation.Cancel();
            Log("Cancel requested.");
        }
    }

    private void ShowResultSet(int index)
    {
        if (_resultSets.Count == 0)
        {
            _results.Table = null;
            _panels[3].Title = "[4] Results";
            return;
        }

        _resultIndex = Math.Clamp(index, 0, _resultSets.Count - 1);
        var resultSet = _resultSets[_resultIndex];
        _results.Table = new ResultSetTableSource(resultSet);
        var count = resultSet.Rows.Count;
        var rows = resultSet.IsTruncated ? $"first {count} rows" : count == 1 ? "1 row" : $"{count} rows";
        var position = _resultSets.Count > 1 ? $" {_resultIndex + 1}/{_resultSets.Count} [ ]" : string.Empty;
        _panels[3].Title = $"[4] Results{position} · {rows}";
    }

    private void LogBatch(BatchResult result)
    {
        foreach (var message in result.Messages)
        {
            var line = result.Batch.StartLine + Math.Max(message.LineNumber, 1) - 1;
            Log(message.IsError ? $"✗ Msg {message.Number}, Level {message.Severity}, Line {line}: {message.Text}" : message.Text);
        }

        if (result.RecordsAffected >= 0) Log($"({result.RecordsAffected} rows affected)");
    }

    private void ShowHelp()
    {
        var close = new Button { Text = "Close", IsDefault = true };
        var dialog = new Dialog { Title = "Keys", Width = 72, Height = 18, BorderStyle = LineStyle.Rounded };
        dialog.Add(new Label { Text = HelpText, X = 1, Y = 1 });
        dialog.AddButton(close);
        close.Accepting += (_, e) =>
        {
            e.Handled = true;
            App?.RequestStop(dialog);
        };
        App!.Run(dialog);
        dialog.Dispose();
    }

    private void UpdateEditorTitle() =>
        _panels[2].Title = _session is null ? "[3] Query" : $"[3] Query · {_sessionName} › {_session.CurrentDatabase}";

    private IDatabaseSession RequireSession() =>
        _session ?? throw new InvalidOperationException("Not connected.");

    private void Log(string text)
    {
        _messages.Text = string.IsNullOrEmpty(_messages.Text) ? text : $"{_messages.Text}\n{text}";
        _messages.MoveEnd();
    }

    private void OnUi(Action action) => App?.Invoke(action);

    /// <summary>Runs database work off the UI thread, one operation at a time since a session has a single connection.</summary>
    private void RunInBackground(Func<CancellationToken, Task> work, CancellationToken token = default, Action? onError = null)
    {
        _ = Task.Run(async () =>
        {
            await _sessionGate.WaitAsync(token);
            try
            {
                await work(token);
            }
            catch (Exception ex)
            {
                OnUi(() =>
                {
                    onError?.Invoke();
                    Log(ex is OperationCanceledException ? "Cancelled." : $"✗ {ex.Message}");
                });
            }
            finally
            {
                _sessionGate.Release();
            }
        }, CancellationToken.None);
    }
}
