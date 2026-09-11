using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class HostCustomAchievementTests
{
    [Fact]
    public void NormalizeTags_is_case_insensitive_and_enforces_limits()
    {
        var tags = HostCustomAchievementService.NormalizeTags([" Geography ", "GEOGRAPHY", "Ukraine"]);

        Assert.Equal(2, tags.Count);
        Assert.Equal("GEOGRAPHY", tags[0].NormalizedName);
        Assert.Equal("UKRAINE", tags[1].NormalizedName);
        Assert.Throws<ArgumentException>(() => HostCustomAchievementService.NormalizeTags([]));
        Assert.Throws<ArgumentException>(() => HostCustomAchievementService.NormalizeTags(
            Enumerable.Range(1, 21).Select(index => $"tag-{index}")));
    }

    [Fact]
    public void Png_validation_requires_signature_and_exact_250_square_dimensions()
    {
        var png = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(png, 0);
        WriteBigEndian(png, 16, 250);
        WriteBigEndian(png, 20, 250);

        Assert.True(HostCustomAchievementService.HasPngSignature(png));
        Assert.True(HostCustomAchievementService.HasRequiredPngDimensions(png));

        WriteBigEndian(png, 20, 249);
        Assert.False(HostCustomAchievementService.HasRequiredPngDimensions(png));
        png[1] = 0;
        Assert.False(HostCustomAchievementService.HasPngSignature(png));
    }

    [Fact]
    public async Task Progress_is_scoped_to_creator_host_and_unlock_is_not_duplicated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var hostA = CreateHost("custom-a");
        var hostB = CreateHost("custom-b");
        var questionA = AddQuiz(hostA, "A", "GEOGRAPHY");
        var questionB = AddQuiz(hostB, "B", "GEOGRAPHY");
        db.Hosts.AddRange(hostA, hostB);

        var definition = new HostCustomAchievement
        {
            HostId = hostA.Id,
            Name = "Geographer",
            Description = "Answer geography questions",
            Target = 2,
            ArtworkPng = [1, 2, 3]
        };
        definition.Tags.Add(new HostCustomAchievementTag
        {
            Name = "geography",
            NormalizedName = "GEOGRAPHY"
        });
        db.HostCustomAchievements.Add(definition);

        AddFinishedGame(hostA, questionA, "A001", "Rose", correctAnswers: 2);
        AddFinishedGame(hostB, questionB, "B001", "Rose", correctAnswers: 5);
        await db.SaveChangesAsync();

        var service = new HostCustomAchievementService(db);
        var first = await service.LoadForPlayerAsync(hostA.Id, "Rose", null, null);
        var second = await service.LoadForPlayerAsync(hostA.Id, "Rose", null, null);

        var achievement = Assert.Single(first);
        Assert.True(achievement.IsUnlocked);
        Assert.Equal(2, achievement.Progress);
        Assert.Single(second);
        Assert.Single(await db.PlayerAchievements
            .Where(item => item.AchievementCode == HostCustomAchievementService.BuildCode(definition.Id))
            .ToListAsync());
    }

    [Fact]
    public async Task Deleted_definition_keeps_existing_unlock_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("custom-delete");
        db.Hosts.Add(host);
        var definition = new HostCustomAchievement
        {
            HostId = host.Id,
            Name = "Persistent",
            Description = "History survives deletion",
            Target = 1,
            ArtworkPng = [1]
        };
        definition.Tags.Add(new HostCustomAchievementTag { Name = "tag", NormalizedName = "TAG" });
        db.HostCustomAchievements.Add(definition);
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = HostCustomAchievementService.BuildCode(definition.Id),
            SourceGameSessionId = null
        });
        await db.SaveChangesAsync();

        await new HostCustomAchievementService(db).DeleteAsync(definition.Id, host.Id);

        Assert.True((await db.HostCustomAchievements.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.Single(await db.PlayerAchievements.ToListAsync());
        var historical = await new HostCustomAchievementService(db)
            .LoadDefinitionByCodeAsync(HostCustomAchievementService.BuildCode(definition.Id));
        Assert.NotNull(historical);
        Assert.True(historical!.IsDeleted);
    }

    private static HostAccount CreateHost(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.test",
        NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static QuizQuestion AddQuiz(HostAccount host, string title, string normalizedTag)
    {
        var quiz = new Quiz { HostId = host.Id, Host = host, Title = title };
        var round = new QuizRound { Title = "Round", SortOrder = 1 };
        round.Rows.Add(new QuizRoundRow { RowIndex = 1, Points = 100 });
        var category = new QuizCategory { Title = "Category", SortOrder = 1 };
        var question = new QuizQuestion { RowIndex = 1 };
        question.Tags.Add(new QuizQuestionTag { Name = normalizedTag, NormalizedName = normalizedTag });
        category.Questions.Add(question);
        round.Categories.Add(category);
        quiz.Rounds.Add(round);
        host.Quizzes.Add(quiz);
        return question;
    }

    private static void AddFinishedGame(
        HostAccount host,
        QuizQuestion question,
        string code,
        string playerName,
        int correctAnswers)
    {
        var quiz = host.Quizzes.Single(item => item.Rounds.SelectMany(round => round.Categories).SelectMany(category => category.Questions).Contains(question));
        for (var index = 0; index < correctAnswers; index++)
        {
            var session = new GameSession
            {
                Quiz = quiz,
                Host = host,
                HostId = host.Id,
                PublicCode = $"{code}{index}",
                Status = GameSessionStatus.Finished,
                FinishedAtUtc = DateTime.UtcNow.AddMinutes(index)
            };
            var player = new GamePlayer
            {
                Name = playerName,
                ReconnectToken = string.Empty,
                CountsForAchievementHistory = true
            };
            session.Players.Add(player);
            var gameQuestion = new GameQuestion
            {
                QuizQuestion = question,
                QuizQuestionId = question.Id,
                Status = GameQuestionStatus.Finished
            };
            gameQuestion.Results.Add(new PlayerQuestionResult
            {
                Player = player,
                IsCorrect = true,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(index)
            });
            session.Questions.Add(gameQuestion);
            host.GameSessions.Add(session);
        }
    }

    private static void WriteBigEndian(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }
}
