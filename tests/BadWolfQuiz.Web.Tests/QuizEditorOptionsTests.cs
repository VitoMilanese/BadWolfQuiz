using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorOptionsTests
{
    [Theory]
    [InlineData(3, 7, 6)]
    [InlineData(3, 4, 4)]
    [InlineData(7, 9, 7)]
    public void InitialCategoryCountClampsStandardCount(
        int minimum,
        int maximum,
        int expected)
    {
        var options = new QuizEditorOptions
        {
            MinimumCategoryCount = minimum,
            MaximumCategoryCount = maximum
        };

        Assert.Equal(expected, options.InitialCategoryCount);
    }

    [Theory]
    [InlineData(3, 10, 5)]
    [InlineData(2, 4, 4)]
    [InlineData(6, 10, 6)]
    public void InitialQuestionCountClampsStandardCount(
        int minimum,
        int maximum,
        int expected)
    {
        var options = new QuizEditorOptions
        {
            MinimumQuestionCount = minimum,
            MaximumQuestionCount = maximum
        };

        Assert.Equal(expected, options.InitialQuestionCount);
    }
}
