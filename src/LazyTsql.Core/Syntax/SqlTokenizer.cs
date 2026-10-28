namespace LazyTsql.Core.Syntax;

public enum SqlTokenKind : byte
{
    Plain,
    Keyword,
    String,
    Comment,
    Number,
    Variable,
}

/// <summary>
/// Lexical T-SQL classification for syntax highlighting. It does not parse; it only tracks the state that spans
/// lines (nested block comments, strings, bracketed and quoted identifiers) so every character gets a kind.
/// </summary>
public static class SqlTokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD", "ALL", "ALTER", "AND", "ANY", "APPLY", "AS", "ASC", "BACKUP", "BEGIN", "BETWEEN", "BREAK", "BY",
        "CASCADE", "CASE", "CAST", "CATCH", "CHECK", "CLOSE", "COLUMN", "COMMIT", "CONSTRAINT", "CONTINUE", "CONVERT",
        "CREATE", "CROSS", "CURSOR", "DATABASE", "DEALLOCATE", "DECLARE", "DEFAULT", "DELETE", "DENY", "DESC", "DISTINCT",
        "DROP", "ELSE", "END", "ESCAPE", "EXCEPT", "EXEC", "EXECUTE", "EXISTS", "FETCH", "FOR", "FOREIGN", "FROM", "FULL",
        "FUNCTION", "GO", "GOTO", "GRANT", "GROUP", "HAVING", "IDENTITY", "IF", "IN", "INDEX", "INNER", "INSERT",
        "INTERSECT", "INTO", "IS", "JOIN", "KEY", "LEFT", "LIKE", "MATCHED", "MERGE", "NEXT", "NOCOUNT", "NOLOCK", "NOT",
        "NULL", "OF", "OFF", "OFFSET", "ON", "ONLY", "OPEN", "OR", "ORDER", "OUTER", "OUTPUT", "OVER", "PARTITION",
        "PERCENT", "PIVOT", "PRIMARY", "PRINT", "PROC", "PROCEDURE", "RAISERROR", "REFERENCES", "RETURN", "RETURNS",
        "REVOKE", "RIGHT", "ROLLBACK", "ROWS", "SAVE", "SCHEMA", "SELECT", "SET", "TABLE", "THEN", "THROW", "TIES", "TO",
        "TOP", "TRAN", "TRANSACTION", "TRIGGER", "TRUNCATE", "TRY", "UNION", "UNIQUE", "UNPIVOT", "UPDATE", "USE",
        "USING", "VALUES", "VIEW", "WAITFOR", "WHEN", "WHERE", "WHILE", "WITH",
        "BIGINT", "BINARY", "BIT", "CHAR", "DATE", "DATETIME", "DATETIME2", "DATETIMEOFFSET", "DECIMAL", "FLOAT", "INT",
        "MAX", "MONEY", "NCHAR", "NTEXT", "NUMERIC", "NVARCHAR", "REAL", "SMALLDATETIME", "SMALLINT", "SMALLMONEY",
        "SQL_VARIANT", "TEXT", "TIME", "TINYINT", "UNIQUEIDENTIFIER", "VARBINARY", "VARCHAR", "XML",
    };

    private enum State
    {
        Code,
        BlockComment,
        String,
        Bracketed,
        Quoted,
    }

    public static SqlTokenKind[][] Classify(IReadOnlyList<string> lines)
    {
        var result = new SqlTokenKind[lines.Count][];
        var state = State.Code;
        var commentDepth = 0;

        for (var row = 0; row < lines.Count; row++)
        {
            var line = lines[row];
            var kinds = result[row] = new SqlTokenKind[line.Length];
            var i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                switch (state)
                {
                    case State.BlockComment:
                        var close = At(line, i, "*/");
                        var open = !close && At(line, i, "/*");
                        var width = close || open ? 2 : 1;
                        Fill(kinds, i, i + width, SqlTokenKind.Comment);
                        i += width;
                        if (open) commentDepth++;
                        else if (close && --commentDepth == 0) state = State.Code;
                        continue;
                    case State.String:
                        kinds[i] = SqlTokenKind.String;
                        i = CloseQuoted(line, i, '\'', ref state, kinds, SqlTokenKind.String);
                        continue;
                    case State.Bracketed:
                        i = CloseQuoted(line, i, ']', ref state, kinds, SqlTokenKind.Plain);
                        continue;
                    case State.Quoted:
                        i = CloseQuoted(line, i, '"', ref state, kinds, SqlTokenKind.Plain);
                        continue;
                }

                var previous = i > 0 ? line[i - 1] : ' ';
                if (At(line, i, "--"))
                {
                    Fill(kinds, i, line.Length, SqlTokenKind.Comment);
                    i = line.Length;
                }
                else if (At(line, i, "/*"))
                {
                    Fill(kinds, i, i + 2, SqlTokenKind.Comment);
                    i += 2;
                    state = State.BlockComment;
                    commentDepth = 1;
                }
                else if (c == '\'' || (c is 'N' or 'n' && At(line, i + 1, "'") && !IsWordPart(previous)))
                {
                    var start = c == '\'' ? 1 : 2;
                    Fill(kinds, i, i + start, SqlTokenKind.String);
                    i += start;
                    state = State.String;
                }
                else if (c is '[' or '"')
                {
                    i++;
                    state = c == '[' ? State.Bracketed : State.Quoted;
                }
                else if (c == '@')
                {
                    var end = WordEnd(line, i + 1);
                    Fill(kinds, i, end, SqlTokenKind.Variable);
                    i = end;
                }
                else if (char.IsAsciiDigit(c) && !IsWordPart(previous))
                {
                    var end = i;
                    while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] == '.')) end++;
                    Fill(kinds, i, end, SqlTokenKind.Number);
                    i = end;
                }
                else if (IsWordPart(c))
                {
                    var end = WordEnd(line, i);
                    // After a dot the word is a member (t.date), never a keyword.
                    if (previous != '.' && Keywords.Contains(line[i..end])) Fill(kinds, i, end, SqlTokenKind.Keyword);
                    i = end;
                }
                else
                {
                    i++;
                }
            }
        }

        return result;
    }

    /// <summary>Advances inside a quoted run; a doubled delimiter is an escape, a single one ends the run.</summary>
    private static int CloseQuoted(string line, int i, char delimiter, ref State state, SqlTokenKind[] kinds, SqlTokenKind kind)
    {
        if (line[i] != delimiter) return i + 1;
        if (At(line, i + 1, delimiter.ToString()))
        {
            kinds[i + 1] = kind;
            return i + 2;
        }

        state = State.Code;
        return i + 1;
    }

    private static int WordEnd(string line, int start)
    {
        var end = start;
        while (end < line.Length && (IsWordPart(line[end]) || line[end] is '@' or '$')) end++;
        return end;
    }

    private static bool IsWordPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '#';

    private static bool At(string line, int index, string value) =>
        index >= 0 && index + value.Length <= line.Length && string.CompareOrdinal(line, index, value, 0, value.Length) == 0;

    private static void Fill(SqlTokenKind[] kinds, int start, int end, SqlTokenKind kind) =>
        kinds.AsSpan(start, end - start).Fill(kind);
}
