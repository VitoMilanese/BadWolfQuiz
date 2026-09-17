namespace BadWolfQuiz.Web.Services;

public enum PlayerAchievementCategory
{
    Quizzes,
    GuessWhatIPlay,
    WordRings,
    OutOfGame
}

public static class PlayerAchievementCategoryCatalog
{
    public static PlayerAchievementCategory GetCategory(string? achievementCode)
    {
        if (achievementCode?.StartsWith("WordRings", StringComparison.Ordinal) == true)
        {
            return PlayerAchievementCategory.WordRings;
        }

        return achievementCode switch
        {
            "SoloAi" or "RoomCreatorWin" => PlayerAchievementCategory.GuessWhatIPlay,
            "Registered" or
            "GitHubVisitor" or
            "PasswordChanged" or
            "DeveloperContacted" or
            "DeveloperReplied" or
            "Contributor" => PlayerAchievementCategory.OutOfGame,
            _ => PlayerAchievementCategory.Quizzes
        };
    }

    public static string GetFilterValue(string? achievementCode) =>
        GetCategory(achievementCode) switch
        {
            PlayerAchievementCategory.GuessWhatIPlay => "guess-what-i-play",
            PlayerAchievementCategory.WordRings => "word-rings",
            PlayerAchievementCategory.OutOfGame => "out-of-game",
            _ => "quizzes"
        };
}
