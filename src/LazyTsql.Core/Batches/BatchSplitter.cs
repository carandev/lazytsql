using System.Text;
using System.Text.RegularExpressions;

namespace LazyTsql.Core.Batches;

/// <summary>
/// Splits a script on <c>GO [count]</c> separators the way sqlcmd and SSMS do:
/// the separator must be alone on its line and is ignored inside comments, strings and quoted identifiers.
/// </summary>
public static partial class BatchSplitter
{
    [GeneratedRegex(@"^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorPattern();

    public static IReadOnlyList<SqlBatch> Split(string script)
    {
        var batches = new List<SqlBatch>();
        var current = new StringBuilder();
        var scanner = new LexicalState();
        var batchStartLine = 1;
        var lineNumber = 0;

        foreach (var (line, terminator) in EnumerateLines(script))
        {
            lineNumber++;

            if (scanner.IsNeutral && SeparatorPattern().Match(line) is { Success: true } separator)
            {
                var count = separator.Groups["count"].Success ? int.Parse(separator.Groups["count"].Value) : 1;
                AddBatch(batches, current, batchStartLine, count);
                current.Clear();
                batchStartLine = lineNumber + 1;
                continue;
            }

            scanner.Consume(line);
            current.Append(line).Append(terminator);
        }

        AddBatch(batches, current, batchStartLine, 1);
        return batches;
    }

    private static void AddBatch(List<SqlBatch> batches, StringBuilder text, int startLine, int repeatCount)
    {
        var batchText = text.ToString().TrimEnd('\r', '\n');
        if (!string.IsNullOrWhiteSpace(batchText))
        {
            batches.Add(new SqlBatch(batchText, startLine, repeatCount));
        }
    }

    private static IEnumerable<(string Line, string Terminator)> EnumerateLines(string script)
    {
        var start = 0;
        while (start < script.Length)
        {
            var end = script.IndexOf('\n', start);
            if (end < 0)
            {
                yield return (script[start..], string.Empty);
                yield break;
            }

            var hasCarriageReturn = end > start && script[end - 1] == '\r';
            var lineEnd = hasCarriageReturn ? end - 1 : end;
            yield return (script[start..lineEnd], hasCarriageReturn ? "\r\n" : "\n");
            start = end + 1;
        }
    }

    /// <summary>Tracks constructs that can span lines, where a GO line is plain text.</summary>
    private struct LexicalState
    {
        private int _blockCommentDepth;
        private char _openQuote;

        public readonly bool IsNeutral => _blockCommentDepth == 0 && _openQuote == '\0';

        public void Consume(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (_openQuote != '\0')
                {
                    if (c == _openQuote)
                    {
                        // A doubled closing quote is an escaped quote character.
                        if (next == _openQuote) i++;
                        else _openQuote = '\0';
                    }
                }
                else if (_blockCommentDepth > 0)
                {
                    // T-SQL block comments nest.
                    if (c == '/' && next == '*') { _blockCommentDepth++; i++; }
                    else if (c == '*' && next == '/') { _blockCommentDepth--; i++; }
                }
                else if (c == '-' && next == '-')
                {
                    return;
                }
                else if (c == '/' && next == '*')
                {
                    _blockCommentDepth++;
                    i++;
                }
                else if (c is '\'' or '"')
                {
                    _openQuote = c;
                }
                else if (c == '[')
                {
                    _openQuote = ']';
                }
            }
        }
    }
}
