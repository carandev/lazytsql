using Terminal.Gui.App;

namespace LazyTsql.Tui;

/// <summary>
/// Terminal.Gui 2.5 reads every ESC[&lt;n&gt;~ as a key but has keys for only some numbers; any other one, such as the end
/// of a paste whose start arrived split (ESC[201~) or F13 and beyond, throws NullReferenceException from its input
/// parser. That closed lazytsql, and inside a dialog it left the app hung. Every run loop goes through here so the
/// sequence is skipped and the loop goes on.
/// </summary>
public static class KeySequenceGuard
{
    public static void Run(IApplication app, IRunnable view) => app.Run(view, IsUnknownKeySequence);

    /// <summary>
    /// The parser's copy of the missing key throws first; the frame under it is the pattern's GetKey, or the parser's
    /// RaiseKeyboardEvent once Native AOT inlines GetKey. Errors thrown by the app's own key handlers, which run below
    /// that event, are not skipped.
    /// </summary>
    public static bool IsUnknownKeySequence(Exception error)
    {
        if (error is not NullReferenceException || error.StackTrace is not { } trace) return false;
        var frames = trace.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return frames.Length > 1
            && frames[0].StartsWith("at Terminal.Gui.Input.Key..ctor(", StringComparison.Ordinal)
            && (frames[1].Contains("AnsiKeyboardParserPattern.GetKey(", StringComparison.Ordinal)
                || frames[1].Contains("AnsiResponseParserBase.RaiseKeyboardEvent(", StringComparison.Ordinal));
    }
}
