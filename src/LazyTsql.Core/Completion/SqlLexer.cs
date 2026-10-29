using System.Text;
using LazyTsql.Core.Syntax;

namespace LazyTsql.Core.Completion;

internal enum TokenType
{
    Word,
    Dot,
    Comma,
    OpenParen,
    Separator,
    Other,
}

/// <summary>A word keeps its unbracketed text; Start and End are columns on <see cref="Row"/>, End exclusive.</summary>
internal sealed record Token(TokenType Type, string Text, int Row, int Start, int End, bool Bracketed = false);

/// <summary>Splits a script into the tokens completion needs. Strings, comments, numbers and variables are opaque.</summary>
internal static class SqlLexer
{
    public static List<Token> Tokenize(IReadOnlyList<string> lines, SqlTokenKind[][] kinds)
    {
        var tokens = new List<Token>();
        for (var row = 0; row < lines.Count; row++)
        {
            var line = lines[row];
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase) || line.TrimStart().StartsWith("GO ", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new Token(TokenType.Separator, "GO", row, 0, line.Length));
                continue;
            }

            var i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (kinds[row][i] is not (SqlTokenKind.Plain or SqlTokenKind.Keyword))
                {
                    var end = i;
                    while (end < line.Length && kinds[row][end] == kinds[row][i]) end++;
                    tokens.Add(new Token(TokenType.Other, line[i..end], row, i, end));
                    i = end;
                }
                else if (c is '[' or '"')
                {
                    var (text, end) = Delimited(line, i, c == '[' ? ']' : '"');
                    tokens.Add(new Token(TokenType.Word, text, row, i, end, Bracketed: true));
                    i = end;
                }
                else if (char.IsLetter(c) || c is '_' or '#')
                {
                    var end = i;
                    while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] is '_' or '#' or '@' or '$')) end++;
                    tokens.Add(new Token(TokenType.Word, line[i..end], row, i, end));
                    i = end;
                }
                else
                {
                    if (!char.IsWhiteSpace(c))
                    {
                        var type = c switch { '.' => TokenType.Dot, ',' => TokenType.Comma, '(' => TokenType.OpenParen, ';' => TokenType.Separator, _ => TokenType.Other };
                        tokens.Add(new Token(type, c.ToString(), row, i, i + 1));
                    }

                    i++;
                }
            }
        }

        return tokens;
    }

    /// <summary>A bracketed or quoted name up to its closing delimiter (doubled delimiters escape), or to the line end.</summary>
    private static (string Text, int End) Delimited(string line, int start, char close)
    {
        var text = new StringBuilder();
        var i = start + 1;
        while (i < line.Length)
        {
            if (line[i] == close)
            {
                if (i + 1 < line.Length && line[i + 1] == close)
                {
                    text.Append(close);
                    i += 2;
                    continue;
                }

                return (text.ToString(), i + 1);
            }

            text.Append(line[i++]);
        }

        return (text.ToString(), i);
    }
}
