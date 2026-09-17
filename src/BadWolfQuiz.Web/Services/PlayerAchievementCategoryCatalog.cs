namespace BadWolfQuiz.Web.Services;

public enum PlayerAchievementCategory
{
    Quizzes,
    GuessWhatIPlay,
    WordRings
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
            _ => PlayerAchievementCategory.Quizzes
        };
    }

    public static string GetFilterValue(string? achievementCode) =>
        GetCategory(achievementCode) switch
        {
            PlayerAchievementCategory.GuessWhatIPlay => "guess-what-i-play",
            PlayerAchievementCategory.WordRings => "word-rings",
            _ => "quizzes"
        };
}
