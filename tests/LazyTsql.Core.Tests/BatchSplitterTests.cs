using LazyTsql.Core.Batches;

namespace LazyTsql.Core.Tests;

public class BatchSplitterTests
{
    [Fact]
    public void Script_without_go_is_a_single_batch()
    {
        var batches = BatchSplitter.Split("SELECT 1;\nSELECT 2;");

        var batch = Assert.Single(batches);
        Assert.Equal("SELECT 1;\nSELECT 2;", batch.Text);
        Assert.Equal(1, batch.RepeatCount);
        Assert.Equal(1, batch.StartLine);
    }

    [Fact]
    public void Go_on_its_own_line_separates_batches_case_insensitively()
    {
        var batches = BatchSplitter.Split("CREATE TABLE t (id int)\ngo\nSELECT * FROM t\n  GO  \n");

        Assert.Equal(["CREATE TABLE t (id int)", "SELECT * FROM t"], batches.Select(b => b.Text));
        Assert.Equal([1, 3], batches.Select(b => b.StartLine));
    }

    [Fact]
    public void Go_with_count_sets_repeat_count()
    {
        var batch = Assert.Single(BatchSplitter.Split("INSERT INTO t VALUES (1)\nGO 5"));

        Assert.Equal(5, batch.RepeatCount);
    }

    [Fact]
    public void Go_inside_block_comment_or_string_is_not_a_separator()
    {
        var sql = "/* start\nGO\nend */\nSELECT 'a\nGO\nb'";

        var batch = Assert.Single(BatchSplitter.Split(sql));

        Assert.Equal(sql, batch.Text);
    }

    [Fact]
    public void Go_after_line_comment_is_still_a_separator()
    {
        var batches = BatchSplitter.Split("SELECT 1 -- it's a comment\nGO\nSELECT 2");

        Assert.Equal(["SELECT 1 -- it's a comment", "SELECT 2"], batches.Select(b => b.Text));
    }

    [Fact]
    public void Words_starting_with_go_are_not_separators()
    {
        var batch = Assert.Single(BatchSplitter.Split("SELECT 1\nGOTO label"));

        Assert.Equal("SELECT 1\nGOTO label", batch.Text);
    }

    [Fact]
    public void Empty_batches_are_dropped()
    {
        var batches = BatchSplitter.Split("GO\n\nGO\nSELECT 1\nGO\n   \n");

        Assert.Equal(["SELECT 1"], batches.Select(b => b.Text));
    }

    [Fact]
    public void Windows_line_endings_are_supported()
    {
        var batches = BatchSplitter.Split("SELECT 1\r\nGO\r\nSELECT 2\r\n");

        Assert.Equal(["SELECT 1", "SELECT 2"], batches.Select(b => b.Text));
    }
}
