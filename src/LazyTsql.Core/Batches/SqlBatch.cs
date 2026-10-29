using LazyTsql.Core.Completion;
using LazyTsql.Core.Syntax;

namespace LazyTsql.Core.Batches;

/// <summary>A batch of T-SQL sent to the server in one round trip.</summary>
/// <param name="StartLine">1-based line in the original script, used to map server error lines back to the editor.</param>
public sealed record SqlBatch(string Text, int StartLine, int RepeatCount = 1)
{
    // Words that start a statement and cannot appear inside a SELECT (FETCH and END can, in OFFSET and CASE).
    private static readonly HashSet<string> StatementWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "MERGE", "EXEC", "EXECUTE", "CREATE", "ALTER", "DROP", "TRUNCATE", "DECLARE", "SET",
        "IF", "WHILE", "BEGIN", "PRINT", "RAISERROR", "THROW", "USE", "GRANT", "REVOKE", "DENY", "WAITFOR", "RETURN", "GOTO",
        "BREAK", "CONTINUE", "COMMIT", "ROLLBACK", "SAVE", "BACKUP", "RESTORE", "DBCC", "KILL", "OPEN", "CLOSE", "DEALLOCATE",
        "BULK", "CHECKPOINT", "RECONFIGURE", "SHUTDOWN", "INTO",
    };

    private static readonly HashSet<string> SetOperators = new(StringComparer.OrdinalIgnoreCase) { "UNION", "ALL", "EXCEPT", "INTERSECT" };

    /// <summary>
    /// Whether the batch is one SELECT and nothing else, so stopping the server once the row limit is reached skips no
    /// other statement. Anything it cannot tell is false: the rows past the limit are then read and discarded.
    /// </summary>
    public bool IsSingleQuery
    {
        get
        {
            var lines = Text.ReplaceLineEndings("\n").Split('\n');
            var tokens = SqlLexer.Tokenize(lines, SqlTokenizer.Classify(lines));
            var words = tokens.FindAll(t => t.Type is TokenType.Word or TokenType.OpenParen or TokenType.Separator);
            if (words.Count == 0 || words[0] is not { Type: TokenType.Word, Bracketed: false } first || !IsWord(first, "SELECT")) return false;

            for (var i = 1; i < words.Count; i++)
            {
                // A trailing semicolon ends the statement; anything after one is another statement.
                if (words[i].Type == TokenType.Separator) return i == words.Count - 1;
                if (words[i] is not { Type: TokenType.Word, Bracketed: false } word) continue;
                if (StatementWords.Contains(word.Text)) return false;
                // Another SELECT is a subquery or a set operand only right after "(" or UNION/EXCEPT/INTERSECT [ALL].
                var previous = words[i - 1];
                var nested = previous.Type == TokenType.OpenParen || (previous is { Type: TokenType.Word, Bracketed: false } && SetOperators.Contains(previous.Text));
                if (IsWord(word, "SELECT") && !nested) return false;
            }

            return true;
        }
    }

    private static bool IsWord(Token token, string word) => token.Text.Equals(word, StringComparison.OrdinalIgnoreCase);
}
