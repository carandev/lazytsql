using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;

namespace LazyTsql.Core.Tests;

public class ResultEditsTests
{
    // SELECT c.name, o.note, c.id, o.oid, UPPER(c.name) AS up FROM dbo.customers c JOIN sales.orders o ...
    private static ResultSet Joined() => new(
        [new("name", "nvarchar"), new("note", "nvarchar"), new("id", "int"), new("oid", "int"), new("up", "nvarchar")],
        [["Ana", "first", 1, 10, "ANA"], ["Ana", "second", 1, 11, "ANA"], ["Bo", null, 2, 12, "BO"]],
        IsTruncated: false);

    private static IReadOnlyList<SourceColumn> JoinedSource() =>
    [
        new("name", false, null, "dbo", "customers", "name", false, true, "nvarchar(20)"),
        new("note", false, null, "sales", "orders", "note", false, true, "nvarchar(10)"),
        new("id", false, null, "dbo", "customers", "id", true, false, "int"),
        new("oid", false, null, "sales", "orders", "oid", true, true, "int"),
        new("up", false, null, null, null, null, false, false, "nvarchar(20)"),
    ];

    [Fact]
    public void Columns_read_from_a_keyed_table_are_editable_and_the_rest_say_why_not()
    {
        var edits = ResultEdits.Create(Joined(), JoinedSource(), "shop");

        Assert.Null(edits.ReadOnlyReason(0));
        Assert.Null(edits.ReadOnlyReason(1));
        Assert.Null(edits.ReadOnlyReason(3));
        Assert.Equal("id cannot be changed: it is an identity, computed or rowversion column.", edits.ReadOnlyReason(2));
        Assert.Equal("up is computed by the query, not read from a table.", edits.ReadOnlyReason(4));
    }

    [Fact]
    public void A_table_whose_key_is_not_in_the_result_is_read_only()
    {
        var set = new ResultSet([new("name", "nvarchar")], [["Ana"]], false);
        var edits = ResultEdits.Create(set, [
            new("name", false, null, "dbo", "customers", "name", false, true, "nvarchar(20)"),
            new("id", true, null, "dbo", "customers", "id", true, false, "int"),
        ], "shop");

        Assert.Equal("name cannot be changed: add [dbo].[customers]'s key column id to the query.", edits.ReadOnlyReason(0));
    }

    [Fact]
    public void A_table_the_query_reads_twice_is_read_only_because_its_rows_cannot_be_told_apart()
    {
        // SELECT e.id, e.name, b.id AS bid, b.name AS bname FROM dbo.emp e JOIN dbo.emp b ON b.id = e.boss
        var set = new ResultSet([new("id", "int"), new("name", "nvarchar"), new("bid", "int"), new("bname", "nvarchar")], [[2, "Ann", 1, "Boss"]], false);
        var edits = ResultEdits.Create(set, [
            new("id", false, null, "dbo", "emp", "id", true, true, "int"),
            new("name", false, null, "dbo", "emp", "name", false, true, "nvarchar(20)"),
            new("bid", false, null, "dbo", "emp", "id", true, true, "int"),
            new("bname", false, null, "dbo", "emp", "name", false, true, "nvarchar(20)"),
        ], "shop");

        Assert.All([1, 3], column => Assert.Equal(
            $"{set.Columns[column].Name} cannot be changed: the query reads [dbo].[emp] more than once, so a cell's row cannot be told apart.",
            edits.ReadOnlyReason(column)));
    }

    [Fact]
    public void A_table_read_again_through_a_hidden_key_is_read_only()
    {
        // SELECT t.id, x.name FROM dbo.t CROSS APPLY (SELECT TOP 1 name FROM dbo.t t2 ...) x
        var set = new ResultSet([new("id", "int"), new("name", "nvarchar")], [[1, "b"]], false);
        var edits = ResultEdits.Create(set, [
            new("id", false, null, "dbo", "t", "id", true, true, "int"),
            new("name", false, null, "dbo", "t", "name", false, true, "nvarchar(20)"),
            new("id", true, null, "dbo", "t", "id", true, true, "int"),
        ], "shop");

        Assert.Equal("name cannot be changed: the query reads [dbo].[t] more than once, so a cell's row cannot be told apart.", edits.ReadOnlyReason(1));
    }

    [Fact]
    public void A_table_without_a_key_is_read_only()
    {
        var set = new ResultSet([new("a", "int")], [[1]], false);
        var edits = ResultEdits.Create(set, [new("a", false, null, "dbo", "log", "a", false, true, "int")], "shop");

        Assert.Equal("a cannot be changed: [dbo].[log] has no primary key or unique key.", edits.ReadOnlyReason(0));
    }

    [Fact]
    public void A_type_without_a_literal_is_read_only()
    {
        var set = new ResultSet([new("id", "int"), new("place", "geography")], [[1, null]], false);
        var edits = ResultEdits.Create(set, [
            new("id", false, null, "dbo", "sites", "id", true, true, "int"),
            new("place", false, null, "dbo", "sites", "place", false, true, "geography"),
        ], "shop");

        Assert.Equal("place cannot be changed here: lazytsql cannot write geography values.", edits.ReadOnlyReason(1));
    }

    [Fact]
    public void Columns_that_no_longer_match_the_result_make_it_read_only()
    {
        var edits = ResultEdits.Create(Joined(), JoinedSource().Take(4).ToList(), "shop");

        Assert.Equal("The query's columns changed since it ran. Run it again to edit.", edits.ReadOnlyReason(0));
    }

    [Fact]
    public void Pending_values_show_instead_of_the_original_until_reverted()
    {
        var edits = ResultEdits.Create(Joined(), JoinedSource(), "shop");

        edits.Set(0, 0, "Anna");
        edits.Set(2, 1, "late");

        Assert.Equal(2, edits.Count);
        Assert.True(edits.IsPending(0, 0));
        Assert.Equal("Anna", edits.Value(0, 0));
        Assert.Equal("Ana", edits.Value(1, 0));

        edits.Revert(0, 0);
        Assert.False(edits.IsPending(0, 0));
        Assert.Equal("Ana", edits.Value(0, 0));

        edits.RevertAll();
        Assert.Equal(0, edits.Count);
    }

    [Fact]
    public void Setting_the_original_value_back_is_not_a_change()
    {
        var set = new ResultSet([new("id", "int"), new("data", "varbinary")], [[1, new byte[] { 1, 2 }]], false);
        var edits = ResultEdits.Create(set, [
            new("id", false, null, "dbo", "blobs", "id", true, true, "int"),
            new("data", false, null, "dbo", "blobs", "data", false, true, "varbinary(max)"),
        ], "shop");

        edits.Set(0, 1, new byte[] { 9 });
        edits.Set(0, 1, new byte[] { 1, 2 });

        Assert.Equal(0, edits.Count);
    }

    [Fact]
    public void Editing_a_read_only_cell_is_refused()
    {
        var edits = ResultEdits.Create(Joined(), JoinedSource(), "shop");

        var error = Assert.Throws<InvalidOperationException>(() => edits.Set(0, 4, "X"));
        Assert.Equal("up is computed by the query, not read from a table.", error.Message);
    }

    [Fact]
    public void The_script_updates_each_changed_row_by_its_key_in_one_transaction()
    {
        var edits = ResultEdits.Create(Joined(), JoinedSource(), "shop");
        edits.Set(2, 1, "it's late");
        edits.Set(0, 0, "Anna");
        edits.Set(0, 3, 20);
        edits.Set(0, 1, null);

        Assert.Equal("""
            IF @@TRANCOUNT > 0 THROW 50000, N'A transaction is open in this session. Commit or roll it back before saving edits.', 1;
            BEGIN TRY
                BEGIN TRANSACTION;
                UPDATE [shop].[dbo].[customers] SET [name] = N'Anna' WHERE [id] = 1;
                IF @@ROWCOUNT <> 1 THROW 50000, N'Row 1 no longer matches exactly one row of [shop].[dbo].[customers]. Nothing was saved.', 1;
                UPDATE [shop].[sales].[orders] SET [note] = NULL, [oid] = 20 WHERE [oid] = 10;
                IF @@ROWCOUNT <> 1 THROW 50000, N'Row 1 no longer matches exactly one row of [shop].[sales].[orders]. Nothing was saved.', 1;
                UPDATE [shop].[sales].[orders] SET [note] = N'it''s late' WHERE [oid] = 12;
                IF @@ROWCOUNT <> 1 THROW 50000, N'Row 3 no longer matches exactly one row of [shop].[sales].[orders]. Nothing was saved.', 1;
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                THROW;
            END CATCH;
            """.ReplaceLineEndings("\n"), edits.Script());
    }

    [Fact]
    public void A_null_or_multi_column_key_and_another_database_are_written_exactly()
    {
        var set = new ResultSet([new("a", "int"), new("b", "nvarchar"), new("v", "int")], [[1, null, 5]], false);
        var edits = ResultEdits.Create(set, [
            new("a", false, "archive", "dbo", "pairs", "a", true, true, "int"),
            new("b", false, "archive", "dbo", "pairs", "b", true, true, "nvarchar(5)"),
            new("v", false, "archive", "dbo", "pairs", "v", false, true, "int"),
        ], "shop");
        edits.Set(0, 2, 6);

        Assert.Contains("UPDATE [archive].[dbo].[pairs] SET [v] = 6 WHERE [a] = 1 AND [b] IS NULL;", edits.Script(), StringComparison.Ordinal);
    }

    [Fact]
    public void Committing_writes_the_pending_values_into_the_rows()
    {
        var set = Joined();
        var edits = ResultEdits.Create(set, JoinedSource(), "shop");
        edits.Set(1, 1, "changed");

        edits.Commit();

        Assert.Equal(0, edits.Count);
        Assert.Equal("changed", set.Rows[1][1]);
        Assert.Equal("changed", edits.Value(1, 1));
    }
}
