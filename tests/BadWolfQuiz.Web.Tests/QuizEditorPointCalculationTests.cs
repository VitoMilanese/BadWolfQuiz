using BadWolfQuiz.Web.Pages.Admin.Quizzes;

namespace BadWolfQuiz.Web.Tests;

public sealed class QuizEditorPointCalculationTests
{
    [Theory]
    [InlineData(1, 200)]
    [InlineData(2, 400)]
    [InlineData(3, 600)]
    [InlineData(4, 800)]
    public void CalculateDefaultPoints_uses_round_and_row_multipliers(
        int roundNumber,
        int firstRowPoints)
    {
        var expectedPoints = Enumerable.Range(1, 5)
            .Select(rowNumber => firstRowPoints * rowNumber);
        var actual = Enumerable.Range(1, 5)
            .Select(rowNumber => EditorModel.CalculateDefaultPoints(
                roundNumber,
                rowNumber));

        Assert.Equal(expectedPoints, actual);
    }
}
