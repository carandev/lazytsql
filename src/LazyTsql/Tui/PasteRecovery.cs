using Terminal.Gui.Input;

namespace LazyTsql.Tui;

/// <summary>
/// Recovers a bracketed paste whose start marker (ESC[200~) reached Terminal.Gui 2.5 split. Its parser releases an ESC
/// that waits more than 50 ms as the Esc key (a terminal multiplexer can split a paste like that), and the rest of the
/// paste then arrived as keystrokes: Esc left the editor and the pasted text ran as shortcuts, so a q asked to quit and a
/// pasted space confirmed it. When ESC[ arrives together but 200~ later, the parser swallows the ESC[ and the rest is
/// typed as text, "200~" included.
/// <para>
/// So Esc is held until the next keys show whether a paste starts; Esc alone is released after
/// <see cref="EscapeWait"/>. Inside a recovered paste every key is collected as text until the end marker: Terminal.Gui
/// throws on ESC[201~ (see <see cref="KeySequenceGuard"/>), or it arrives split as Esc and [201~, or input stays idle
/// for <see cref="PasteIdle"/>.
/// </para>
/// </summary>
public sealed class PasteRecovery
{
    public static readonly TimeSpan EscapeWait = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan PasteIdle = TimeSpan.FromSeconds(1);

    private const string Start = "[200~", EndMarker = "[201~";

    /// <summary>
    /// What to do with a key: <see cref="Held"/> keeps it from the app, <see cref="Release"/> lists earlier held keys to
    /// replay before it, and <see cref="Paste"/> is recovered text to paste.
    /// </summary>
    public readonly record struct Outcome(bool Held, IReadOnlyList<Key> Release, string? Paste)
    {
        public static readonly Outcome Pass = new(false, [], null);
    }

    private enum State { Idle, AfterEscape, Pasting }

    private State _state;
    private readonly List<Key> _held = [];
    private int _matched;
    private readonly System.Text.StringBuilder _paste = new();
    // In a paste, how much of the end marker arrived after an Esc; -1 when no Esc did.
    private int _endMatched = -1;

    /// <summary>How long to wait before calling <see cref="Expire"/>, or null when nothing is held.</summary>
    public TimeSpan? Wait => _state switch
    {
        State.AfterEscape => EscapeWait,
        State.Pasting => PasteIdle,
        _ => null,
    };

    public Outcome Feed(Key key)
    {
        switch (_state)
        {
            case State.AfterEscape:
                if (key.AsRune.Value == Start[_matched])
                {
                    _held.Add(key);
                    if (++_matched == Start.Length) BeginPaste();
                    return new Outcome(true, [], null);
                }

                var release = TakeHeld();
                return Feed(key) with { Release = release };

            case State.Pasting:
                return FeedPaste(key);

            default:
                if (key != Key.Esc) return Outcome.Pass;
                _state = State.AfterEscape;
                _held.Add(key);
                return new Outcome(true, [], null);
        }
    }

    /// <summary>The parser swallowed <paramref name="sequence"/>; a lone ESC[ may be the start of a split paste.</summary>
    public void Swallowed(string sequence)
    {
        if (_state != State.Idle || sequence != "\u001b[") return;
        _state = State.AfterEscape;
        _matched = 1;
    }

    /// <summary>The <see cref="Wait"/> ran out: Esc was only Esc, or the paste's end never arrived.</summary>
    public Outcome Expire() => _state switch
    {
        State.AfterEscape => new Outcome(false, TakeHeld(), null),
        State.Pasting => new Outcome(false, [], TakePaste()),
        _ => Outcome.Pass,
    };

    /// <summary>The parser skipped an unknown key sequence, which in a paste is its end marker.</summary>
    public Outcome End() => _state == State.Pasting ? new Outcome(false, [], TakePaste()) : Outcome.Pass;

    private Outcome FeedPaste(Key key)
    {
        if (key == Key.Esc)
        {
            _endMatched = 0;
            return new Outcome(true, [], null);
        }

        if (_endMatched >= 0)
        {
            if (key.AsRune.Value == EndMarker[_endMatched])
            {
                if (++_endMatched == EndMarker.Length) return new Outcome(true, [], TakePaste());
                return new Outcome(true, [], null);
            }

            // Not the end after all; what came after the Esc is pasted text.
            _paste.Append(EndMarker[.._endMatched]);
            _endMatched = -1;
        }

        if (Text(key) is { } text) _paste.Append(text);
        return new Outcome(true, [], null);
    }

    private static string? Text(Key key)
    {
        if (key == Key.Enter || key == Key.J.WithCtrl) return "\n";
        if (key == Key.Tab) return "\t";
        var rune = key.AsRune;
        return rune.Value == 0 ? null : rune.ToString();
    }

    private void BeginPaste()
    {
        _state = State.Pasting;
        _held.Clear();
        _matched = 0;
        _paste.Clear();
        _endMatched = -1;
    }

    private List<Key> TakeHeld()
    {
        var held = _held.ToList();
        _held.Clear();
        _matched = 0;
        _state = State.Idle;
        return held;
    }

    private string TakePaste()
    {
        var text = _paste.ToString();
        _paste.Clear();
        _endMatched = -1;
        _state = State.Idle;
        return text;
    }
}
