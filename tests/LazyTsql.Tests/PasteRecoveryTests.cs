using LazyTsql.Tui;
using Terminal.Gui.Input;

namespace LazyTsql.Tests;

public class PasteRecoveryTests
{
    private static Key[] Keys(string text) => [.. text.Select(c => c switch
    {
        '\n' => Key.Enter,
        '\t' => Key.Tab,
        ' ' => Key.Space,
        _ => new Key(c),
    })];

    /// <summary>Feeds every key and returns the ones that were not held.</summary>
    private static List<Key> Feed(PasteRecovery recovery, IEnumerable<Key> keys, List<string> pastes, List<Key>? released = null)
    {
        var passed = new List<Key>();
        foreach (var key in keys)
        {
            var outcome = recovery.Feed(key);
            released?.AddRange(outcome.Release);
            if (outcome.Paste is { } text) pastes.Add(text);
            if (!outcome.Held) passed.Add(key);
        }

        return passed;
    }

    [Fact]
    public void A_paste_whose_escape_arrived_alone_comes_back_as_one_paste()
    {
        var recovery = new PasteRecovery();
        var pastes = new List<string>();

        var passed = Feed(recovery, [Key.Esc, .. Keys("[200~select q\n\tfrom t")], pastes);
        var end = recovery.End();

        Assert.Empty(passed);
        Assert.Empty(pastes);
        Assert.Equal("select q\n\tfrom t", end.Paste);
        Assert.Empty(end.Release);
        Assert.Null(recovery.Wait);
    }

    [Fact]
    public void An_end_marker_that_arrives_split_ends_the_paste_too()
    {
        var recovery = new PasteRecovery();
        var pastes = new List<string>();

        var passed = Feed(recovery, [Key.Esc, .. Keys("[200~Select 1"), Key.Esc, .. Keys("[201~x")], pastes);

        Assert.Equal(["Select 1"], pastes);
        Assert.Equal([new Key('x')], passed);
    }

    [Fact]
    public void A_paste_whose_csi_was_swallowed_comes_back_as_one_paste()
    {
        var recovery = new PasteRecovery();
        var pastes = new List<string>();

        recovery.Swallowed("\u001b[");
        var passed = Feed(recovery, Keys("200~select 1"), pastes);

        Assert.Empty(passed);
        Assert.Equal("select 1", recovery.End().Paste);
    }

    [Fact]
    public void Escape_alone_is_held_and_released_when_the_wait_runs_out()
    {
        var recovery = new PasteRecovery();

        var outcome = recovery.Feed(Key.Esc);

        Assert.True(outcome.Held);
        Assert.Equal(PasteRecovery.EscapeWait, recovery.Wait);
        var expired = recovery.Expire();
        Assert.Equal([Key.Esc], expired.Release);
        Assert.Null(expired.Paste);
        Assert.Null(recovery.Wait);
    }

    [Fact]
    public void Escape_then_other_keys_releases_them_in_order()
    {
        var recovery = new PasteRecovery();
        var released = new List<Key>();

        var passed = Feed(recovery, [Key.Esc, .. Keys("[2x")], [], released);

        Assert.Equal([Key.Esc, new Key('['), new Key('2')], released);
        Assert.Equal([new Key('x')], passed);
        Assert.Null(recovery.Wait);
    }

    [Fact]
    public void Escape_twice_releases_the_first_and_holds_the_second()
    {
        var recovery = new PasteRecovery();
        var released = new List<Key>();

        var passed = Feed(recovery, [Key.Esc, Key.Esc], [], released);

        Assert.Equal([Key.Esc], released);
        Assert.Empty(passed);
        Assert.Equal(PasteRecovery.EscapeWait, recovery.Wait);
    }

    [Fact]
    public void Keys_without_escape_pass_untouched()
    {
        var recovery = new PasteRecovery();

        var passed = Feed(recovery, Keys("[200~q"), []);

        Assert.Equal(Keys("[200~q"), passed);
        Assert.Null(recovery.Wait);
    }

    [Fact]
    public void A_paste_whose_end_never_arrives_is_delivered_when_input_goes_idle()
    {
        var recovery = new PasteRecovery();
        Feed(recovery, [Key.Esc, .. Keys("[200~abc")], []);

        Assert.Equal(PasteRecovery.PasteIdle, recovery.Wait);
        var expired = recovery.Expire();

        Assert.Equal("abc", expired.Paste);
        Assert.Empty(expired.Release);
        Assert.Null(recovery.Wait);
    }

    [Fact]
    public void A_skipped_sequence_outside_a_paste_changes_nothing()
    {
        var recovery = new PasteRecovery();

        var end = recovery.End();

        Assert.Null(end.Paste);
        Assert.Empty(end.Release);
        Assert.False(end.Held);
    }
}
