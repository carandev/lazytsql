using LazyTsql.Tui;
using Terminal.Gui.Drivers;

namespace LazyTsql.Tests;

public class KeySequenceGuardTests
{
    [Theory]
    [InlineData("\u001b[201~")]
    [InlineData("\u001b[999~")]
    public void An_unknown_key_sequence_is_skipped(string sequence)
    {
        // Terminal.Gui 2.5 matches every ESC[<n>~ but has keys for only some; when it stops throwing, the guard can go.
        var error = Assert.Throws<NullReferenceException>(() => new CsiKeyPattern().GetKey(sequence));

        Assert.True(KeySequenceGuard.IsUnknownKeySequence(error));
    }

    [Fact]
    public void Other_errors_are_not_skipped()
    {
        var error = Assert.Throws<NullReferenceException>(() => ((string)null!).Trim());

        Assert.False(KeySequenceGuard.IsUnknownKeySequence(error));
        Assert.False(KeySequenceGuard.IsUnknownKeySequence(new InvalidOperationException()));
    }
}
