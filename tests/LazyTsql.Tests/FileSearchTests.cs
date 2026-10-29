using LazyTsql.Tui;

namespace LazyTsql.Tests;

public sealed class FileSearchTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-search-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void Touch(string relative)
    {
        var path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    private string Relative(FileNode node) => Path.GetRelativePath(_directory, node.Path).Replace('\\', '/');

    [Fact]
    public void Finds_folders_and_scripts_at_any_depth_in_tree_order_ignoring_case()
    {
        Touch("orders.sql");
        Touch("reports/Monthly_Orders.sql");
        Touch("reports/sales.sql");
        Touch("old_orders/seed.sql");
        Touch(".git/orders.sql");

        var matches = FileSearch.Matches(FileNode.Directory(_directory), "ORDERS");

        Assert.Equal(["old_orders", "reports/Monthly_Orders.sql", "orders.sql"], matches.Select(Relative));
        Assert.Empty(FileSearch.Matches(FileNode.Directory(_directory), ""));
        Assert.Empty(FileSearch.Matches(FileNode.Directory(_directory), "nothing"));
    }

    [Fact]
    public void The_folders_to_expand_lead_from_the_root_to_a_match()
    {
        Touch("a/b/deep.sql");
        var root = FileNode.Directory(_directory);
        var match = Assert.Single(FileSearch.Matches(root, "deep"));

        Assert.Equal(["", "a", "a/b"], FileSearch.Ancestors(root, match).Select(Relative).Select(path => path == "." ? "" : path));
    }

    [Fact]
    public void Linked_folders_are_not_searched_so_a_link_loop_cannot_hang_it()
    {
        if (OperatingSystem.IsWindows()) return;
        Touch("real/orders.sql");
        Directory.CreateSymbolicLink(Path.Combine(_directory, "real", "loop"), _directory);
        Directory.CreateSymbolicLink(Path.Combine(_directory, "linked"), Path.Combine(_directory, "real"));

        var matches = FileSearch.Matches(FileNode.Directory(_directory), "orders");

        Assert.Equal(["real/orders.sql"], matches.Select(Relative));
    }

    [Fact]
    public void A_cancelled_search_stops()
    {
        Touch("orders.sql");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Empty(FileSearch.Matches(FileNode.Directory(_directory), "orders", cancellation.Token));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(2, 1, 0)]
    [InlineData(0, -1, 2)]
    [InlineData(-1, 1, 0)]
    public void Next_and_previous_wrap_around(int current, int step, int expected)
    {
        Assert.Equal(expected, FileSearch.Step(current, step, count: 3));
    }
}
