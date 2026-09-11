namespace BadWolfQuiz.Web.Services;

public static class PlayerTagAchievementCatalog
{
    public static IReadOnlyList<PlayerAchievementDefinition> Definitions { get; } =
    [
        new("Films25", "🎬", false, PlayerAchievementMetric.TaggedAnswers, 25, "Films25"),
        new("Series25", "📺", false, PlayerAchievementMetric.TaggedAnswers, 25, "Series25"),
        new("Cartoons25", "🧸", false, PlayerAchievementMetric.TaggedAnswers, 25, "Cartoons25"),
        new("AnimatedSeries25", "🌀", false, PlayerAchievementMetric.TaggedAnswers, 25, "AnimatedSeries25"),
        new("Games25", "🎮", false, PlayerAchievementMetric.TaggedAnswers, 25, "Games25"),
        new("AudioQuestions25", "🎧", false, PlayerAchievementMetric.AudioQuestionAnswers, 25),
        new("VideoQuestions25", "🎥", false, PlayerAchievementMetric.VideoQuestionAnswers, 25),
        new("HarryPotter25", "⚡", false, PlayerAchievementMetric.TaggedAnswers, 25, "HarryPotter25"),
        new("StarWars25", "🌌", false, PlayerAchievementMetric.TaggedAnswers, 25, "StarWars25"),
        new("Fantasy25", "🐉", false, PlayerAchievementMetric.TaggedAnswers, 25, "Fantasy25"),
        new("SciFi25", "🚀", false, PlayerAchievementMetric.TaggedAnswers, 25, "SciFi25"),
        new("Animals25", "🐾", false, PlayerAchievementMetric.TaggedAnswers, 25, "Animals25"),
        new("Horrors25", "👻", false, PlayerAchievementMetric.TaggedAnswers, 25, "Horrors25"),
        new("Music25", "🎵", false, PlayerAchievementMetric.TaggedAnswers, 25, "Music25"),
        new("DoctorWhoTag", "🕰️", true, PlayerAchievementMetric.TaggedAnswers, 1, "DoctorWhoTag"),
        new("RobocopTag", "🤖", true, PlayerAchievementMetric.TaggedAnswers, 1, "RobocopTag"),
        new("TerminatorTag", "🦾", true, PlayerAchievementMetric.TaggedAnswers, 1, "TerminatorTag"),
        new("MafiaGodfatherTag", "🤵", true, PlayerAchievementMetric.TaggedAnswers, 1, "MafiaGodfatherTag")
    ];

    private static readonly IReadOnlyDictionary<string, HashSet<string>> TagGroups =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Films25"] = Tags(
                "фільми", "фільми до 90х", "фільми 90х", "фільми 2000х", "фільми 2010х", "фільми 2020х",
                "films", "films before 90", "films 90", "films 2000", "films 2010", "films 2020"),
            ["Series25"] = Tags(
                "серіали", "серіали до 90х", "серіали 90х", "серіали 2000х", "серіали 2010х", "серіали 2020х",
                "series", "series before 90", "series 90", "series 2000", "series 2010", "series 2020"),
            ["Cartoons25"] = Tags(
                "мультфільми", "мультфільми до 90х", "мультфільми 90х", "мультфільми 2000х", "мультфільми 2010х", "мультфільми 2020х",
                "cartoons", "cartoons before 90", "cartoons 90", "cartoons 2000", "cartoons 2010", "cartoons 2020"),
            ["AnimatedSeries25"] = Tags(
                "мультсеріали", "мультсеріали до 90х", "мультсеріали 90х", "мультсеріали 2000х", "мультсеріали 2010х", "мультсеріали 2020х",
                "animated series", "animated series before 90", "animated series 90", "animated series 2000", "animated series 2010", "animated series 2020"),
            ["Games25"] = Tags("ігри", "ретро-ігри", "games", "retro-games"),
            ["HarryPotter25"] = Tags("Гаррі Поттер", "Harry Potter"),
            ["StarWars25"] = Tags("Зоряні Війни", "Star Wars"),
            ["Fantasy25"] = Tags("фантастика", "fantasy"),
            ["SciFi25"] = Tags("наукова фантастика", "sci-fi"),
            ["Animals25"] = Tags("тварини", "animals"),
            ["Horrors25"] = Tags("жахи", "horrors"),
            ["Music25"] = Tags("музика", "music"),
            ["DoctorWhoTag"] = Tags("Доктор Хто", "Doctor Who"),
            ["RobocopTag"] = Tags("Робокоп", "Robocop"),
            ["TerminatorTag"] = Tags("Термінатор", "Terminator"),
            ["MafiaGodfatherTag"] = Tags("Мафія", "Mafia", "Хрещений Батько", "Godfather")
        };

    public static IReadOnlyDictionary<string, int> CountAnswers(
        IEnumerable<PlayerAchievementAnswerSource> answers)
    {
        var counts = TagGroups.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            if (answer.IsCorrect != true || answer.Tags is null || answer.Tags.Count == 0)
            {
                continue;
            }

            var normalizedTags = answer.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(NormalizeTag)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var group in TagGroups)
            {
                if (normalizedTags.Overlaps(group.Value))
                {
                    counts[group.Key]++;
                }
            }
        }

        return counts;
    }

    public static int GetCount(PlayerAchievementHistory history, string? tagGroup)
    {
        if (string.IsNullOrWhiteSpace(tagGroup) || history.TaggedAnswers is null)
        {
            return 0;
        }

        return history.TaggedAnswers.TryGetValue(tagGroup, out var count) ? count : 0;
    }

    private static HashSet<string> Tags(params string[] values) =>
        values.Select(NormalizeTag).ToHashSet(StringComparer.Ordinal);

    private static string NormalizeTag(string value) =>
        value.Trim().ToUpperInvariant();
}
