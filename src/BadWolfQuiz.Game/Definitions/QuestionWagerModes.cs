namespace BadWolfQuiz.Game.Definitions;

public enum QuestionWagerMode
{
    Normal = 0,
    AnonymousShared = 1
}

public static class QuestionWagerModes
{
    // Stored through the existing QuestionPresentationType column so existing quiz
    // databases do not need a per-question wager-mode migration. UI code normalizes
    // this marker back to Standard and exposes it through QuestionWagerMode instead.
    public const QuestionPresentationType AnonymousShared = (QuestionPresentationType)6;

    public static bool IsAnonymousShared(QuestionPresentationType presentationType) =>
        presentationType == AnonymousShared;

    public static QuestionWagerMode GetMode(QuestionPresentationType presentationType) =>
        IsAnonymousShared(presentationType)
            ? QuestionWagerMode.AnonymousShared
            : QuestionWagerMode.Normal;

    public static QuestionPresentationType GetContentPresentationType(
        QuestionPresentationType presentationType) =>
        IsAnonymousShared(presentationType)
            ? QuestionPresentationType.Standard
            : presentationType;

    public static QuestionPresentationType Apply(
        QuestionPresentationType presentationType,
        QuestionWagerMode wagerMode) =>
        wagerMode == QuestionWagerMode.AnonymousShared &&
        presentationType == QuestionPresentationType.Standard
            ? AnonymousShared
            : presentationType;
}
