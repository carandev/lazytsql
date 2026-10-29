using LazyTsql.Infrastructure;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public sealed class FolderStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-folders-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private FolderStore Store() => new(Path.Combine(_directory, "config", "folders.json"));

    [Fact]
    public void Folders_are_kept_in_the_order_they_were_added_without_duplicates()
    {
        var store = Store();
        var scripts = Path.Combine(_directory, "scripts");
        var reports = Path.Combine(_directory, "reports");

        Assert.Empty(store.Load());
        Assert.True(store.Add(scripts));
        Assert.True(store.Add(reports + Path.DirectorySeparatorChar));
        Assert.False(store.Add(Path.Combine(_directory, ".", "scripts")));

        Assert.Equal([scripts, reports], Store().Load());
        Assert.Contains("scripts", File.ReadAllText(Path.Combine(_directory, "config", "folders.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_is_removed_by_its_path()
    {
        var store = Store();
        var scripts = Path.Combine(_directory, "scripts");
        store.Add(scripts);

        Assert.True(store.Remove(scripts + Path.DirectorySeparatorChar));
        Assert.False(store.Remove(scripts));
        Assert.Empty(Store().Load());
    }

    [Fact]
    public void A_saved_folder_that_no_longer_exists_says_so()
    {
        Assert.Equal("gone/ (not found)", FileNode.Directory(Path.Combine(_directory, "gone")).Label);
        Assert.Equal($"{Path.GetFileName(_directory)}/", FileNode.Directory(_directory).Label);
    }

    [Fact]
    public void A_search_match_belongs_to_the_folder_that_holds_it()
    {
        var a = FileNode.Directory(Path.Combine(_directory, "a"));
        var ab = FileNode.Directory(Path.Combine(_directory, "ab"));
        var match = new FileNode(Path.Combine(_directory, "ab", "x", "q.sql"), IsDirectory: false);

        Assert.Equal(ab, FileSearch.RootOf([a, ab], match));
    }
}
