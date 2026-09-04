namespace BadWolfQuiz.Game.Definitions;

public static class MultipleChoiceQuestionExtensions
{
    public static IReadOnlyList<int> GetCorrectAnswerOptionIds(
        this QuizQuestionSnapshot question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (!MultipleChoiceAnswerContract.IsMultipleChoice(question.PresentationType))
        {
            return [];
        }

        return MultipleChoiceAnswerContract.Split(
                question.PresentationType,
                question.StoredAnswerBlocks)
            .CorrectOptionBlocks
            .Select(block => block.SourceContentBlockId)
            .ToArray();
    }
}