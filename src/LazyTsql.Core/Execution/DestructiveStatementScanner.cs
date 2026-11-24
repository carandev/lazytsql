using LazyTsql.Core.Batches;

namespace LazyTsql.Core.Execution;

/// <summary>An UPDATE or DELETE statement with no top-level WHERE clause, and the 1-based line it starts on.</summary>
public sealed record UnsafeStatement(string Verb, int Line);

/// <summary>
/// Flags UPDATE and DELETE statements with no WHERE clause: run as-is, they touch every row in the table.
/// A WHERE only inside a subquery or nested statement does not count. A statement that starts with a leading
/// CTE (<c>WITH ... AS (...) UPDATE/DELETE ...</c>) is not checked, since its real verb is not the first word.
/// </summary>
public static class DestructiveStatementScanner
{
    public static IReadOnlyList<UnsafeStatement> FindUnsafeStatements(string script)
    {
        var results = new List<UnsafeStatement>();
        foreach (var batch in BatchSplitter.Split(script))
        {
            foreach (var found in ScanBatch(batch.Text))
            {
                results.Add(new UnsafeStatement(found.Verb, batch.StartLine + found.Line - 1));
            }
        }

        return results;
    }

    private static IReadOnlyList<UnsafeStatement> ScanBatch(string text)
    {
        var results = new List<UnsafeStatement>();
        var line = 1;
        var parenDepth = 0;
        string? verb = null;
        var verbLine = 1;
        var hasWhere = false;
        var wordIndex = 0;

        void EndStatement()
        {
            if (verb is "UPDATE" or "DELETE" && !hasWhere) results.Add(new UnsafeStatement(verb, verbLine));
            verb = null;
            hasWhere = false;
            wordIndex = 0;
        }

        void Word(string word)
        {
            if (parenDepth != 0) return;
            var upper = word.ToUpperInvariant();
            if (wordIndex == 0)
            {
                verb = upper;
                verbLine = line;
            }
            // UPDATE STATISTICS recomputes stats, not row data; it is not a destructive DML statement.
            else if (wordIndex == 1 && verb == "UPDATE" && upper == "STATISTICS")
            {
                verb = null;
            }
            else if (upper == "WHERE")
            {
                hasWhere = true;
            }

            wordIndex++;
        }

        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var stop = end < 0 ? text.Length : end + 2;
                for (var j = i; j < stop; j++) if (text[j] == '\n') line++;
                i = stop;
                continue;
            }

            if (c == '\'' || c == '"')
            {
                var quote = c;
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\n') line++;
                    if (text[i] == quote)
                    {
                        if (i + 1 < text.Length && text[i + 1] == quote) { i += 2; continue; }
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (c == '[')
            {
                var end = text.IndexOf(']', i + 1);
                i = end < 0 ? text.Length : end + 1;
                continue;
            }

            if (c == '(') { parenDepth++; i++; continue; }
            if (c == ')') { parenDepth = Math.Max(0, parenDepth - 1); i++; continue; }

            if (c == ';')
            {
                if (parenDepth == 0) EndStatement();
                i++;
                continue;
            }

            if (char.IsLetter(c) || c is '_' or '#')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '#')) i++;
                Word(text[start..i]);
                continue;
            }

            i++;
        }

        EndStatement();
        return results;
    }
}
