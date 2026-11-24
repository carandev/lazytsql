using LazyTsql.Core.Execution;

namespace LazyTsql.Core.Tests;

public class DestructiveStatementScannerTests
{
    [Fact]
    public void Delete_without_where_is_flagged()
    {
        var found = DestructiveStatementScanner.FindUnsafeStatements("DELETE FROM Orders;");

        var statement = Assert.Single(found);
        Assert.Equal("DELETE", statement.Verb);
        Assert.Equal(1, statement.Line);
    }

    [Fact]
    public void Update_without_where_is_flagged()
    {
        var found = DestructiveStatementScanner.FindUnsafeStatements("UPDATE Orders SET Status = 'x'");

        var statement = Assert.Single(found);
        Assert.Equal("UPDATE", statement.Verb);
    }

    [Fact]
    public void Delete_with_where_is_safe()
    {
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements("DELETE FROM Orders WHERE Id = 1;"));
    }

    [Fact]
    public void Update_with_where_is_safe()
    {
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements("UPDATE Orders SET Status = 'x' WHERE Id = 1;"));
    }

    [Fact]
    public void Where_only_inside_a_subquery_does_not_count()
    {
        var sql = "DELETE FROM Orders WHERE Id IN (SELECT Id FROM Cancelled)";
        // Top-level WHERE is present even though the subquery also filters, so this one is safe.
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements(sql));

        var unsafeSql = "DELETE FROM Orders WHERE CustomerId IN (SELECT Id FROM Customers WHERE Active = 1)";
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements(unsafeSql));
    }

    [Fact]
    public void Select_and_insert_are_never_flagged()
    {
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements("SELECT * FROM Orders; INSERT INTO Orders VALUES (1);"));
    }

    [Fact]
    public void Where_keyword_inside_a_string_or_comment_does_not_count()
    {
        var found = DestructiveStatementScanner.FindUnsafeStatements("DELETE FROM Orders -- WHERE used to be here\n");
        Assert.Single(found);

        var withString = DestructiveStatementScanner.FindUnsafeStatements("UPDATE Orders SET Note = 'no WHERE clause needed'");
        Assert.Single(withString);
    }

    [Fact]
    public void Each_statement_is_checked_independently()
    {
        var sql = "DELETE FROM A WHERE Id = 1; DELETE FROM B;";

        var found = DestructiveStatementScanner.FindUnsafeStatements(sql);

        var statement = Assert.Single(found);
        Assert.Equal("DELETE", statement.Verb);
    }

    [Fact]
    public void Batches_separated_by_go_are_checked_independently()
    {
        var sql = "DELETE FROM A WHERE Id = 1\nGO\nDELETE FROM B\nGO\n";

        var found = DestructiveStatementScanner.FindUnsafeStatements(sql);

        var statement = Assert.Single(found);
        Assert.Equal("DELETE", statement.Verb);
        Assert.Equal(3, statement.Line);
    }

    [Fact]
    public void Update_statistics_is_not_flagged()
    {
        Assert.Empty(DestructiveStatementScanner.FindUnsafeStatements("UPDATE STATISTICS Orders"));
    }
}
