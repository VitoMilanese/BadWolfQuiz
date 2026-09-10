using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerTagAchievementCatalogTests
{
    [Theory]
    [InlineData("Films25", "фільми")]
    [InlineData("Films25", "фільми до 90х")]
    [InlineData("Films25", "фільми 90х")]
    [InlineData("Films25", "фільми 2000х")]
    [InlineData("Films25", "фільми 2010х")]
    [InlineData("Films25", "фільми 2020х")]
    [InlineData("Films25", "films")]
    [InlineData("Films25", "films before 90")]
    [InlineData("Films25", "films 90")]
    [InlineData("Films25", "films 2000")]
    [InlineData("Films25", "films 2010")]
    [InlineData("Films25", "films 2020")]
    [InlineData("Series25", "серіали")]
    [InlineData("Series25", "серіали до 90х")]
    [InlineData("Series25", "серіали 90х")]
    [InlineData("Series25", "серіали 2000х")]
    [InlineData("Series25", "серіали 2010х")]
    [InlineData("Series25", "серіали 2020х")]
    [InlineData("Series25", "series")]
    [InlineData("Series25", "series before 90")]
    [InlineData("Series25", "series 90")]
    [InlineData("Series25", "series 2000")]
    [InlineData("Series25", "series 2010")]
    [InlineData("Series25", "series 2020")]
    [InlineData("Cartoons25", "мультфільми")]
    [InlineData("Cartoons25", "мультфільми до 90х")]
    [InlineData("Cartoons25", "мультфільми 90х")]
    [InlineData("Cartoons25", "мультфільми 2000х")]
    [InlineData("Cartoons25", "мультфільми 2010х")]
    [InlineData("Cartoons25", "мультфільми 2020х")]
    [InlineData("Cartoons25", "cartoons")]
    [InlineData("Cartoons25", "cartoons before 90")]
    [InlineData("Cartoons25", "cartoons 90")]
    [InlineData("Cartoons25", "cartoons 2000")]
    [InlineData("Cartoons25", "cartoons 2010")]
    [InlineData("Cartoons25", "cartoons 2020")]
    [InlineData("AnimatedSeries25", "мультсеріали")]
    [InlineData("AnimatedSeries25", "мультсеріали до 90х")]
    [InlineData("AnimatedSeries25", "мультсеріали 90х")]
    [InlineData("AnimatedSeries25", "мультсеріали 2000х")]
    [InlineData("AnimatedSeries25", "мультсеріали 2010х")]
    [InlineData("AnimatedSeries25", "мультсеріали 2020х")]
    [InlineData("AnimatedSeries25", "animated series")]
    [InlineData("AnimatedSeries25", "animated series before 90")]
    [InlineData("AnimatedSeries25", "animated series 90")]
    [InlineData("AnimatedSeries25", "animated series 2000")]
    [InlineData("AnimatedSeries25", "animated series 2010")]
    [InlineData("AnimatedSeries25", "animated series 2020")]
    [InlineData("Games25", "ігри")]
    [InlineData("Games25", "ретро-ігри")]
    [InlineData("Games25", "games")]
    [InlineData("Games25", "retro-games")]
    [InlineData("HarryPotter25", "Гаррі Поттер")]
    [InlineData("HarryPotter25", "Harry Potter")]
    [InlineData("StarWars25", "Зоряні Війни")]
    [InlineData("StarWars25", "Star Wars")]
    [InlineData("Fantasy25", "фантастика")]
    [InlineData("Fantasy25", "fantasy")]
    [InlineData("SciFi25", "наукова фантастика")]
    [InlineData("SciFi25", "sci-fi")]
    [InlineData("Animals25", "тварини")]
    [InlineData("Animals25", "animals")]
    [InlineData("Horrors25", "жахи")]
    [InlineData("Horrors25", "horrors")]
    [InlineData("Music25", "музика")]
    [InlineData("Music25", "music")]
    [InlineData("DoctorWhoTag", "Доктор Хто")]
    [InlineData("DoctorWhoTag", "Doctor Who")]
    [InlineData("RobocopTag", "Робокоп")]
    [InlineData("RobocopTag", "Robocop")]
    [InlineData("TerminatorTag", "Термінатор")]
    [InlineData("TerminatorTag", "Terminator")]
    [InlineData("MafiaGodfatherTag", "Мафія")]
    [InlineData("MafiaGodfatherTag", "Mafia")]
    [InlineData("MafiaGodfatherTag", "Хрещений Батько")]
    [InlineData("MafiaGodfatherTag", "Godfather")]
    public void Every_requested_tag_matches_its_achievement_group(string group, string tag)
    {
        var answer = new PlayerAchievementAnswerSource(
            1,
            false,
            0,
            DateTime.UtcNow,
            [$"  {tag.ToLowerInvariant()}  "]);

        var counts = PlayerTagAchievementCatalog.CountAnswers([answer]);

        Assert.Equal(1, counts[group]);
    }

    [Fact]
    public void One_answer_counts_only_once_per_group_even_with_multiple_matching_tags()
    {
        var answer = new PlayerAchievementAnswerSource(
            1,
            true,
            0,
            DateTime.UtcNow,
            ["films", "ФІЛЬМИ 90Х", "films 2000"]);

        var counts = PlayerTagAchievementCatalog.CountAnswers([answer]);

        Assert.Equal(1, counts["Films25"]);
    }
}
