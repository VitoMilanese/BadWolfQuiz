using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameAnswerFileParserTests
{
    [Fact]
    public void Blank_lines_are_unassigned_answers()
    {
        var result = MinigameAnswerFileParser.Parse("1\n\n0\n", 3);

        Assert.True(result.Success);
        Assert.Equal(new bool?[] { true, null, false }, result.Answers);
    }

    [Fact]
    public void A_final_blank_answer_is_preserved_when_the_file_has_a_trailing_separator()
    {
        var result = MinigameAnswerFileParser.Parse("1\n0\n\n", 3);

        Assert.True(result.Success);
        Assert.Equal(new bool?[] { true, false, null }, result.Answers);
    }

    [Theory]
    [InlineData("1\n0\n1\n", 3, true, 0)]
    [InlineData("1\n \n0\n", 3, true, 0)]
    [InlineData("1\n2\n0\n", 3, false, 2)]
    [InlineData("1\n0\n", 3, false, 0)]
    public void Parser_accepts_only_one_zero_or_blank_per_question(
        string content,
        int questionCount,
        bool expectedSuccess,
        int expectedInvalidLine)
    {
        var result = MinigameAnswerFileParser.Parse(content, questionCount);

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(expectedInvalidLine, result.InvalidLineNumber);
    }
}
