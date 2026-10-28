using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public sealed class PathCompletionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-paths-").FullName;

    public PathCompletionTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "reports"));
        Directory.CreateDirectory(Path.Combine(_directory, "release"));
        foreach (var file in new[] { "query.sql", "Query2.SQL", "notes.txt", ".hidden.sql" })
            File.WriteAllText(Path.Combine(_directory, file), string.Empty);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string In(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Lists_directories_first_then_files_with_the_extension_skipping_hidden_ones()
    {
        var entries = PathCompletion.Matches(_directory + Path.DirectorySeparatorChar, ".sql");

        Assert.Equal(
            [new PathEntry("release", true), new PathEntry("reports", true), new PathEntry("query.sql", false), new PathEntry("Query2.SQL", false)],
            entries);
        Assert.Equal([new PathEntry(".hidden.sql", false)], PathCompletion.Matches(In("."), ".sql"));
        Assert.Contains(new PathEntry("notes.txt", false), PathCompletion.Matches(In("n"), extension: null));
    }

    [Fact]
    public void Tab_completes_a_unique_match_or_the_common_prefix()
    {
        Assert.Equal(In("reports") + Path.DirectorySeparatorChar, PathCompletion.Complete(In("rep"), ".sql"));
        Assert.Equal(In("re"), PathCompletion.Complete(In("re"), ".sql"));
        Assert.Equal(In("query"), PathCompletion.Complete(In("Q"), ".sql"));
        Assert.Equal(In("Query2.SQL"), PathCompletion.Complete(In("query2"), ".sql"));
        Assert.Equal(In("nothing"), PathCompletion.Complete(In("nothing"), ".sql"));
    }

    [Fact]
    public void A_missing_directory_has_no_matches_and_completes_to_itself()
    {
        var missing = Path.Combine(_directory, "nope", "x");

        Assert.Empty(PathCompletion.Matches(missing, ".sql"));
        Assert.Equal(missing, PathCompletion.Complete(missing, ".sql"));
    }

    [Fact]
    public void Home_is_expanded_and_relative_paths_become_absolute()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.Combine(home, "a", "b.sql"), PathCompletion.ExpandHome("~/a/../a/b.sql"));
        Assert.Equal(Path.GetFullPath("x.sql"), PathCompletion.ExpandHome("x.sql"));
    }
}
