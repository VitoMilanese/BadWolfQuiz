using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public static class QuestionBuzzerPolicy
{
    public static QuestionBuzzerPolicyState Get(
        GameSession session,
        int sourceQuestionId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var question = session.Board.Questions.SingleOrDefault(item =>
            item.SourceQuestionId == sourceQuestionId)
            ?? throw new GameRuleViolationException(
                $"Question {sourceQuestionId} does not belong to this game.");

        if (question.IsSpecial || question.IsAllPlayerQuestion)
        {
            return new QuestionBuzzerPolicyState(
                QuestionBuzzerMode.Disabled,
                0,
                false);
        }

        var definition = session.Quiz.Rounds
            .SelectMany(round => round.Questions)
            .Single(item => item.SourceQuestionId == sourceQuestionId);

        var mode = definition.BuzzerMode == QuestionBuzzerMode.UseGameSetting
            ? session.Settings.RegularQuestionBuzzerStartMode ==
                GamePhaseStartMode.Automatic
                ? QuestionBuzzerMode.Immediately
                : QuestionBuzzerMode.Manual
            : definition.BuzzerMode;

        var initialBlocks = question.PresentationType ==
            QuestionPresentationType.FourClues
            ? definition.QuestionBlocks.Take(2)
            : definition.QuestionBlocks;
        var hasInitialMedia = initialBlocks.Any(block =>
            block.Kind is ContentBlockKind.Audio or
                ContentBlockKind.Video or
                ContentBlockKind.YouTube);

        return new QuestionBuzzerPolicyState(
            mode,
            Math.Max(0, definition.BuzzDelaySeconds),
            hasInitialMedia);
    }
}

public readonly record struct QuestionBuzzerPolicyState(
    QuestionBuzzerMode Mode,
    int DelaySeconds,
    bool HasInitialMedia);
