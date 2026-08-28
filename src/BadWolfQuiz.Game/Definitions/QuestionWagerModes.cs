namespace BadWolfQuiz.Game.Definitions;

public static class QuestionWagerModes
{
    // Stored through the existing QuestionPresentationType column so old quizzes
    // remain schema-compatible. Runtime/editor surfaces treat this as a wager mode,
    // not as a different content presentation contract.
    public const QuestionPresentationType AnonymousShared = (QuestionPresentationType)6;

    public static bool IsAnonymousShared(QuestionPresentationType presentationType) =>
        presentationType == AnonymousShared;
}
