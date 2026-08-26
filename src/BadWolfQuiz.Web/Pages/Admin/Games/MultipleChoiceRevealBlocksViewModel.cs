using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed record MultipleChoiceRevealBlocksViewModel(
    Guid GameSessionId,
    int SourceQuestionId,
    QuestionPresentationType PresentationType,
    IReadOnlyList<ContentBlockSnapshot> Blocks);
