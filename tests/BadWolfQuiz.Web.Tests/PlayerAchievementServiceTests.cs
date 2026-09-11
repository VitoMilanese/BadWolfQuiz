using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerAchievementServiceTests
{
    [Fact]
    public void Catalog_contains_all_eighty_achievements()
    {
        Assert.Equal(80, PlayerAchievementService.Catalog.Count);

        var expectedCodes = new[]
        {
            "Registered",
            "OwnQuizWithOthers",
            "PublicQuizGuest",
            "SoloAi",
            "RoomCreatorWin",
            "GitHubVisitor",
            "QuizRated",
            "AllInCorrect",
            "AllInWrong",
            "PasswordChanged",
            "DeveloperContacted",
            "DeveloperReplied",
            "Contributor",
            "FirstPick",
            "SecondRoundFirstPick",
            "LastRoundComebackWin",
            "FinalLeaderZero",
            "EveryCategoryAttempt",
            "EveryCategoryCorrect",
            "SilentRound",
            "SilentRoundGain",
            "Score30K",
            "TotalScore100K",
            "TotalScore500K",
            "TotalScore1M",
            "AnonymousStake100Profit",
            "AnonymousStakeZeroSave",
            "FourCluesTwoClues",
            "BuzzerPhotoFinishFirst",
            "BuzzerPhotoFinishSecond",
            "KickedAndReturned",
            "FirstToThirdReturn",
            "LateJoiner",
            "AvatarChanged",
            "WebcamEnabled",
            "Films25",
            "Series25",
            "Cartoons25",
            "AnimatedSeries25",
            "Games25",
            "AudioQuestions25",
            "VideoQuestions25",
            "HarryPotter25",
            "StarWars25",
            "Fantasy25",
            "SciFi25",
            "Animals25",
            "Horrors25",
            "Music25",
            "DoctorWhoTag",
            "RobocopTag",
            "TerminatorTag",
            "MafiaGodfatherTag"
        };

        Assert.All(
            expectedCodes,
            code => Assert.Contains(
                PlayerAchievementService.Catalog,
                item => item.Code == code));

        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "PasswordChanged").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "DeveloperContacted").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "DeveloperReplied").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "Contributor").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "FirstPick").IsSecret);
        Assert.False(PlayerAchievementService.Catalog.Single(item => item.Code == "SecondRoundFirstPick").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "LastRoundComebackWin").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "FinalLeaderZero").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "EveryCategoryAttempt").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "EveryCategoryCorrect").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "SilentRound").IsSecret);
        Assert.True(PlayerAchievementService.Catalog.Single(item => item.Code == "SilentRoundGain").IsSecret);
        Assert.All(
            PlayerTagAchievementCatalog.Definitions.Where(item => item.Target == 25),
            item => Assert.False(item.IsSecret));
        Assert.All(
            PlayerTagAchievementCatalog.Definitions.Where(item => item.Target == 1),
            item => Assert.True(item.IsSecret));
    }

    [Theory]
    [InlineData("SuperChupa")]
    [InlineData("superchupa")]
    public async Task LoadForPlayerAsync_returns_every_achievement_unlocked_for_SuperChupa(string playerName)
    {
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var db = new QuizDbContext(options);
        var service = new PlayerAchievementService(db);

        var achievements = await service.LoadForPlayerAsync(
            "preview-host",
            playerName,
            currentGameCode: null);

        Assert.Equal(PlayerAchievementService.Catalog.Count, achievements.Count);
        Assert.All(achievements, achievement =>
        {
            Assert.True(achievement.IsUnlocked);
            Assert.False(achievement.IsNewInCurrentGame);
            Assert.Equal(achievement.Target, achievement.Progress);
        });
        Assert.Empty(db.ChangeTracker.Entries<PlayerAchievement>());
    }

    [Fact]
    public void Catalog_uses_fifteen_thousand_points_for_big_game()
    {
        var achievement = Assert.Single(
            PlayerAchievementService.Catalog,
            item => item.Code == "BigGame");

        Assert.Equal(15_000, achievement.Target);
        Assert.Equal(30_000, PlayerAchievementService.Catalog.Single(item => item.Code == "Score30K").Target);
        Assert.Equal(100_000, PlayerAchievementService.Catalog.Single(item => item.Code == "TotalScore100K").Target);
        Assert.Equal(500_000, PlayerAchievementService.Catalog.Single(item => item.Code == "TotalScore500K").Target);
        Assert.Equal(1_000_000, PlayerAchievementService.Catalog.Single(item => item.Code == "TotalScore1M").Target);
    }

    [Fact]
    public void BuildHistory_calculates_streak_flawless_win_and_recovery()
    {
        var now = new DateTime(2026, 9, 9, 18, 0, 0, DateTimeKind.Utc);
        PlayerAchievementGameSource[] appearances =
        [
            new(1, 10, "Wolf", 16_000, now),
            new(2, 11, "wolf", 1_000, now.AddHours(1))
        ];
        PlayerAchievementGameScoreSource[] scores =
        [
            new(10, 16_000),
            new(10, 15_000),
            new(11, 1_000),
            new(11, 2_000)
        ];
        var answers = new List<PlayerAchievementAnswerSource>
        {
            new(1, false, -500, now.AddMinutes(1)),
            new(1, true, 500, now.AddMinutes(2)),
            new(1, true, 500, now.AddMinutes(3)),
            new(1, true, 500, now.AddMinutes(4)),
            new(1, true, 500, now.AddMinutes(5)),
            new(1, true, 500, now.AddMinutes(6))
        };
        answers.AddRange(Enumerable.Range(0, 10).Select(index =>
            new PlayerAchievementAnswerSource(
                2,
                true,
                100,
                now.AddHours(1).AddMinutes(index + 1))));

        var history = PlayerAchievementService.BuildHistory(
            appearances,
            scores,
            answers);

        Assert.Equal(2, history.GamesPlayed);
        Assert.Equal(15, history.CorrectAnswers);
        Assert.Equal(1, history.Wins);
        Assert.Equal(10, history.BestCorrectStreak);
        Assert.Equal(16_000, history.BestFinalScore);
        Assert.Equal(17_000, history.TotalScore);
        Assert.Equal(10, history.BestFlawlessAttempts);
        Assert.True(history.RecoveredFromNegative);
    }

    [Fact]
    public void BuildHistory_counts_only_correct_tagged_answers()
    {
        var now = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);
        PlayerAchievementGameSource[] appearances = [new(1, 10, "Wolf", 0, now)];
        PlayerAchievementGameScoreSource[] scores = [new(10, 0)];
        var answers = Enumerable.Range(0, 26)
            .Select(index => new PlayerAchievementAnswerSource(
                1,
                index < 25,
                0,
                now.AddSeconds(index),
                index == 0
                    ? ["films 90", "Doctor Who"]
                    : ["ФІЛЬМИ 2000Х"]))
            .ToArray();

        var history = PlayerAchievementService.BuildHistory(appearances, scores, answers);
        var progress = PlayerAchievementService.BuildProgress(history, []);

        Assert.Equal(25, history.TaggedAnswers!["Films25"]);
        Assert.Equal(1, history.TaggedAnswers["DoctorWhoTag"]);
        Assert.Equal(25, progress.Single(item => item.Code == "Films25").Progress);
        Assert.Equal(1, progress.Single(item => item.Code == "DoctorWhoTag").Progress);
    }

    [Fact]
    public void Catalog_uses_media_metrics_for_audio_and_video_achievements()
    {
        var audio = PlayerAchievementService.Catalog.Single(item => item.Code == "AudioQuestions25");
        var video = PlayerAchievementService.Catalog.Single(item => item.Code == "VideoQuestions25");

        Assert.Equal(PlayerAchievementMetric.AudioQuestionAnswers, audio.Metric);
        Assert.Equal(PlayerAchievementMetric.VideoQuestionAnswers, video.Metric);
        Assert.Null(audio.TagGroup);
        Assert.Null(video.TagGroup);
    }

    [Fact]
    public void BuildHistory_counts_audio_and_video_blocks_instead_of_media_tags()
    {
        var now = new DateTime(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);
        PlayerAchievementGameSource[] appearances = [new(1, 10, "Wolf", 0, now)];
        PlayerAchievementGameScoreSource[] scores = [new(10, 0)];
        PlayerAchievementAnswerSource[] answers =
        [
            new(1, true, 0, now, ["audio question"], HasAudioBlock: true),
            new(1, true, 0, now.AddSeconds(1), ["video question"], HasVideoBlock: true),
            new(1, true, 0, now.AddSeconds(2), ["аудіопитання", "відеопитання"]),
            new(1, false, 0, now.AddSeconds(3), null, HasAudioBlock: true, HasVideoBlock: true),
            new(1, true, 0, now.AddSeconds(4), null, HasAudioBlock: true, HasVideoBlock: true)
        ];

        var history = PlayerAchievementService.BuildHistory(appearances, scores, answers);
        var progress = PlayerAchievementService.BuildProgress(history, []);

        Assert.Equal(2, history.AudioQuestionAnswers);
        Assert.Equal(2, history.VideoQuestionAnswers);
        Assert.False(history.TaggedAnswers!.ContainsKey("AudioQuestions25"));
        Assert.False(history.TaggedAnswers.ContainsKey("VideoQuestions25"));
        Assert.Equal(2, progress.Single(item => item.Code == "AudioQuestions25").Progress);
        Assert.Equal(2, progress.Single(item => item.Code == "VideoQuestions25").Progress);
    }

    [Fact]
    public async Task EvaluateCompletedGameAsync_persists_each_unlock_once()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = new HostAccount
        {
            Id = "host-achievements",
            Email = "host@example.test",
            NormalizedEmail = "HOST@EXAMPLE.TEST",
            PasswordHash = "test"
        };
        var quiz = new Quiz
        {
            Host = host,
            HostId = host.Id,
            Title = "Achievement Quiz"
        };
        var session = new GameSession
        {
            Quiz = quiz,
            Host = host,
            HostId = host.Id,
            PublicCode = "ACH001",
            Status = GameSessionStatus.Finished,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            FinishedAtUtc = DateTime.UtcNow
        };
        session.Players.Add(new GamePlayer
        {
            Name = "Rose",
            ReconnectToken = string.Empty,
            TotalScore = 15_000,
            IsActive = false
        });
        session.Players.Add(new GamePlayer
        {
            Name = "Mickey",
            ReconnectToken = string.Empty,
            TotalScore = 1_000,
            IsActive = false
        });
        session.Players.Add(new GamePlayer
        {
            Name = "Donna",
            ReconnectToken = string.Empty,
            TotalScore = 500,
            IsActive = false
        });
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.EvaluateCompletedGameAsync(session.Id);
        await service.EvaluateCompletedGameAsync(session.Id);

        var roseKey = PlayerAchievementService.NormalizePlayerKey("Rose");
        var rose = await db.PlayerAchievements
            .Where(item => item.HostId == host.Id && item.PlayerKey == roseKey)
            .ToListAsync();

        Assert.Contains(rose, item => item.AchievementCode == "FirstGame");
        Assert.Contains(rose, item => item.AchievementCode == "Winner");
        Assert.Contains(rose, item => item.AchievementCode == "BigGame");
        Assert.Equal(
            rose.Count,
            rose.Select(item => item.AchievementCode).Distinct().Count());
        Assert.All(rose, item => Assert.Equal(session.Id, item.SourceGameSessionId));

        await service.AdoptHostNicknameHistoryAsync(
            host.Id,
            host.Id,
            "Rose",
            session.Id);

        var accountProgress = await service.LoadForPlayerAsync(
            host.Id,
            "Rose",
            session.PublicCode,
            host.Id);
        var accountAchievements = await db.PlayerAchievements
            .Where(item => item.AccountId == host.Id)
            .ToListAsync();

        Assert.Contains(accountAchievements, item => item.AchievementCode == "Registered");
        Assert.Contains(accountAchievements, item => item.AchievementCode == "OwnQuizWithOthers");
        Assert.Contains(accountProgress, item => item.Code == "BigGame" && item.IsUnlocked);
        Assert.True(await db.PlayerGameAccountLinks
            .IgnoreQueryFilters()
            .AnyAsync(item => item.AccountId == host.Id && item.Player.Name == "Rose"));
    }

    [Fact]
    public async Task LoadForPlayerAsync_does_not_adopt_host_history_before_finished_game_confirmation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("host-delayed", "host-delayed@example.test");
        var account = CreateHost("account-delayed", "account-delayed@example.test");
        var quiz = CreateQuiz(host, "Delayed adoption");
        var previous = CreateStoredGame(
            quiz,
            host,
            "DELAY1",
            "Rose",
            1_000,
            GameSessionStatus.Finished,
            countsForAchievementHistory: true,
            DateTime.UtcNow.AddDays(-2));
        db.Hosts.AddRange(host, account);
        db.GameSessions.Add(previous);
        await db.SaveChangesAsync();
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = DateTime.UtcNow.AddDays(-2),
            SourceGameSessionId = previous.Id
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null,
            account.Id);

        Assert.False(await db.PlayerGameAccountLinks
            .AsNoTracking()
            .AnyAsync(link => link.AccountId == account.Id));
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == account.Id &&
                item.AchievementCode == "DoubleReward"));
    }

    [Fact]
    public async Task AdoptHostNicknameHistoryAsync_preserves_unlocks_and_skips_ineligible_or_unfinished_games()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-adopt", "host-adopt@example.test");
        var account = CreateHost("account-adopt", "account-adopt@example.test");
        var quiz = CreateQuiz(host, "Adoption");
        var eligible = CreateStoredGame(
            quiz, host, "ADOPT1", "Rose", 1_000, GameSessionStatus.Finished, true, now.AddDays(-30));
        var removed = CreateStoredGame(
            quiz, host, "ADOPT2", "Rose", 30_000, GameSessionStatus.Finished, false, now.AddDays(-20));
        var unfinished = CreateStoredGame(
            quiz, host, "ADOPT3", "Rose", 30_000, GameSessionStatus.Running, true, now.AddDays(-10));
        var confirmed = CreateStoredGame(
            quiz, host, "ADOPT4", "Rose", 2_000, GameSessionStatus.Finished, true, now);
        db.Hosts.AddRange(host, account);
        db.GameSessions.AddRange(eligible, removed, unfinished, confirmed);
        await db.SaveChangesAsync();

        var playerKey = PlayerAchievementService.NormalizePlayerKey("Rose");
        var originalUnlockAt = now.AddDays(-29);
        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = originalUnlockAt,
                SourceGameSessionId = eligible.Id
            },
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "AvatarChanged",
                UnlockedAtUtc = now.AddDays(-28),
                SourceGameSessionId = null
            },
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "HalfReward",
                UnlockedAtUtc = now.AddDays(-19),
                SourceGameSessionId = removed.Id
            },
            new PlayerAchievement
            {
                AccountId = account.Id,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = now.AddDays(-1),
                SourceGameSessionId = confirmed.Id
            });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.AdoptHostNicknameHistoryAsync(
            account.Id,
            host.Id,
            "Rose",
            confirmed.Id);

        var links = await db.PlayerGameAccountLinks
            .AsNoTracking()
            .Where(link => link.AccountId == account.Id)
            .Select(link => link.GamePlayerId)
            .ToListAsync();
        Assert.Contains(eligible.Players.Single().Id, links);
        Assert.Contains(confirmed.Players.Single().Id, links);
        Assert.DoesNotContain(removed.Players.Single().Id, links);
        Assert.DoesNotContain(unfinished.Players.Single().Id, links);

        var accountUnlocks = await db.PlayerAchievements
            .AsNoTracking()
            .Where(item => item.AccountId == account.Id)
            .ToListAsync();
        var doubleReward = accountUnlocks.Single(item => item.AchievementCode == "DoubleReward");
        Assert.Equal(originalUnlockAt, doubleReward.UnlockedAtUtc);
        Assert.Equal(eligible.Id, doubleReward.SourceGameSessionId);
        Assert.Contains(accountUnlocks, item => item.AchievementCode == "AvatarChanged");
        Assert.DoesNotContain(accountUnlocks, item => item.AchievementCode == "HalfReward");
    }

    [Fact]
    public async Task AdoptHostNicknameHistoryAsync_does_not_steal_history_claimed_by_another_account()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 11, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-conflict", "host-conflict@example.test");
        var firstAccount = CreateHost("account-first", "account-first@example.test");
        var secondAccount = CreateHost("account-second", "account-second@example.test");
        var quiz = CreateQuiz(host, "Conflicting adoption");
        var alreadyClaimed = CreateStoredGame(
            quiz, host, "CLAIM1", "Rose", 1_000, GameSessionStatus.Finished, true, now.AddDays(-10));
        var confirmed = CreateStoredGame(
            quiz, host, "CLAIM2", "Rose", 1_000, GameSessionStatus.Finished, true, now);
        db.Hosts.AddRange(host, firstAccount, secondAccount);
        db.GameSessions.AddRange(alreadyClaimed, confirmed);
        await db.SaveChangesAsync();
        db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
        {
            GamePlayerId = alreadyClaimed.Players.Single().Id,
            AccountId = firstAccount.Id
        });
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = now.AddDays(-9),
            SourceGameSessionId = alreadyClaimed.Id
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.AdoptHostNicknameHistoryAsync(
            secondAccount.Id,
            host.Id,
            "Rose",
            confirmed.Id);

        Assert.Equal(
            firstAccount.Id,
            await db.PlayerGameAccountLinks
                .Where(link => link.GamePlayerId == alreadyClaimed.Players.Single().Id)
                .Select(link => link.AccountId)
                .SingleAsync());
        Assert.Equal(
            secondAccount.Id,
            await db.PlayerGameAccountLinks
                .Where(link => link.GamePlayerId == confirmed.Players.Single().Id)
                .Select(link => link.AccountId)
                .SingleAsync());
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == secondAccount.Id &&
                item.AchievementCode == "DoubleReward"));
    }

    [Fact]
    public async Task LoadForPlayerAsync_ignores_ineligible_and_unfinished_game_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-filter", "host-filter@example.test");
        var quiz = CreateQuiz(host, "History filtering");
        var eligible = CreateStoredGame(
            quiz, host, "FILTER1", "Rose", 5_000, GameSessionStatus.Finished, true, now.AddDays(-3));
        var removed = CreateStoredGame(
            quiz, host, "FILTER2", "Rose", 30_000, GameSessionStatus.Finished, false, now.AddDays(-2));
        var unfinished = CreateStoredGame(
            quiz, host, "FILTER3", "Rose", 50_000, GameSessionStatus.Running, true, now.AddDays(-1));
        db.Hosts.Add(host);
        db.GameSessions.AddRange(eligible, removed, unfinished);
        await db.SaveChangesAsync();

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null);

        Assert.Equal(1, progress.Single(item => item.Code == "Regular").Progress);
        Assert.Equal(5_000, progress.Single(item => item.Code == "BigGame").Progress);
        Assert.Equal(5_000, progress.Single(item => item.Code == "TotalScore100K").Progress);
    }

    [Fact]
    public async Task Account_history_combines_eligible_finished_games_across_hosts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc);
        var account = CreateHost("account-global", "account-global@example.test");
        var hostA = CreateHost("host-global-a", "host-global-a@example.test");
        var hostB = CreateHost("host-global-b", "host-global-b@example.test");
        var quizA = CreateQuiz(hostA, "Host A");
        var quizB = CreateQuiz(hostB, "Host B");
        var gameA = CreateStoredGame(
            quizA, hostA, "GLOBAL1", "Rose", 10_000, GameSessionStatus.Finished, true, now.AddDays(-2));
        var gameB = CreateStoredGame(
            quizB, hostB, "GLOBAL2", "Rose", 20_000, GameSessionStatus.Finished, true, now.AddDays(-1));
        db.Hosts.AddRange(account, hostA, hostB);
        db.GameSessions.AddRange(gameA, gameB);
        await db.SaveChangesAsync();
        db.PlayerGameAccountLinks.AddRange(
            new PlayerGameAccountLink
            {
                GamePlayerId = gameA.Players.Single().Id,
                AccountId = account.Id
            },
            new PlayerGameAccountLink
            {
                GamePlayerId = gameB.Players.Single().Id,
                AccountId = account.Id
            });
        await db.SaveChangesAsync();

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            hostA.Id,
            "Rose",
            currentGameCode: null,
            account.Id);

        Assert.Equal(2, progress.Single(item => item.Code == "Regular").Progress);
        Assert.Equal(30_000, progress.Single(item => item.Code == "TotalScore100K").Progress);
        Assert.True(progress.Single(item => item.Code == "Score30K").IsUnlocked == false);
    }

    private static HostAccount CreateHost(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static Quiz CreateQuiz(HostAccount host, string title) => new()
    {
        Host = host,
        HostId = host.Id,
        Title = title
    };

    private static GameSession CreateStoredGame(
        Quiz quiz,
        HostAccount host,
        string publicCode,
        string playerName,
        int score,
        GameSessionStatus status,
        bool countsForAchievementHistory,
        DateTime timestamp)
    {
        var session = new GameSession
        {
            Quiz = quiz,
            Host = host,
            HostId = host.Id,
            PublicCode = publicCode,
            Status = status,
            CreatedAtUtc = timestamp.AddHours(-1),
            StartedAtUtc = timestamp.AddMinutes(-50),
            FinishedAtUtc = status == GameSessionStatus.Finished ? timestamp : null
        };
        session.Players.Add(new GamePlayer
        {
            Name = playerName,
            ReconnectToken = string.Empty,
            TotalScore = score,
            JoinedAtUtc = timestamp.AddMinutes(-45),
            LastSeenAtUtc = timestamp,
            IsActive = false,
            CountsForAchievementHistory = countsForAchievementHistory
        });
        return session;
    }

    [Fact]
    public void BuildProgress_marks_only_unlocks_from_current_game_as_new_and_sorts_unlocks_first()
    {
        var history = new PlayerAchievementHistory(
            GamesPlayed: 5,
            CorrectAnswers: 30,
            Wins: 1,
            BestCorrectStreak: 5,
            BestFinalScore: 15_000,
            BestFlawlessAttempts: 0,
            RecoveredFromNegative: false,
            TotalScore: 500_000);
        PlayerAchievement[] unlocked =
        [
            new()
            {
                AchievementCode = "FirstGame",
                HostId = "host",
                PlayerKey = "WOLF",
                SourceGameSessionId = 7
            },
            new()
            {
                AchievementCode = "BigGame",
                HostId = "host",
                PlayerKey = "WOLF",
                SourceGameSessionId = 8
            }
        ];

        var progress = PlayerAchievementService.BuildProgress(history, unlocked, 8);

        Assert.False(progress.Single(item => item.Code == "FirstGame").IsNewInCurrentGame);
        Assert.True(progress.Single(item => item.Code == "BigGame").IsNewInCurrentGame);
        Assert.All(progress.Take(2), item => Assert.True(item.IsUnlocked));
        Assert.All(progress.Skip(2), item => Assert.False(item.IsUnlocked));
        var secret = progress.Single(item => item.Code == "FromAbyss");
        Assert.True(secret.IsSecret);
        Assert.False(secret.IsUnlocked);
        Assert.Equal(500_000, progress.Single(item => item.Code == "TotalScore500K").Progress);
    }
}
