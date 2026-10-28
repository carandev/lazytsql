using LazyTsql.Core.Syntax;

namespace LazyTsql.Core.Tests;

public class SqlTokenizerTests
{
    // One letter per character: P plain, K keyword, S string, C comment, N number, V variable.
    private static string[] Kinds(params string[] lines) =>
        SqlTokenizer.Classify(lines).Select(line => new string(line.Select(kind => "PKSCNV"[(int)kind]).ToArray())).ToArray();

    [Fact]
    public void Keywords_are_case_insensitive_and_identifiers_stay_plain()
    {
        Assert.Equal(["KKKKKKPNPKKKKPPPPPPPPPPP"], Kinds("select 1 FROM dbo.users;"));
    }

    [Fact]
    public void A_word_after_a_dot_is_a_member_name_not_a_keyword()
    {
        Assert.Equal(["PPPPPPPKKKK"], Kinds("a.date date"));
    }

    [Fact]
    public void Strings_keep_escaped_quotes_and_the_N_prefix()
    {
        Assert.Equal(["KKKKKKPSSSSSSSSSSSSSSSPPN"], Kinds("SELECT N'it''s select', 1"));
    }

    [Fact]
    public void Comments_nest_and_span_lines()
    {
        Assert.Equal(
            ["KKKKKKPCCCCCCCCC", "CCCCCCCCCCPN", "CCCCCCCCCCC"],
            Kinds("SELECT /* a /* b", "*/ from */ 1", "-- select 1"));
    }

    [Fact]
    public void Bracketed_and_quoted_identifiers_are_plain_even_when_they_are_keywords()
    {
        Assert.Equal(["KKKKKKPPPPPPPPPPPPPPPPPPPP"], Kinds("SELECT [select]]x], \"from\""));
    }

    [Fact]
    public void Numbers_and_variables_are_recognized()
    {
        Assert.Equal(["KKKKKKKPVVPNPNNNNPNNNNPVVVVVVVVVV"], Kinds("DECLARE @x=1+3.14+0xFF+@@ROWCOUNT"));
    }

    [Fact]
    public void An_unterminated_string_runs_to_the_end_of_the_script()
    {
        Assert.Equal(["KKKKKKPSSSS", "SSSS"], Kinds("SELECT 'abc", "from"));
    }
}
