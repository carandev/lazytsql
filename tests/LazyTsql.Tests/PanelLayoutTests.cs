using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class PanelLayoutTests
{
    [Fact]
    public void The_focused_panel_grows_and_shrinks_in_steps()
    {
        var layout = new PanelLayout();

        layout.Resize(KeyContext.Query, +1);
        Assert.Equal(PanelLayout.DefaultQuery + PanelLayout.Step, layout.Query);
        layout.Resize(KeyContext.Connections, -1);
        Assert.Equal(PanelLayout.DefaultConnections - PanelLayout.Step, layout.Connections);
        layout.Resize(KeyContext.Messages, +1);
        Assert.Equal(PanelLayout.DefaultMessages + 1, layout.MessagesRows);
        layout.ResizeLeft(-1);
        Assert.Equal(PanelLayout.DefaultLeft - PanelLayout.Step, layout.Left);
    }

    [Fact]
    public void A_panel_that_fills_the_rest_grows_by_shrinking_its_neighbour()
    {
        var layout = new PanelLayout();

        layout.Resize(KeyContext.Results, +1);
        layout.Resize(KeyContext.Schema, +1);

        Assert.Equal(PanelLayout.DefaultQuery - PanelLayout.Step, layout.Query);
        Assert.Equal(PanelLayout.DefaultFiles - PanelLayout.Step, layout.Files);
    }

    [Fact]
    public void Sizes_stay_within_limits()
    {
        var layout = new PanelLayout();
        for (var i = 0; i < 100; i++)
        {
            layout.Resize(KeyContext.Query, +1);
            layout.Resize(KeyContext.Messages, -1);
            layout.Resize(KeyContext.Files, +1);
            layout.Resize(KeyContext.Connections, +1);
            layout.ResizeLeft(+1);
        }

        Assert.Equal((80, 3, 60), (layout.Query, layout.MessagesRows, layout.Left));
        Assert.True(layout.Connections + layout.Files <= 85, $"{layout.Connections} + {layout.Files}");
    }

    [Fact]
    public void A_layout_round_trips_as_text_and_bad_text_gives_the_defaults()
    {
        var layout = new PanelLayout();
        layout.Resize(KeyContext.Query, +1);

        Assert.Equal(layout, PanelLayout.Parse(layout.ToString()));
        Assert.Equal(new PanelLayout(), PanelLayout.Parse("nonsense"));
        Assert.Equal(new PanelLayout(), PanelLayout.Parse("28 20 30 999 7"));
    }
}
