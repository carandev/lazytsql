using LazyTsql.Core.Syntax;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace LazyTsql.Tui;

/// <summary>
/// Minimal lazygit-like look: the terminal's own background and foreground everywhere,
/// gray borders that turn green on the focused panel, and a blue selection.
/// Uses the 16 ANSI colors so it follows the terminal's color scheme.
/// </summary>
public static class Theme
{
    private static readonly Color Default = Color.None;
    private static readonly Color Accent = new(ColorName16.Green);
    private static readonly Color Muted = new(ColorName16.DarkGray);
    private static readonly Color Selection = new(ColorName16.Blue);

    public static Scheme Content { get; } = new(new Attribute(Default, Default))
    {
        Focus = new Attribute(new Color(ColorName16.White), Selection, TextStyle.Bold),
        Active = new Attribute(Default, Muted),
        Highlight = new Attribute(Default, Muted),
        HotNormal = new Attribute(Accent, Default),
        HotFocus = new Attribute(Accent, Selection, TextStyle.Bold),
        HotActive = new Attribute(Accent, Muted),
        Editable = new Attribute(Default, Default),
        ReadOnly = new Attribute(Default, Default),
        Disabled = new Attribute(Muted, Default),
    };

    // Frame titles draw with the Focus role while focused; keep them as accent text instead of a filled bar.
    public static Scheme FocusedBorder { get; } = new(Content)
    {
        Normal = new Attribute(Accent, Default, TextStyle.Bold),
        Focus = new Attribute(Accent, Default, TextStyle.Bold),
    };

    public static Scheme MutedBorder { get; } = new(Content) { Normal = new Attribute(Muted, Default), Focus = new Attribute(Muted, Default) };

    public static Scheme TableHeader { get; } = Uniform(new Attribute(Default, Default, TextStyle.Bold));

    public static Scheme Hints { get; } = Uniform(new Attribute(Muted, Default));

    private static readonly Attribute KeywordText = new(Selection, Default, TextStyle.Bold);
    private static readonly Attribute StringText = new(Accent, Default);
    private static readonly Attribute CommentText = new(Muted, Default, TextStyle.Italic);
    private static readonly Attribute NumberText = new(new Color(ColorName16.Magenta), Default);
    private static readonly Attribute VariableText = new(new Color(ColorName16.Cyan), Default);

    /// <summary>Editor color for a token; null keeps the normal text color.</summary>
    public static Attribute? Syntax(SqlTokenKind kind) => kind switch
    {
        SqlTokenKind.Keyword => KeywordText,
        SqlTokenKind.String => StringText,
        SqlTokenKind.Comment => CommentText,
        SqlTokenKind.Number => NumberText,
        SqlTokenKind.Variable => VariableText,
        _ => null,
    };

    // Scheme(Attribute) derives Focus and others by inverting colors; a uniform scheme keeps one look in every role.
    private static Scheme Uniform(Attribute attribute) => new(attribute)
    {
        Focus = attribute,
        Active = attribute,
        Highlight = attribute,
        HotNormal = attribute,
        HotFocus = attribute,
        HotActive = attribute,
        Editable = attribute,
        ReadOnly = attribute,
    };

    /// <summary>Panel frame whose border follows focus while its content keeps the plain scheme.</summary>
    public static void StylePanel(FrameView frame, View content)
    {
        frame.SetScheme(MutedBorder);
        content.SetScheme(Content);
        frame.HasFocusChanged += (_, _) => frame.SetScheme(frame.HasFocus ? FocusedBorder : MutedBorder);
        if (content.HorizontalScrollBar is { } horizontal) horizontal.VisibilityMode = ScrollBarVisibilityMode.None;
    }

    // Without a filled background an empty input is invisible, so dialog inputs get a muted underline.
    private static Scheme Input { get; } = new(Content)
    {
        Normal = new Attribute(Default, Default, TextStyle.Underline),
        Editable = new Attribute(Default, Default, TextStyle.Underline),
        Focus = new Attribute(Default, Default, TextStyle.Underline | TextStyle.Bold),
    };

    public static void StyleDialog(Dialog dialog)
    {
        dialog.SetScheme(FocusedBorder);
        StyleDialogContent(dialog.SubViews);
    }

    private static void StyleDialogContent(IEnumerable<View> views)
    {
        foreach (var view in views)
        {
            view.SetScheme(view is TextField ? Input : Content);
            StyleDialogContent(view.SubViews);
        }
    }
}
