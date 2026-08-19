using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Services;

public static class AllPlayerQuestionCompatibility
{
    public const string TextMode = "text";
    public const string MultipleChoiceMode = "multipleChoice";

    public static QuestionPresentationType ResolvePostedPresentationType(
        QuestionPresentationType presentationType,
        string? allPlayerMode) => allPlayerMode switch
        {
            TextMode => QuestionPresentationType.AllPlayerText,
            MultipleChoiceMode =>
                QuestionPresentationType.AllPlayerMultipleChoice,
            _ => presentationType
        };

    public static string? GetMode(
        QuestionPresentationType presentationType) => presentationType switch
        {
            QuestionPresentationType.AllPlayerText => TextMode,
            QuestionPresentationType.AllPlayerMultipleChoice =>
                MultipleChoiceMode,
            _ => null
        };

    public static QuestionPresentationType ResolveStoredPresentationType(
        QuizQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.PresentationType != QuestionPresentationType.Standard)
        {
            return question.PresentationType;
        }

        return LooksLikeLegacyImageMultipleChoice(question)
            ? QuestionPresentationType.AllPlayerMultipleChoice
            : QuestionPresentationType.Standard;
    }

    public static bool LooksLikeLegacyImageMultipleChoice(
        QuizQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.PresentationType != QuestionPresentationType.Standard ||
            question.IsSpecial ||
            !question.ExcludeFromRandomWagerSelection ||
            question.BuzzModeOverride != BuzzActivationMode.Disabled)
        {
            return false;
        }

        var answerBlocks = question.AnswerBlocks
            .OrderBy(block => block.SortOrder)
            .ToArray();

        return answerBlocks.Length is >= 2 and <= 4 &&
            answerBlocks.All(block =>
                block.BlockType == ContentBlockType.Image &&
                block.FileData is { Length: > 0 } &&
                !string.IsNullOrWhiteSpace(block.FileContentType));
    }
}
