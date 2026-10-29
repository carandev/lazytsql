using LazyTsql.Core.Batches;

namespace LazyTsql.Core.Tests;

public class SqlBatchTests
{
    [Theory]
    [InlineData("SELECT * FROM dbo.orders")]
    [InlineData("select a.id from a join (select id from b) x on x.id = a.id where a.name = 'DELETE' -- UPDATE\n;")]
    [InlineData("SELECT id FROM a UNION ALL SELECT id FROM b ORDER BY id OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY")]
    [InlineData("SELECT CASE WHEN x = 1 THEN 'a' END, [update] FROM t WHERE y IN (SELECT y FROM u) /* ; INSERT */")]
    public void A_lone_SELECT_can_stop_at_the_row_limit(string text)
    {
        Assert.True(new SqlBatch(text, 1).IsSingleQuery);
    }

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1 SELECT 2")]
    [InlineData("SELECT * FROM a\nUPDATE a SET x = 1")]
    [InlineData("SELECT * INTO copy FROM a")]
    [InlineData("SELECT * FROM a EXEC dbo.audit")]
    [InlineData("SELECT * FROM a SET NOCOUNT ON")]
    [InlineData("WITH x AS (SELECT 1 AS id) SELECT * FROM x")]
    [InlineData("EXEC dbo.report")]
    [InlineData("DECLARE @x int = 1; SELECT @x")]
    [InlineData("")]
    public void Anything_else_reads_every_row(string text)
    {
        Assert.False(new SqlBatch(text, 1).IsSingleQuery);
    }
}
