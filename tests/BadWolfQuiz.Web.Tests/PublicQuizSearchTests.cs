using BadWolfQuiz.Web.Pages;

namespace BadWolfQuiz.Web.Tests;

public sealed class PublicQuizSearchTests
{
    [Theory]
    [InlineData("Marvel", "marvel")]
    [InlineData("Marvel", "MARVEL")]
    [InlineData("Україна", "уКрАїНа")]
    [InlineData("Cinema Italiano", "cInEmA iTaLiAnO")]
    public void MatchesSearch_ignores_title_case(string title, string search)
    {
        Assert.True(PublicQuizzesModel.MatchesSearch(title, null, search));
    }

    [Theory]
    [InlineData("Quiz about Marvel movies", "MARVEL")]
    [InlineData("Квіз про Україну", "уКрАїНу")]
    public void MatchesSearch_ignores_description_case(string description, string search)
    {
        Assert.True(PublicQuizzesModel.MatchesSearch("Other title", description, search));
    }

    [Fact]
    public void MatchesSearch_does_not_match_unrelated_text()
    {
        Assert.False(PublicQuizzesModel.MatchesSearch(
            "Marvel",
            "Superhero movies",
            "Star Wars"));
    }
}
