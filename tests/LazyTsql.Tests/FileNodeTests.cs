using LazyTsql.Cli;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public sealed class FileNodeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-files-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Touch(string relative)
    {
        var path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    [Fact]
    public void A_directory_lists_its_folders_first_then_its_sql_scripts()
    {
        Touch("zeta.sql");
        Touch("Alpha.SQL");
        Touch("notes.txt");
        Touch("reports/sales.sql");
        Touch("archive/old.sql");
        Touch(".git/hooks.sql");

        var children = FileNode.Directory(_directory).Children();

        Assert.Equal(["archive/", "reports/", "Alpha.SQL", "zeta.sql"], children.Select(node => node.Label));
        Assert.Equal([true, true, false, false], children.Select(node => node.IsDirectory));
        Assert.Equal(["sales.sql"], children[1].Children().Select(node => node.Label));
        Assert.Empty(children[2].Children());
    }

    [Fact]
    public void An_unreadable_or_deleted_directory_has_no_children()
    {
        Assert.Empty(FileNode.Directory(Path.Combine(_directory, "gone")).Children());
    }

    [Fact]
    public void Nodes_for_the_same_path_are_equal_so_a_refresh_keeps_folders_expanded()
    {
        Touch("reports/sales.sql");

        Assert.Equal(FileNode.Directory(_directory).Children()[0], FileNode.Directory(_directory).Children()[0]);
    }

    [Fact]
    public void A_directory_instead_of_a_script_opens_in_the_files_panel()
    {
        Touch("reports/sales.sql");
        var reports = Path.Combine(_directory, "reports");

        Assert.Equal((null, reports), Paths(["-c", "dev", reports]));
        Assert.Equal((null, reports), Paths([reports]));
        Assert.Equal(("q.sql", null), Paths(["q.sql", "-c", "dev"]));
        Assert.Equal(("missing.sql", null), Paths(["-c", "dev", "missing.sql"]));
        Assert.Equal((null, null), Paths(["-c", "dev"]));

        static (string?, string?) Paths(string[] args)
        {
            var parsed = CliArguments.Parse(args);
            return (parsed.ScriptPath, parsed.DirectoryPath);
        }
    }
}
