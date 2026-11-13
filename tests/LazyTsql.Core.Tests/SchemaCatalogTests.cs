using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Tests;

public class SchemaCatalogTests
{
    [Fact]
    public void Objects_named_finds_every_schema_ignoring_case()
    {
        var catalog = new SchemaCatalog();
        var dbo = new DbObject("dbo", "Orders", DbObjectKind.Table);
        var sales = new DbObject("sales", "orders", DbObjectKind.View);
        catalog.SetObjects("shop", [dbo, new DbObject("dbo", "customers", DbObjectKind.Table), sales]);

        Assert.Equal([dbo, sales], catalog.ObjectsNamed("SHOP", "ORDERS"));
        Assert.Empty(catalog.ObjectsNamed("shop", "nope"));
    }

    [Fact]
    public void Objects_named_follows_reloaded_objects_and_never_requests_a_load()
    {
        var catalog = new SchemaCatalog();
        var requested = new List<string>();
        catalog.ObjectsNeeded += requested.Add;

        Assert.Empty(catalog.ObjectsNamed("shop", "orders"));
        Assert.Empty(requested);

        catalog.SetObjects("shop", [new DbObject("dbo", "orders", DbObjectKind.Table)]);
        Assert.Single(catalog.ObjectsNamed("shop", "orders"));

        var renamed = new DbObject("dbo", "orders_v2", DbObjectKind.Table);
        catalog.SetObjects("shop", [renamed]);
        Assert.Empty(catalog.ObjectsNamed("shop", "orders"));
        Assert.Equal([renamed], catalog.ObjectsNamed("shop", "orders_v2"));

        catalog.Clear();
        Assert.Empty(catalog.ObjectsNamed("shop", "orders_v2"));
    }

    [Fact]
    public void A_snapshot_keeps_the_names_loaded_when_it_was_taken_and_requests_nothing()
    {
        var catalog = new SchemaCatalog { Databases = ["shop"] };
        var orders = new DbObject("dbo", "orders", DbObjectKind.Table);
        catalog.SetObjects("shop", [orders]);
        catalog.SetColumns("shop", orders, [new ColumnDefinition("id", "int", false, true, false)]);
        var requested = new List<string>();
        catalog.ObjectsNeeded += database => requested.Add($"objects {database}");
        catalog.ColumnsNeeded += (database, table) => requested.Add($"columns {database}.{table.Name}");
        catalog.ForeignKeysNeeded += database => requested.Add($"keys {database}");

        var snapshot = catalog.Snapshot();
        catalog.SetObjects("shop", []);
        catalog.Databases = [];

        Assert.Equal(["shop"], snapshot.Databases);
        Assert.Equal([orders], snapshot.Objects("shop"));
        Assert.Equal([orders], snapshot.ObjectsNamed("shop", "orders"));
        Assert.Single(snapshot.Columns("shop", orders)!);
        Assert.Null(snapshot.Objects("archive"));
        Assert.Null(snapshot.Columns("shop", new DbObject("dbo", "lines", DbObjectKind.Table)));
        Assert.Null(snapshot.ForeignKeys("shop"));
        Assert.Empty(requested);
    }

    [Fact]
    public void What_a_snapshot_missed_is_requested_once_unless_it_loaded_since()
    {
        var catalog = new SchemaCatalog();
        var lines = new DbObject("dbo", "lines", DbObjectKind.Table);
        var requested = new List<string>();
        catalog.ObjectsNeeded += database => requested.Add($"objects {database}");
        catalog.ColumnsNeeded += (database, table) => requested.Add($"columns {database}.{table.Name}");
        catalog.ForeignKeysNeeded += database => requested.Add($"keys {database}");

        var snapshot = catalog.Snapshot();
        snapshot.Objects("shop");
        snapshot.Objects("shop");
        snapshot.Objects("archive");
        snapshot.Columns("shop", lines);
        snapshot.ForeignKeys("shop");
        catalog.SetObjects("archive", []);

        catalog.RequestMissing(snapshot);
        catalog.RequestMissing(snapshot);

        Assert.Equal(["objects shop", "columns shop.lines", "keys shop"], requested);
    }
}
