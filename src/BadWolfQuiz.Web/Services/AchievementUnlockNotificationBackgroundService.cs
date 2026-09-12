using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using RuntimeGamePlayer = BadWolfQuiz.Game.Runtime.GamePlayer;
using RuntimeGameSessionStatus = BadWolfQuiz.Game.Runtime.GameSessionStatus;
using StoredGameSessionStatus = BadWolfQuiz.Web.Models.GameSessionStatus;

namespace BadWolfQuiz.Web.Services;

/// <summary>
/// Evaluates achievements while a game is running and publishes transient unlock notifications.
/// Persisted achievement rows remain authoritative; notification state only prevents replay.
/// </summary>
public sealed class AchievementUnlockNotificationBackgroundService(
    IDbContextFactory<QuizDbContext> dbFactory,
    GameSessionRegistry sessions,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<AchievementResource> localizer,
    ILogger<AchievementUnlockNotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(650);
    private readonly Dictionary<Guid, string> stateSignatures = [];
    private readonly HashSet<Guid> completedGamesHandled = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EvaluateAllAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateAllAsync(stoppingToken);
        }
    }

    private async Task EvaluateAllAsync(CancellationToken cancellationToken)
    {
        var games = sessions.GetAll().ToArray();
        var liveIds = games.Select(game => game.Session.Id.Value).ToHashSet();

        foreach (var staleId in stateSignatures.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            stateSignatures.Remove(staleId);
            completedGamesHandled.Remove(staleId);
        }

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = game.Session.Status;
            if (status == RuntimeGameSessionStatus.Lobby)
            {
                continue;
            }

            var gameId = game.Session.Id.Value;
            if (status == RuntimeGameSessionStatus.Completed &&
                completedGamesHandled.Contains(gameId))
            {
                continue;
            }

            string signature;
            try
            {
                signature = BuildStateSignature(game);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (status != RuntimeGameSessionStatus.Completed &&
                stateSignatures.TryGetValue(gameId, out var previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var completed = await EvaluateGameAsync(game, cancellationToken);
                stateSignatures[gameId] = signature;
                if (completed)
                {
                    completedGamesHandled.Add(gameId);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Achievement notification evaluation failed. GameCode={GameCode} GameId={GameId}",
                    game.PublicCode,
                    gameId);
            }
        }
    }

    private async Task<bool> EvaluateGameAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var runtime = game.Session;
        var isCompleted = runtime.Status == RuntimeGameSessionStatus.Completed;

        var storedGameSessionId = await db.GameSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(session =>
                session.PublicCode == game.PublicCode &&
                (!isCompleted || session.Status == StoredGameSessionStatus.Finished))
            .OrderByDescending(session => session.Id)
            .Select(session => (int?)session.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (isCompleted && !storedGameSessionId.HasValue)
        {
            // Runtime completion can precede the GameHistoryStore transaction by a moment.
            // Retry instead of consuming the transient notification opportunity.
            return false;
        }

        var runtimePlayers = runtime.Players.ToArray();
        var runtimeQuestions = runtime.Board.Questions.ToArray();
        var questionTags = isCompleted
            ? new Dictionary<int, HashSet<string>>()
            : await LoadQuestionTagsAsync(db, runtimeQuestions, cancellationToken);

        var standardService = new PlayerAchievementService(db);
        var customService = new HostCustomAchievementService(db);
        IReadOnlyList<HostCustomAchievement> customDefinitions =
            string.IsNullOrWhiteSpace(game.HostId)
                ? []
                : await customService.LoadDefinitionsAsync(game.HostId, cancellationToken);

        foreach (var player in runtimePlayers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id);

            if (isCompleted)
            {
                await BackfillLiveUnlockSourcesAsync(
                    db,
                    game,
                    player,
                    accountId,
                    storedGameSessionId!.Value,
                    cancellationToken);

                var persisted = await standardService.LoadForPlayerAsync(
                    game.HostId,
                    player.Name,
                    game.PublicCode,
                    accountId,
                    isContributor: false,
                    cancellationToken);
                foreach (var achievement in persisted.Where(item => item.IsNewInCurrentGame))
                {
                    await PublishBuiltInAsync(game, player, achievement.Code, cancellationToken);
                }

                var custom = await customService.LoadForPlayerAsync(
                    game.HostId,
                    player.Name,
                    game.PublicCode,
                    accountId,
                    cancellationToken);
                foreach (var achievement in custom.Where(item => item.IsNewInCurrentGame))
                {
                    await PublishCustomAsync(game, player, achievement, cancellationToken);
                }

                continue;
            }

            var progress = await standardService.LoadForPlayerAsync(
                game.HostId,
                player.Name,
                currentGameCode: null,
                accountId,
                isContributor: false,
                cancellationToken);
            var progressByCode = progress.ToDictionary(item => item.Code, StringComparer.Ordinal);
            var answerStats = BuildCurrentAnswerStats(player.Id, runtimeQuestions, questionTags);

            foreach (var definition in PlayerAchievementService.Catalog)
            {
                if (!progressByCode.TryGetValue(definition.Code, out var current) || current.IsUnlocked)
                {
                    continue;
                }

                var value = GetLiveMetricValue(definition, current, answerStats);
                if (value < definition.Target)
                {
                    continue;
                }

                var changed = await standardService.UnlockPlayerAsync(
                    accountId,
                    game.HostId,
                    player.Name,
                    definition.Code,
                    sourceGameSessionId: null,
                    cancellationToken);
                if (changed)
                {
                    await PublishBuiltInAsync(game, player, definition.Code, cancellationToken);
                }
            }

            foreach (var code in GetLiveDirectUnlockCodes(game, player, runtimeQuestions))
            {
                var changed = await standardService.UnlockPlayerAsync(
                    accountId,
                    game.HostId,
                    player.Name,
                    code,
                    sourceGameSessionId: null,
                    cancellationToken);
                if (changed)
                {
                    await PublishBuiltInAsync(game, player, code, cancellationToken);
                }
            }

            if (customDefinitions.Count == 0)
            {
                continue;
            }

            var customProgress = await customService.LoadForPlayerAsync(
                game.HostId,
                player.Name,
                currentGameCode: null,
                accountId,
                cancellationToken);
            var customProgressById = customProgress.ToDictionary(item => item.Id);

            foreach (var definition in customDefinitions)
            {
                if (!customProgressById.TryGetValue(definition.Id, out var current) ||
                    current.IsUnlocked)
                {
                    continue;
                }

                var definitionTags = definition.Tags
                    .Select(tag => tag.NormalizedName)
                    .ToHashSet(StringComparer.Ordinal);
                var currentMatches = answerStats.CorrectTaggedAnswers.Count(tags =>
                    tags.Overlaps(definitionTags));
                if (current.Progress + currentMatches < definition.Target)
                {
                    continue;
                }

                if (await TryUnlockCustomAsync(
                        db,
                        game,
                        player,
                        accountId,
                        definition,
                        cancellationToken))
                {
                    var notification = current with
                    {
                        Progress = definition.Target,
                        IsUnlocked = true
                    };
                    await PublishCustomAsync(game, player, notification, cancellationToken);
                }
            }
        }

        return isCompleted;
    }

    private static async Task<Dictionary<int, HashSet<string>>> LoadQuestionTagsAsync(
        QuizDbContext db,
        IReadOnlyList<RuntimeQuestion> questions,
        CancellationToken cancellationToken)
    {
        var questionIds = questions
            .Select(question => question.SourceQuestionId)
            .Distinct()
            .ToArray();
        if (questionIds.Length == 0)
        {
            return [];
        }

        var rows = await db.QuizQuestionTags
            .AsNoTracking()
            .Where(tag => questionIds.Contains(tag.QuizQuestionId))
            .Select(tag => new { tag.QuizQuestionId, tag.NormalizedName })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.QuizQuestionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.NormalizedName)
                    .ToHashSet(StringComparer.Ordinal));
    }

    private static LiveAnswerStats BuildCurrentAnswerStats(
        GamePlayerId playerId,
        IReadOnlyList<RuntimeQuestion> questions,
        IReadOnlyDictionary<int, HashSet<string>> tagsByQuestion)
    {
        var orderedAttempts = questions
            .SelectMany(question => question.AnswerAttempts
                .Where(attempt => attempt.PlayerId == playerId)
                .Select(attempt => new QuestionAttempt(question, attempt)))
            .OrderBy(item => item.Attempt.JudgedAtUtc)
            .ThenBy(item => item.Attempt.Id)
            .ToArray();

        var correctAnswers = 0;
        var currentStreak = 0;
        var bestCorrectStreak = 0;
        var audioAnswers = 0;
        var videoAnswers = 0;
        var taggedAnswers = new List<HashSet<string>>();
        var tagSources = new List<PlayerAchievementAnswerSource>();

        foreach (var item in orderedAttempts)
        {
            if (!item.Attempt.IsCorrect)
            {
                currentStreak = 0;
                continue;
            }

            correctAnswers++;
            currentStreak++;
            bestCorrectStreak = Math.Max(bestCorrectStreak, currentStreak);

            var hasAudio = HasContentKind(item.Question, ContentBlockKind.Audio);
            var hasVideo = HasContentKind(item.Question, ContentBlockKind.Video) ||
                           HasContentKind(item.Question, ContentBlockKind.YouTube);
            if (hasAudio)
            {
                audioAnswers++;
            }
            if (hasVideo)
            {
                videoAnswers++;
            }

            var tags = tagsByQuestion.TryGetValue(item.Question.SourceQuestionId, out var found)
                ? new HashSet<string>(found, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            taggedAnswers.Add(tags);
            tagSources.Add(new PlayerAchievementAnswerSource(
                0,
                true,
                item.Attempt.ScoreDelta,
                item.Attempt.JudgedAtUtc.UtcDateTime,
                tags.ToArray(),
                hasAudio,
                hasVideo));
        }

        return new LiveAnswerStats(
            correctAnswers,
            bestCorrectStreak,
            audioAnswers,
            videoAnswers,
            PlayerTagAchievementCatalog.CountAnswers(tagSources),
            taggedAnswers);
    }

    private static bool HasContentKind(RuntimeQuestion question, ContentBlockKind kind) =>
        question.QuestionBlocks.Any(block => block.Kind == kind) ||
        question.AnswerBlocks.Any(block => block.Kind == kind);

    private static int GetLiveMetricValue(
        PlayerAchievementDefinition definition,
        PlayerAchievementProgress historical,
        LiveAnswerStats current) => definition.Metric switch
    {
        PlayerAchievementMetric.CorrectAnswers => historical.Progress + current.CorrectAnswers,
        PlayerAchievementMetric.BestCorrectStreak => Math.Max(
            historical.Progress,
            current.BestCorrectStreak),
        PlayerAchievementMetric.TaggedAnswers => historical.Progress +
            (definition.TagGroup is not null &&
             current.TaggedAnswerCounts.TryGetValue(definition.TagGroup, out var tagged)
                ? tagged
                : 0),
        PlayerAchievementMetric.AudioQuestionAnswers => historical.Progress + current.AudioAnswers,
        PlayerAchievementMetric.VideoQuestionAnswers => historical.Progress + current.VideoAnswers,
        _ => historical.Progress
    };

    private static IReadOnlySet<string> GetLiveDirectUnlockCodes(
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        IReadOnlyList<RuntimeQuestion> questions)
    {
        var result = new HashSet<string>(
            PlayerAchievementRuntimeState.GetPendingAchievementCodes(game, player.Id),
            StringComparer.Ordinal);

        if (PlayerAchievementRuntimeState.DidCompleteFirstToThirdReturn(game, player.Id))
        {
            result.Add("FirstToThirdReturn");
        }

        var selectedQuestions = questions
            .Select(question => new
            {
                Question = question,
                Sequence = game.GetQuestionOpenSequence(question.SourceQuestionId)
            })
            .Where(item => item.Question.SelectedByPlayerId.HasValue && item.Sequence.HasValue)
            .OrderBy(item => item.Sequence!.Value)
            .ToArray();
        if (selectedQuestions.FirstOrDefault()?.Question.SelectedByPlayerId == player.Id)
        {
            result.Add("FirstPick");
        }

        var orderedRounds = game.Session.Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .ToArray();
        if (orderedRounds.Length >= 2 &&
            PlayerAchievementRuntimeState.GetRoundFirstPick(
                game,
                orderedRounds[1].SourceRoundId)?.PlayerId == player.Id)
        {
            result.Add("SecondRoundFirstPick");
        }

        foreach (var question in questions)
        {
            foreach (var attempt in question.AnswerAttempts.Where(attempt => attempt.PlayerId == player.Id))
            {
                if (attempt.IsCorrect && attempt.RewardModifier == AnswerRewardModifier.Double)
                {
                    result.Add("DoubleReward");
                }
                if (attempt.IsCorrect && attempt.RewardModifier == AnswerRewardModifier.Half)
                {
                    result.Add("HalfReward");
                }
                if (PlayerAchievementRuntimeState.WasAllInWager(
                        game,
                        question.SourceQuestionId,
                        player.Id))
                {
                    result.Add(attempt.IsCorrect ? "AllInCorrect" : "AllInWrong");
                }
                if (attempt.IsCorrect &&
                    question.PresentationType == QuestionPresentationType.FourClues &&
                    question.RevealedClueCount == 2)
                {
                    result.Add("FourCluesTwoClues");
                }
            }
        }

        foreach (var round in orderedRounds)
        {
            var roundQuestions = questions
                .Where(question => question.SourceRoundId == round.SourceRoundId)
                .ToArray();
            var categoryIds = roundQuestions
                .Select(question => question.SourceCategoryId)
                .Distinct()
                .ToArray();
            if (categoryIds.Length < 5)
            {
                continue;
            }

            var attempted = roundQuestions
                .Where(question => question.AnswerAttempts.Any(attempt => attempt.PlayerId == player.Id))
                .Select(question => question.SourceCategoryId)
                .ToHashSet();
            if (categoryIds.All(attempted.Contains))
            {
                result.Add("EveryCategoryAttempt");
            }

            var correct = roundQuestions
                .Where(question => question.AnswerAttempts.Any(attempt =>
                    attempt.PlayerId == player.Id && attempt.IsCorrect))
                .Select(question => question.SourceCategoryId)
                .ToHashSet();
            if (categoryIds.All(correct.Contains))
            {
                result.Add("EveryCategoryCorrect");
            }
        }

        return result;
    }

    private static async Task<bool> TryUnlockCustomAsync(
        QuizDbContext db,
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        string? accountId,
        HostCustomAchievement definition,
        CancellationToken cancellationToken)
    {
        var identity = PlayerAchievementIdentity.Create(accountId, game.HostId, player.Name);
        if (!identity.IsValid)
        {
            return false;
        }

        var code = HostCustomAchievementService.BuildCode(definition.Id);
        var exists = identity.AccountId is not null
            ? await db.PlayerAchievements.AsNoTracking().AnyAsync(
                item => item.AccountId == identity.AccountId && item.AchievementCode == code,
                cancellationToken)
            : await db.PlayerAchievements.AsNoTracking().AnyAsync(
                item =>
                    item.AccountId == null &&
                    item.HostId == identity.HostId &&
                    item.PlayerKey == identity.PlayerKey &&
                    item.AchievementCode == code,
                cancellationToken);
        if (exists)
        {
            return false;
        }

        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = identity.AccountId,
            HostId = identity.AccountId is null ? identity.HostId : null,
            PlayerKey = identity.AccountId is null ? identity.PlayerKey : null,
            AchievementCode = code,
            SourceGameSessionId = null,
            UnlockedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task BackfillLiveUnlockSourcesAsync(
        QuizDbContext db,
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        string? accountId,
        int gameSessionId,
        CancellationToken cancellationToken)
    {
        var codes = PlayerAchievementRuntimeState.GetNotifiedAchievementCodes(game, player.Id);
        if (codes.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var normalizedAccountId = accountId.Trim();
            await db.PlayerAchievements
                .Where(item =>
                    item.AccountId == normalizedAccountId &&
                    item.SourceGameSessionId == null &&
                    codes.Contains(item.AchievementCode))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        item => item.SourceGameSessionId,
                        gameSessionId),
                    cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(game.HostId))
        {
            var hostId = game.HostId.Trim();
            var playerKey = PlayerAchievementService.NormalizePlayerKey(player.Name);
            await db.PlayerAchievements
                .Where(item =>
                    item.AccountId == null &&
                    item.HostId == hostId &&
                    item.PlayerKey == playerKey &&
                    item.SourceGameSessionId == null &&
                    codes.Contains(item.AchievementCode))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        item => item.SourceGameSessionId,
                        gameSessionId),
                    cancellationToken);
        }
    }

    private async Task PublishBuiltInAsync(
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        string code,
        CancellationToken cancellationToken)
    {
        if (!PlayerAchievementRuntimeState.TryMarkAchievementUnlockNotified(game, player.Id, code))
        {
            return;
        }

        await PublishAsync(
            game,
            player,
            new AchievementUnlockNotification(
                BuildEventId(game, player, code),
                player.Id.Value,
                player.Name,
                code,
                localizer[$"{code}_Name"].Value,
                localizer[$"{code}_Description"].Value,
                $"/images/achievements/{code}.png"),
            cancellationToken);
    }

    private async Task PublishCustomAsync(
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        HostCustomAchievementProgress achievement,
        CancellationToken cancellationToken)
    {
        if (!PlayerAchievementRuntimeState.TryMarkAchievementUnlockNotified(
                game,
                player.Id,
                achievement.Code))
        {
            return;
        }

        await PublishAsync(
            game,
            player,
            new AchievementUnlockNotification(
                BuildEventId(game, player, achievement.Code),
                player.Id.Value,
                player.Name,
                achievement.Code,
                achievement.Name,
                achievement.Description,
                achievement.ArtworkUrl),
            cancellationToken);
    }

    private async Task PublishAsync(
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        AchievementUnlockNotification notification,
        CancellationToken cancellationToken)
    {
        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync("AchievementUnlocked", notification, cancellationToken);

        logger.LogInformation(
            "Achievement unlock notification published. GameCode={GameCode} PlayerId={PlayerId} Achievement={AchievementCode}",
            game.PublicCode,
            player.Id.Value,
            notification.AchievementCode);
    }

    private static string BuildEventId(
        GameSessionRegistration game,
        RuntimeGamePlayer player,
        string code) =>
        $"{game.Session.Id.Value:N}:{player.Id.Value:N}:{code}";

    private static string BuildStateSignature(GameSessionRegistration game)
    {
        var runtime = game.Session;
        var values = new List<string>
        {
            runtime.Status.ToString(),
            runtime.CurrentRoundIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        foreach (var player in runtime.Players.OrderBy(item => item.Id.Value))
        {
            values.Add(player.Id.Value.ToString("N"));
            values.Add(PlayerAchievementRuntimeState.GetPlayerAccountId(game, player.Id) ?? string.Empty);
            values.Add(string.Join(',',
                PlayerAchievementRuntimeState.GetPendingAchievementCodes(game, player.Id)));
        }

        foreach (var question in runtime.Board.Questions.OrderBy(item => item.SourceQuestionId))
        {
            values.Add(question.SourceQuestionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            values.Add(question.Status.ToString());
            values.Add(question.RevealedClueCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var attempt in question.AnswerAttempts.OrderBy(item => item.JudgedAtUtc))
            {
                values.Add(attempt.Id.ToString("N"));
                values.Add(attempt.IsCorrect ? "1" : "0");
                values.Add(((int)attempt.RewardModifier).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return string.Join('|', values);
    }

    private sealed record QuestionAttempt(RuntimeQuestion Question, QuestionAnswerAttempt Attempt);

    private sealed record LiveAnswerStats(
        int CorrectAnswers,
        int BestCorrectStreak,
        int AudioAnswers,
        int VideoAnswers,
        IReadOnlyDictionary<string, int> TaggedAnswerCounts,
        IReadOnlyList<HashSet<string>> CorrectTaggedAnswers);
}

public sealed record AchievementUnlockNotification(
    string EventId,
    Guid PlayerId,
    string PlayerName,
    string AchievementCode,
    string Title,
    string Description,
    string ArtworkUrl);
