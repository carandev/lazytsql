using System.Collections.ObjectModel;
using LazyTsql.Core;
using LazyTsql.Core.Completion;
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
/// lazygit-style layout: numbered panels on the left (connections, files, schema), editor and results on the right,
/// a message log at the bottom and context key hints in the status bar.
/// </summary>
public sealed class MainView : Runnable
{
    private const int ConnectionsPanel = 0, FilesPanel = 1, SchemaPanel = 2, QueryPanel = 3, ResultsPanel = 4, MessagesPanel = 5;

    private readonly IDatabaseConnector _connector;
    private readonly IBacpacTool _bacpac;
    // sqlpackage opens its own connection, so it needs the connection string, not the session.
    private string? _connectionString;
    private readonly FileConnectionStore _store;
    private readonly ActivityLog _activity;
    private bool _activityFailed;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly SchemaCatalog _catalog = new();
    private readonly ObservableCollection<string> _connectionNames = [];
    private readonly FrameView[] _panels;
    private readonly ListView _connections;
    private readonly TreeView<FileNode> _files;
    private readonly TreeView<SchemaNode> _schema;
    private readonly SqlEditor _editor;
    private readonly TableView _results;
    private readonly TextView _messages;
    private readonly Label _hints;

    private IReadOnlyList<ConnectionProfile> _profiles = [];
    private IDatabaseSession? _session;
    private string? _sessionName;
    private CancellationTokenSource? _queryCancellation;
    private List<ShownResult> _resultSets = [];
    private int _maxRows = new QueryOptions().MaxRows;
    private int _resultIndex;
    private bool _quitConfirmed;
    private ScriptFile? _script;
    private string _savedText = string.Empty;
    private FileNode _filesRoot = FileNode.Directory(Environment.CurrentDirectory);

    public MainView(IDatabaseConnector connector, IBacpacTool bacpac, FileConnectionStore store, ActivityLog activity)
    {
        _activity = activity;
        _connector = connector;
        _bacpac = bacpac;
        _store = store;

        _connections = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };
        _connections.SetSource(_connectionNames);

        _files = new TreeView<FileNode>
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowLetterBasedNavigation = false,
            Style = new TreeStyle { ShowBranchLines = false, ExpandableSymbol = new System.Text.Rune('▸'), CollapseableSymbol = new System.Text.Rune('▾') },
            TreeBuilder = new DelegateTreeBuilder<FileNode>(node => node.Children(), node => node.IsDirectory),
        };

        _schema = new TreeView<SchemaNode>
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowLetterBasedNavigation = false,
            Style = new TreeStyle { ShowBranchLines = false, ExpandableSymbol = new System.Text.Rune('▸'), CollapseableSymbol = new System.Text.Rune('▾') },
            TreeBuilder = new DelegateTreeBuilder<SchemaNode>(GetChildren, node => node.CanExpand),
        };

        _editor = new SqlEditor { Width = Dim.Fill(), Height = Dim.Fill(), Text = "SELECT @@VERSION;" };
        _results = new TableView { Width = Dim.Fill(), Height = Dim.Fill(), FullRowSelect = false };
        _results.Style.ExpandLastColumn = false;
        _results.Style.ShowHorizontalHeaderOverline = false;
        _results.Style.ShowHorizontalBottomLine = false;
        _results.Style.HeaderScheme = Theme.TableHeader;
        _messages = new TextView { Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true };

        var connectionsPanel = Panel("[1] Connections", _connections, 0, 0, Dim.Percent(28), Dim.Percent(20));
        var filesPanel = Panel("[2] Files", _files, 0, Pos.Bottom(connectionsPanel), Dim.Percent(28), Dim.Percent(30));
        var schemaPanel = Panel("[3] Schema", _schema, 0, Pos.Bottom(filesPanel), Dim.Percent(28), Dim.Fill(1));
        var editorPanel = Panel("[4] Query", _editor, Pos.Right(connectionsPanel), 0, Dim.Fill(), Dim.Percent(38));
        var messagesPanel = Panel("[6] Messages", _messages, Pos.Right(schemaPanel), Pos.AnchorEnd(8), Dim.Fill(), 7);
        var resultsPanel = Panel("[5] Results", _results, Pos.Right(schemaPanel), Pos.Bottom(editorPanel), Dim.Fill(), Dim.Fill(8));
        _panels = [connectionsPanel, filesPanel, schemaPanel, editorPanel, resultsPanel, messagesPanel];

        _hints = new Label { X = 1, Y = Pos.AnchorEnd(1), Width = Dim.Fill(1), Height = 1 };
        _hints.SetScheme(Theme.Hints);
        foreach (var panel in _panels) panel.HasFocusChanged += (_, _) => UpdateHints();
        _editor.ContentsChanged += (_, _) => UpdateEditorTitle();
        _editor.EnableCompletion(() => _session is { } session ? (_catalog, session.CurrentDatabase) : null);
        _editor.Autocomplete.Scheme = Theme.Popup;
        _catalog.ObjectsNeeded += LoadCatalogObjects;
        _catalog.ColumnsNeeded += LoadCatalogColumns;
        _catalog.ForeignKeysNeeded += LoadCatalogForeignKeys;

        SetScheme(Theme.Content);
        Add(connectionsPanel, filesPanel, schemaPanel, editorPanel, resultsPanel, messagesPanel, _hints);
        ShowFiles(Environment.CurrentDirectory);
        Initialized += (_, _) =>
        {
            App!.Keyboard.KeyDown += OnGlobalKeyDown;
            // The footer follows dialogs too; update after the session stack has changed.
            App.SessionBegun += (_, _) => OnUi(UpdateHints);
            App.SessionEnded += (_, _) => OnUi(UpdateHints);
            ReloadConnections();
            UpdateHints();
        };
    }

    public void OpenScript(ScriptFile script, string text)
    {
        _script = script;
        _editor.Text = text;
        _savedText = _editor.Text;
        UpdateEditorTitle();
    }

    /// <summary>Shows a folder's scripts in the files panel, the folder itself expanded at the top.</summary>
    public void ShowFiles(string directory)
    {
        _filesRoot = FileNode.Directory(directory);
        _files.ClearObjects();
        _files.AddObject(_filesRoot);
        _files.Expand(_filesRoot);
    }

    /// <summary>Runs the update check off the UI thread once the screen is up; a notice goes to the messages panel.</summary>
    public void CheckForUpdatesOnStart(Func<CancellationToken, Task<string?>> check) =>
        Initialized += (_, _) => _ = Task.Run(async () =>
        {
            if (await check(CancellationToken.None) is not { } notice) return;
            OnUi(() =>
            {
                Log(notice);
                _panels[MessagesPanel].Title = "[6] Messages · update available";
            });
        });

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

    /// <summary>Every way out (q, Ctrl+Q, the Esc quit key) asks first.</summary>
    protected override bool OnIsRunningChanging(bool oldIsRunning, bool newIsRunning)
    {
        if (newIsRunning || _quitConfirmed) return base.OnIsRunningChanging(oldIsRunning, newIsRunning);
        // Ask after the stop request unwinds instead of nesting a modal inside it.
        OnUi(Quit);
        return true;
    }

    private void Quit()
    {
        var dirty = IsScriptDirty;
        var question = dirty ? $"Quit lazytsql?\nUnsaved changes in {_script!.Name} will be lost." : "Quit lazytsql?";
        if (_quitConfirmed || !ConfirmDialog.Show(App!, "Quit", question, "Quit", confirmIsDefault: !dirty)) return;
        _quitConfirmed = true;
        App?.RequestStop(this);
    }

    private static FrameView Panel(string title, View content, Pos x, Pos y, Dim width, Dim height)
    {
        var frame = new FrameView { Title = title, X = x, Y = y, Width = width, Height = height, BorderStyle = LineStyle.Rounded, HotKeySpecifier = Theme.NoHotKey };
        frame.Add(content);
        Theme.StylePanel(frame, content);
        return frame;
    }

    private void OnGlobalKeyDown(object? sender, Key key)
    {
        // Dialogs own the keyboard while they are open; only help is answered for them here.
        if (App?.TopRunnableView is View { Data: KeyContext dialog } top && top != this)
        {
            var typing = top.MostFocused is TextField or TextView;
            if (dialog != KeyContext.Help && (key == Key.F1 || (key.AsRune.Value == '?' && !typing)))
            {
                key.Handled = true;
                HelpDialog.Show(App, dialog);
            }

            return;
        }

        if (App?.TopRunnableView != this) return;

        var inEditor = _editor.HasFocus;
        var handled = true;

        if (key == Key.F5 || key == Key.E.WithCtrl) RunQuery();
        else if (key == Key.K.WithCtrl) CancelQuery();
        else if (key == Key.F1) HelpDialog.Show(App!, PanelContext());
        else if (key == Key.S.WithCtrl) SaveScript();
        else if (key == Key.O.WithCtrl) PromptOpenScript();
        else if (key == Key.Q.WithCtrl) Quit();
        // Esc first closes the completion popup; the editor handles that.
        else if (key == Key.Esc && inEditor && !_editor.Autocomplete.Visible) FocusPanel(ResultsPanel);
        else if (key.IsAlt && PanelIndex(key.NoAlt) is { } altPanel) FocusPanel(altPanel);
        else if (!inEditor && VimMotion(key) is { } motion) _panels[CurrentPanel()].SubViews.First().NewKeyDownEvent(motion);
        else if (inEditor || _messages.HasFocus && key != Key.Tab) handled = false;
        else if (PanelIndex(key) is { } panel) FocusPanel(panel);
        else if (key == Key.Q) Quit();
        else if (key == Key.Tab) FocusPanel((CurrentPanel() + 1) % _panels.Length);
        else if (key.AsRune.Value == '?') HelpDialog.Show(App!, PanelContext());
        else if (_connections.HasFocus && key == Key.Enter) ConnectSelected();
        else if (_connections.HasFocus && key == Key.A) EditConnection(null);
        else if (_connections.HasFocus && key == Key.E) EditConnection(SelectedProfile());
        else if (_connections.HasFocus && key == Key.D) DeleteConnection();
        else if (_files.HasFocus && key == Key.Enter) OpenSelectedFile();
        else if (_files.HasFocus && key == Key.R) ReloadFiles();
        else if (_schema.HasFocus && key == Key.S) SelectTopFromTable();
        else if (_schema.HasFocus && key == Key.E) OpenDefinition();
        else if (_schema.HasFocus && key == Key.R) ReloadSchema();
        else if (_schema.HasFocus && key == Key.X) ExportBacpac();
        else if (_schema.HasFocus && key == Key.I) ImportBacpac();
        else if (_schema.HasFocus && key == Key.Enter) ToggleSchemaNode();
        else if (_results.HasFocus && key.AsRune.Value == ']') ShowResultSet(_resultIndex + 1);
        else if (_results.HasFocus && key.AsRune.Value == '[') ShowResultSet(_resultIndex - 1);
        else if (_results.HasFocus && key == Key.X) ExportResultSet();
        else if (_results.HasFocus && key == Key.M) ChangeRowLimit();
        else handled = false;

        key.Handled = handled;
    }

    /// <summary>Vim motions translated to the arrow keys each view already binds, so behavior matches the arrows exactly.</summary>
    private static Key? VimMotion(Key key)
    {
        if (key == Key.D.WithCtrl) return Key.PageDown;
        if (key == Key.U.WithCtrl) return Key.PageUp;
        if (key.IsCtrl || key.IsAlt) return null;
        return key.AsRune.Value switch
        {
            'j' => Key.CursorDown,
            'k' => Key.CursorUp,
            'h' => Key.CursorLeft,
            'l' => Key.CursorRight,
            'g' => Key.Home,
            'G' => Key.End,
            _ => null,
        };
    }

    /// <summary>Keys for the focused panel, or for the open dialog, like lazygit's bottom line.</summary>
    private void UpdateHints() =>
        _hints.Text = KeyHelp.Footer(App?.TopRunnableView is View { Data: KeyContext dialog } top && top != this ? dialog : PanelContext());

    private KeyContext PanelContext() => CurrentPanel() switch
    {
        ConnectionsPanel => KeyContext.Connections,
        FilesPanel => KeyContext.Files,
        SchemaPanel => KeyContext.Schema,
        QueryPanel => KeyContext.Query,
        ResultsPanel => KeyContext.Results,
        _ => KeyContext.Messages,
    };

    private static int? PanelIndex(Key key) =>
        key.AsRune.Value is >= '1' and <= '6' and var digit ? digit - '1' : null;

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
        if (_profiles.Count == 0) Log("No saved connections. Focus [1] and press 'a' to add one.");
    }

    private ConnectionProfile? SelectedProfile()
    {
        var index = _connections.SelectedItem ?? 0;
        return index < _profiles.Count ? _profiles[index] : null;
    }

    private void SelectProfile(string name)
    {
        var index = _profiles.ToList().FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) _connections.SelectedItem = index;
    }

    private void EditConnection(ConnectionProfile? existing)
    {
        if (ConnectionDialog.Show(App!, existing) is not { } profile) return;
        try
        {
            _store.Upsert(profile, replacing: existing?.Name);
            ReloadConnections();
            SelectProfile(profile.Name);
            Log(existing is null ? $"Saved connection '{profile.Name}'. Press Enter to connect." : $"Updated connection '{profile.Name}'.");
        }
        catch (IOException ex)
        {
            Log($"✗ Could not save {_store.Path}: {ex.Message}");
        }
    }

    private void DeleteConnection()
    {
        if (SelectedProfile() is not { } profile) return;
        if (!ConfirmDialog.Show(App!, "Delete connection", $"Delete '{profile.Name}'?", "Delete", confirmIsDefault: false)) return;
        try
        {
            _store.Remove(profile.Name);
            ReloadConnections();
            Log($"Deleted connection '{profile.Name}'.");
        }
        catch (IOException ex)
        {
            Log($"✗ Could not save {_store.Path}: {ex.Message}");
        }
    }

    private void ConnectSelected()
    {
        if (SelectedProfile() is not { } profile) return;
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
            var databases = await SchemaDatabasesAsync(session, token);
            var previous = _session;
            _session = session;
            _sessionName = displayName;
            _connectionString = connectionString;
            if (previous is not null) await previous.DisposeAsync();

            OnUi(() =>
            {
                ResetCatalog(databases);
                ShowDatabases(databases, session);
                UpdateEditorTitle();
                Log($"Connected to {displayName} (SQL Server {session.ServerVersion}), database {session.CurrentDatabase}.");
                FocusPanel(SchemaPanel);
            });
        });
    }

    /// <summary>A connection scoped to a database shows only that one; otherwise every accessible database.</summary>
    private static async Task<IReadOnlyList<string>> SchemaDatabasesAsync(IDatabaseSession session, CancellationToken token) =>
        session.ConfiguredDatabase is { } database ? [database] : await session.GetDatabasesAsync(token);

    private void ShowDatabases(IReadOnlyList<string> databases, IDatabaseSession session)
    {
        _schema.ClearObjects();
        _schema.AddObjects(databases.Select(name => (SchemaNode)new DatabaseNode(name)));
        if (session.ConfiguredDatabase is not null && _schema.Objects?.FirstOrDefault() is { } only) _schema.Expand(only);
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
                    .ContinueWith(_ => session.GetColumnsAsync(table.Database, table.Value, token), token).Unwrap())
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
        var objects = await session.GetObjectsAsync(database, token);
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

    private void ResetCatalog(IReadOnlyList<string> databases)
    {
        _catalog.Clear();
        _catalog.Databases = databases;
    }

    /// <summary>Loads completion names in the background; suggestions refresh when they arrive.</summary>
    private void LoadCatalogObjects(string database)
    {
        var session = _session;
        if (session is null) return;
        RunInBackground(async token =>
        {
            IReadOnlyList<DbObject> objects = [.. await session.GetObjectsAsync(database, token), .. await session.GetSynonymsAsync(database, token)];
            OnUi(() =>
            {
                // A reconnect while loading starts a new catalog; these names belong to the old connection.
                if (_session != session) return;
                _catalog.SetObjects(database, objects);
                _editor.RefreshCompletion();
            });
        }, onError: () => _catalog.SetObjects(database, []));
    }

    private void LoadCatalogColumns(string database, DbObject table)
    {
        var session = _session;
        if (session is null) return;
        RunInBackground(async token =>
        {
            var columns = await session.GetColumnsAsync(database, table, token);
            OnUi(() =>
            {
                if (_session != session) return;
                _catalog.SetColumns(database, table, columns);
                _editor.RefreshCompletion();
            });
        }, onError: () => _catalog.SetColumns(database, table, []));
    }

    private void LoadCatalogForeignKeys(string database)
    {
        var session = _session;
        if (session is null) return;
        RunInBackground(async token =>
        {
            var foreignKeys = await session.GetForeignKeysAsync(database, token);
            OnUi(() =>
            {
                if (_session != session) return;
                _catalog.SetForeignKeys(database, foreignKeys);
                _editor.RefreshCompletion();
            });
        }, onError: () => _catalog.SetForeignKeys(database, []));
    }

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
            var session = RequireSession();
            var databases = await SchemaDatabasesAsync(session, token);
            OnUi(() =>
            {
                ResetCatalog(databases);
                ShowDatabases(databases, session);
                Log($"Schema reloaded: {databases.Count} databases.");
            });
        });
    }

    private void SelectTopFromTable()
    {
        if (_schema.SelectedObject is not ObjectNode { HasColumns: true } table) return;
        if (ReplaceEditorText($"SELECT TOP (100) * FROM [{table.Database}].{table.Value.QualifiedName};")) RunQuery();
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
                if (ReplaceEditorText(definition.Trim())) FocusPanel(QueryPanel);
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
        ReleaseResults();
        _panels[ResultsPanel].Title = "[5] Results · running…";
        WriteActivity($"Running on {_sessionName} › {_session.CurrentDatabase}:\n{script}");
        Log("Running…");

        RunInBackground(async _ =>
        {
            var session = RequireSession();
            var resultSets = new List<ShownResult>();
            var total = TimeSpan.Zero;
            await foreach (var result in ScriptRunner.RunAsync(session, script, new QueryOptions(_maxRows), cancellation.Token))
            {
                resultSets.AddRange(result.ResultSets.Select((set, i) => new ShownResult(set, session, result.Batch, i)));
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
        }, cancellation.Token, onError: () => _panels[ResultsPanel].Title = "[5] Results");
    }

    private void CancelQuery()
    {
        if (_queryCancellation is { IsCancellationRequested: false } cancellation)
        {
            cancellation.Cancel();
            Log("Cancel requested.");
        }
    }

    /// <summary>
    /// Drops the shown results before a run, so the old and the new rows are never held together. After a large result
    /// the heap is compacted, which gives its memory back to the system instead of keeping it for the next run.
    /// </summary>
    private void ReleaseResults()
    {
        var rows = _resultSets.Sum(shown => (long)shown.Set.Rows.Count);
        _resultSets = [];
        ShowResultSet(0);
        if (rows >= 100_000) GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private void ShowResultSet(int index)
    {
        if (_resultSets.Count == 0)
        {
            _results.Table = null;
            _panels[ResultsPanel].Title = "[5] Results";
            return;
        }

        _resultIndex = Math.Clamp(index, 0, _resultSets.Count - 1);
        var resultSet = _resultSets[_resultIndex].Set;
        _results.Table = new ResultSetTableSource(resultSet);
        var count = resultSet.Rows.Count;
        var rows = resultSet.IsTruncated ? $"first {count} rows" : count == 1 ? "1 row" : $"{count} rows";
        var position = _resultSets.Count > 1 ? $" {_resultIndex + 1}/{_resultSets.Count} [ ]" : string.Empty;
        _panels[ResultsPanel].Title = $"[5] Results{position} · {rows}";
    }

    private void ExportResultSet()
    {
        if (_resultSets.Count == 0)
        {
            Log("Nothing to export. Run a query first.");
            return;
        }

        var shown = _resultSets[_resultIndex];
        if (shown.Set.IsTruncated && shown.Session != _session)
        {
            Log("✗ The connection changed since this result was fetched. Run the query again to export every row.");
            return;
        }

        // The grid holds only the fetched rows: every row means running the batch again, which repeats its side effects.
        if (shown.Set.IsTruncated && !ConfirmDialog.Show(
                App!,
                "Export every row",
                $"The grid holds the first {shown.Set.Rows.Count} rows. Exporting every row\nruns the batch at line {shown.Batch.StartLine} again.",
                "Run again",
                confirmIsDefault: false))
        {
            return;
        }

        var suggested = Path.Combine(Environment.CurrentDirectory, $"results-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        if (FilePicker.Show(App!, "Export CSV", "Export", suggested, ".csv") is not { } path) return;

        if (!shown.Set.IsTruncated)
        {
            try
            {
                using (var writer = CsvFile(path)) Output.CsvResultWriter.Write(writer, shown.Set);
                Log($"Exported {shown.Set.Rows.Count} rows to {path}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log($"✗ Export failed: {ex.Message}");
            }

            return;
        }

        _queryCancellation?.Dispose();
        _queryCancellation = new CancellationTokenSource();
        Log("Exporting every row… (Ctrl+K cancels)");
        RunInBackground(async token =>
        {
            try
            {
                long count;
                await using (var writer = CsvFile(path))
                {
                    count = await shown.Session.StreamResultSetAsync(shown.Batch, shown.Index, new Output.CsvRowWriter(writer), new QueryOptions(), token);
                }

                OnUi(() => Log($"Exported {count} rows to {path}."));
            }
            catch (Exception ex)
            {
                // A partial file would look like a complete export.
                try { File.Delete(path); } catch (Exception ex2) when (ex2 is IOException or UnauthorizedAccessException) { }
                // Cancelling stops the command on the server, which SqlClient may report as an error.
                if (ex is OperationCanceledException || token.IsCancellationRequested) throw new OperationCanceledException(token);
                OnUi(() => Log($"✗ Export failed: {ex.Message}"));
            }
        }, _queryCancellation.Token);
    }

    /// <summary>Exports the database of the selected schema node (or the current one) with sqlpackage.</summary>
    private void ExportBacpac()
    {
        if (_session is not { } session || _connectionString is not { } connectionString)
        {
            Log("Not connected. Pick a connection in [1] and press Enter.");
            return;
        }

        var database = _schema.SelectedObject switch
        {
            DatabaseNode node => node.Name,
            ObjectNode node => node.Database,
            _ => session.CurrentDatabase,
        };
        var suggested = Path.Combine(Environment.CurrentDirectory, $"{database}-{DateTime.Now:yyyyMMdd-HHmmss}.bacpac");
        if (FilePicker.Show(App!, $"Export {database}", "Export", suggested, ".bacpac") is not { } path) return;

        RunSqlPackage($"Exporting {database} to {path}…", $"Exported {database} to {path}.", token =>
            _bacpac.ExportAsync(connectionString, database, path, line => OnUi(() => Log(line)), token), onCancel: null);
    }

    /// <summary>Imports a .bacpac as a new database with sqlpackage, then reloads the schema.</summary>
    private void ImportBacpac()
    {
        if (_session is null || _connectionString is not { } connectionString)
        {
            Log("Not connected. Pick a connection in [1] and press Enter.");
            return;
        }

        if (FilePicker.Show(App!, "Import .bacpac", "Import", Environment.CurrentDirectory + Path.DirectorySeparatorChar, ".bacpac") is not { } path) return;
        if (!File.Exists(path))
        {
            Log($"✗ File not found: {path}");
            return;
        }

        if (NameDialog.Show(App!, "Import .bacpac", "New database name (it must not exist or be empty):", NameDialog.DefaultDatabaseName(path)) is not { } database) return;

        RunSqlPackage($"Importing {path} as {database}…", $"Imported {path} as {database}.", async token =>
        {
            await _bacpac.ImportAsync(connectionString, database, path, line => OnUi(() => Log(line)), token);
            OnUi(ReloadSchema);
        }, onCancel: $"The database {database} may exist partially imported.");
    }

    /// <summary>Runs sqlpackage in the background with its output in Messages; Ctrl+K cancels it.</summary>
    private void RunSqlPackage(string starting, string done, Func<CancellationToken, Task> run, string? onCancel)
    {
        _queryCancellation?.Dispose();
        _queryCancellation = new CancellationTokenSource();
        Log($"{starting} (Ctrl+K cancels)");
        RunInBackground(async token =>
        {
            try
            {
                await run(token);
                OnUi(() => Log(done));
            }
            catch (BacpacException ex)
            {
                OnUi(() => Log($"✗ {ex.Message}"));
            }
            catch (OperationCanceledException) when (onCancel is not null)
            {
                OnUi(() => Log(onCancel));
                throw;
            }
        }, _queryCancellation.Token);
    }

    // The BOM lets Excel detect UTF-8, so accents and ñ survive opening the file.
    private static StreamWriter CsvFile(string path) =>
        new(path, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private void ChangeRowLimit()
    {
        if (RowLimitDialog.Show(App!, _maxRows) is not { } limit) return;
        _maxRows = limit;
        Log(limit == 0 ? "Row limit: every row. Applies to the next run." : $"Row limit: {limit} rows. Applies to the next run.");
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

    private void UpdateEditorTitle()
    {
        var file = _script is null ? string.Empty : $" · {_script.Name}{(IsScriptDirty ? "*" : string.Empty)}";
        var connection = _session is null ? string.Empty : $" · {_sessionName} › {_session.CurrentDatabase}";
        _panels[QueryPanel].Title = $"[4] Query{file}{connection}";
    }

    private bool IsScriptDirty => _script is not null && _editor.Text != _savedText;

    private bool ConfirmDiscardScript() =>
        !IsScriptDirty || ConfirmDialog.Show(App!, "Unsaved changes", $"Discard unsaved changes in {_script!.Name}?", "Discard", confirmIsDefault: false);

    /// <summary>Generated SQL replaces the editor and detaches it from the open file, which stays as saved.</summary>
    private bool ReplaceEditorText(string text)
    {
        if (!ConfirmDiscardScript()) return false;
        _script = null;
        _editor.Text = text;
        UpdateEditorTitle();
        return true;
    }

    private void PromptOpenScript()
    {
        if (!ConfirmDiscardScript()) return;
        var directory = _script is null ? Environment.CurrentDirectory : Path.GetDirectoryName(_script.Path)!;
        if (FilePicker.Show(App!, "Open script", "Open", directory + Path.DirectorySeparatorChar, ".sql") is not { } path) return;
        OpenScriptFile(path);
    }

    /// <summary>Enter on a script opens it; on a folder it expands or collapses.</summary>
    private void OpenSelectedFile()
    {
        if (_files.SelectedObject is not { } node) return;
        if (node.IsDirectory)
        {
            if (_files.IsExpanded(node)) _files.Collapse(node);
            else _files.Expand(node);
        }
        else if (ConfirmDiscardScript())
        {
            OpenScriptFile(node.Path);
        }
    }

    private void ReloadFiles()
    {
        _files.RebuildTree();
        _files.SetNeedsDraw();
    }

    private void OpenScriptFile(string path)
    {
        try
        {
            var (script, text) = ScriptFile.Open(path);
            OpenScript(script, text);
            Log(File.Exists(script.Path) ? $"Opened {script.Path}." : $"New file {script.Path}; Ctrl+S creates it.");
            FocusPanel(QueryPanel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log($"✗ Could not open {path}: {ex.Message}");
        }
    }

    /// <summary>Saves the open script, asking for a path the first time.</summary>
    private void SaveScript()
    {
        var script = _script;
        try
        {
            if (script is null)
            {
                var suggested = Path.Combine(Environment.CurrentDirectory, "query.sql");
                if (FilePicker.Show(App!, "Save script", "Save", suggested, ".sql") is not { } path) return;
                script = ScriptFile.Open(path).Script;
                if (File.Exists(script.Path) && !ConfirmDialog.Show(App!, "Save script", $"Overwrite {script.Name}?", "Overwrite", confirmIsDefault: false)) return;
            }

            var text = _editor.Text;
            script.Save(text);
            _script = script;
            _savedText = text;
            UpdateEditorTitle();
            Log($"Saved {script.Path}.");
            // A script saved as a new file shows up in the files panel.
            ReloadFiles();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log($"✗ Could not save: {ex.Message}");
        }
    }

    private IDatabaseSession RequireSession() =>
        _session ?? throw new InvalidOperationException("Not connected.");

    /// <summary>Every message also goes to the activity log on disk.</summary>
    private void Log(string text)
    {
        WriteActivity(text);
        _messages.Text = string.IsNullOrEmpty(_messages.Text) ? text : $"{_messages.Text}\n{text}";
        _messages.MoveEnd();
    }

    /// <summary>A log that cannot be written is reported once; the work goes on.</summary>
    private void WriteActivity(string text)
    {
        if (_activity.Write("tui", text) || _activityFailed) return;
        _activityFailed = true;
        Log($"Could not write the activity log in {_activity.Directory}.");
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

    /// <summary>A result set with the session and batch that produced it, so an export can fetch every row again.</summary>
    private sealed record ShownResult(ResultSet Set, IDatabaseSession Session, Core.Batches.SqlBatch Batch, int Index);
}
