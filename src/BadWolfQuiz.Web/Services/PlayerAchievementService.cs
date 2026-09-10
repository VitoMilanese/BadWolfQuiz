using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class PlayerAchievementService(QuizDbContext db)
{
    public static IReadOnlyList<PlayerAchievementDefinition> Catalog { get; } =
    [
        new("FirstGame", "🐾", false, PlayerAchievementMetric.GamesPlayed, 1),
        new("FirstBite", "🦷", false, PlayerAchievementMetric.CorrectAnswers, 1),
        new("Regular", "🎟️", false, PlayerAchievementMetric.GamesPlayed, 5),
        new("Veteran", "🏅", false, PlayerAchievementMetric.GamesPlayed, 25),
        new("Accurate", "🎯", false, PlayerAchievementMetric.CorrectAnswers, 25),
        new("KnowledgeMachine", "🧠", false, PlayerAchievementMetric.CorrectAnswers, 100),
        new("Winner", "🏆", false, PlayerAchievementMetric.Wins, 1),
        new("Champion", "👑", false, PlayerAchievementMetric.Wins, 5),
        new("Streak", "🔥", false, PlayerAchievementMetric.BestCorrectStreak, 5),
        new("BigGame", "💰", false, PlayerAchievementMetric.BestFinalScore, 15_000),
        new("Score30K", "🤑", false, PlayerAchievementMetric.BestFinalScore, 30_000),
        new("TotalScore100K", "💯", false, PlayerAchievementMetric.TotalScore, 100_000),
        new("TotalScore500K", "💎", false, PlayerAchievementMetric.TotalScore, 500_000),
        new("TotalScore1M", "🏦", false, PlayerAchievementMetric.TotalScore, 1_000_000),
        new("Flawless", "✨", false, PlayerAchievementMetric.BestFlawlessAttempts, 10),
        new("FromAbyss", "🐺", true, PlayerAchievementMetric.RecoveredFromNegative, 1),
        new("Registered", "🪪", false, PlayerAchievementMetric.Registered, 1),
        new("OwnQuizWithOthers", "🛠️", false, PlayerAchievementMetric.OwnQuizWithOthers, 1),
        new("PublicQuizGuest", "🌐", false, PlayerAchievementMetric.PublicQuizGuest, 1),
        new("SoloAi", "🤖", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("RoomCreatorWin", "🎮", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("GitHubVisitor", "💻", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("QuizRated", "⭐", false, PlayerAchievementMetric.QuizRated, 1),
        new("AllInCorrect", "🎰", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("AllInWrong", "💥", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("PasswordChanged", "🔐", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("DeveloperContacted", "📨", true, PlayerAchievementMetric.DeveloperContacted, 1),
        new("DeveloperReplied", "📬", true, PlayerAchievementMetric.DeveloperReplied, 1),
        new("Contributor", "🧩", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("FirstPick", "🏁", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("SecondRoundFirstPick", "2️⃣", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("LastRoundComebackWin", "🐺", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("FinalLeaderZero", "📉", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("EveryCategoryAttempt", "🧭", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("EveryCategoryCorrect", "🧠", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("SilentRound", "🤐", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("SilentRoundGain", "📈", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("AnonymousStake100Profit", "💯", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("AnonymousStakeZeroSave", "🛡️", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("FourCluesTwoClues", "✌️", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("BuzzerPhotoFinishFirst", "⚡", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("BuzzerPhotoFinishSecond", "🏎️", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("KickedAndReturned", "🚪", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("FirstToThirdReturn", "🕳️", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("LateJoiner", "🕒", true, PlayerAchievementMetric.DirectUnlock, 1),
        new("AvatarChanged", "🎭", false, PlayerAchievementMetric.DirectUnlock, 1),
        new("WebcamEnabled", "📷", false, PlayerAchievementMetric.DirectUnlock, 1),
        .. PlayerTagAchievementCatalog.Definitions
    ];

    public async Task AdoptHostNicknameHistoryAsync(
        string? accountId,
        string? hostId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(hostId))
        {
            return;
        }

        var playerKey = NormalizePlayerKey(playerName);
        if (playerKey.Length == 0)
        {
            return;
        }

        var candidates = await db.GamePlayers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(player =>
                player.Session.HostId == hostId &&
                player.Session.Status == GameSessionStatus.Finished)
            .Select(player => new { player.Id, player.Name })
            .ToListAsync(cancellationToken);
        var candidateIds = candidates
            .Where(player => string.Equals(
                NormalizePlayerKey(player.Name),
                playerKey,
                StringComparison.Ordinal))
            .Select(player => player.Id)
            .Distinct()
            .ToArray();
        if (candidateIds.Length == 0)
        {
            return;
        }

        var linkedIds = (await db.PlayerGameAccountLinks
                .AsNoTracking()
                .Where(link => candidateIds.Contains(link.GamePlayerId))
                .Select(link => link.GamePlayerId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var playerId in candidateIds.Where(id => !linkedIds.Contains(id)))
        {
            db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
            {
                GamePlayerId = playerId,
                AccountId = accountId.Trim()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EvaluateCompletedGameAsync(
        int gameSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await db.GameSessions
            .IgnoreQueryFilters()
            .Where(item => item.Id == gameSessionId && item.Status == GameSessionStatus.Finished)
            .Select(item => new { item.HostId })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.HostId))
        {
            return;
        }

        var players = await db.GamePlayers
            .IgnoreQueryFilters()
            .Where(player => player.GameSessionId == gameSessionId)
            .Select(player => new
            {
                player.Id,
                player.Name,
                AccountId = db.PlayerGameAccountLinks
                    .Where(link => link.GamePlayerId == player.Id)
                    .Select(link => link.AccountId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var player in players)
        {
            var fallback = PlayerAchievementIdentity.Create(null, session.HostId, player.Name);
            if (fallback.IsValid)
            {
                await EnsureUnlockedAsync(fallback, gameSessionId, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(player.AccountId))
            {
                await EnsureUnlockedAsync(
                    PlayerAchievementIdentity.Create(player.AccountId, null, null),
                    gameSessionId,
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerAchievementProgress>> LoadForPlayerAsync(
        string? hostId,
        string playerName,
        string? currentGameCode,
        string? accountId = null,
        bool isContributor = false,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(
                NormalizePlayerKey(playerName),
                "SUPERCHUPA",
                StringComparison.Ordinal))
        {
            return Catalog
                .Select(definition => new PlayerAchievementProgress(
                    definition.Code,
                    definition.Icon,
                    definition.IsSecret,
                    IsUnlocked: true,
                    IsNewInCurrentGame: false,
                    Progress: definition.Target,
                    Target: definition.Target))
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            await AdoptHostNicknameHistoryAsync(accountId, hostId, playerName, cancellationToken);
        }

        var identity = PlayerAchievementIdentity.Create(accountId, hostId, playerName);
        if (!identity.IsValid)
        {
            return [];
        }

        var history = await EnsureUnlockedAsync(identity, null, cancellationToken);
        if (isContributor)
        {
            await AddUnlockIfMissingAsync(identity, "Contributor", null, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);

        int? currentGameSessionId = null;
        if (!string.IsNullOrWhiteSpace(currentGameCode))
        {
            currentGameSessionId = await db.GameSessions
                .IgnoreQueryFilters()
                .Where(session => session.PublicCode == currentGameCode)
                .Select(session => (int?)session.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var unlocked = await QueryForIdentity(identity)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return BuildProgress(history, unlocked, currentGameSessionId);
    }

    public Task<bool> UnlockAccountAsync(
        string? accountId,
        string achievementCode,
        int? sourceGameSessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Task.FromResult(false);
        }

        return UnlockAsync(
            PlayerAchievementIdentity.Create(accountId, null, null),
            achievementCode,
            sourceGameSessionId,
            cancellationToken);
    }

    public async Task<bool> UnlockPlayerAsync(
        string? accountId,
        string? hostId,
        string playerName,
        string achievementCode,
        int? sourceGameSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var changed = false;
        var fallback = PlayerAchievementIdentity.Create(null, hostId, playerName);
        if (fallback.IsValid)
        {
            changed |= await UnlockAsync(fallback, achievementCode, sourceGameSessionId, cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            changed |= await UnlockAsync(
                PlayerAchievementIdentity.Create(accountId, null, null),
                achievementCode,
                sourceGameSessionId,
                cancellationToken);
        }
        return changed;
    }

    private async Task<bool> UnlockAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        int? sourceGameSessionId,
        CancellationToken cancellationToken)
    {
        if (Catalog.All(item => !string.Equals(item.Code, achievementCode, StringComparison.Ordinal)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(achievementCode), achievementCode, "Unknown player achievement code.");
        }

        if (db.ChangeTracker.Entries<PlayerAchievement>()
                .Select(entry => entry.Entity)
                .Any(item => item.AchievementCode == achievementCode && MatchesIdentity(item, identity)) ||
            await QueryForIdentity(identity)
                .AsNoTracking()
                .AnyAsync(item => item.AchievementCode == achievementCode, cancellationToken))
        {
            return false;
        }

        AddUnlock(identity, achievementCode, sourceGameSessionId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PlayerAchievementHistory> EnsureUnlockedAsync(
        PlayerAchievementIdentity identity,
        int? sourceGameSessionId,
        CancellationToken cancellationToken)
    {
        var history = identity.AccountId is not null
            ? await LoadAccountHistoryAsync(identity.AccountId, cancellationToken)
            : await LoadHostHistoryAsync(identity.HostId!, identity.PlayerKey!, cancellationToken);
        var existing = (await QueryForIdentity(identity)
                .AsNoTracking()
                .Select(item => item.AchievementCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var tracked in db.ChangeTracker.Entries<PlayerAchievement>()
                     .Select(entry => entry.Entity)
                     .Where(item => MatchesIdentity(item, identity)))
        {
            existing.Add(tracked.AchievementCode);
        }

        foreach (var definition in Catalog)
        {
            if (existing.Contains(definition.Code) || !IsSatisfied(definition, history))
            {
                continue;
            }
            AddUnlock(identity, definition.Code, sourceGameSessionId);
            existing.Add(definition.Code);
        }
        return history;
    }

    private async Task AddUnlockIfMissingAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        int? sourceGameSessionId,
        CancellationToken cancellationToken)
    {
        if (db.ChangeTracker.Entries<PlayerAchievement>()
                .Select(entry => entry.Entity)
                .Any(item => item.AchievementCode == achievementCode && MatchesIdentity(item, identity)) ||
            await QueryForIdentity(identity)
                .AsNoTracking()
                .AnyAsync(item => item.AchievementCode == achievementCode, cancellationToken))
        {
            return;
        }
        AddUnlock(identity, achievementCode, sourceGameSessionId);
    }

    private void AddUnlock(
        PlayerAchievementIdentity identity,
        string achievementCode,
        int? sourceGameSessionId) =>
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = identity.AccountId,
            HostId = identity.AccountId is null ? identity.HostId : null,
            PlayerKey = identity.AccountId is null ? identity.PlayerKey : null,
            AchievementCode = achievementCode,
            SourceGameSessionId = sourceGameSessionId,
            UnlockedAtUtc = DateTime.UtcNow
        });

    private IQueryable<PlayerAchievement> QueryForIdentity(PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? db.PlayerAchievements.Where(item => item.AccountId == identity.AccountId)
            : db.PlayerAchievements.Where(item =>
                item.AccountId == null && item.HostId == identity.HostId && item.PlayerKey == identity.PlayerKey);

    private static bool MatchesIdentity(
        PlayerAchievement achievement,
        PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? achievement.AccountId == identity.AccountId
            : achievement.AccountId is null &&
              achievement.HostId == identity.HostId &&
              achievement.PlayerKey == identity.PlayerKey;

    private async Task<PlayerAchievementHistory> LoadHostHistoryAsync(
        string hostId,
        string playerKey,
        CancellationToken cancellationToken)
    {
        var candidates = await db.GamePlayers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(player =>
                player.Session.HostId == hostId &&
                player.Session.Status == GameSessionStatus.Finished)
            .Select(player => new PlayerAchievementGameSource(
                player.Id,
                player.GameSessionId,
                player.Name,
                player.TotalScore,
                player.Session.FinishedAtUtc ?? player.Session.CreatedAtUtc,
                player.Session.Quiz.HostId,
                player.Session.Quiz.IsPublic,
                player.Session.HostId,
                player.Session.Players.Count))
            .ToListAsync(cancellationToken);
        var appearances = candidates
            .Where(item => string.Equals(NormalizePlayerKey(item.Name), playerKey, StringComparison.Ordinal))
            .ToArray();
        var history = await BuildHistoryForAppearancesAsync(appearances, cancellationToken);
        if (appearances.Length == 0)
        {
            return history;
        }

        var gameIds = appearances.Select(item => item.GameSessionId).Distinct().ToArray();
        var ratingKeys = appearances
            .GroupBy(item => item.GameSessionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => $"player:{item.Name}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var ratings = await db.QuizRatings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(rating => gameIds.Contains(rating.GameSessionId))
            .Select(rating => new PlayerAchievementRatingSource(rating.GameSessionId, rating.RaterKey))
            .ToListAsync(cancellationToken);

        return history with
        {
            PublicQuizGuest = appearances.Any(IsPublicGuestAppearance),
            QuizRated = ratings.Any(rating =>
                ratingKeys.TryGetValue(rating.GameSessionId, out var keys) && keys.Contains(rating.RaterKey))
        };
    }

    private async Task<PlayerAchievementHistory> LoadAccountHistoryAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        // Account achievements must see games hosted by other accounts too.
        var appearances = await db.PlayerGameAccountLinks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(link =>
                link.AccountId == accountId &&
                link.Player.Session.Status == GameSessionStatus.Finished)
            .Select(link => new PlayerAchievementGameSource(
                link.Player.Id,
                link.Player.GameSessionId,
                link.Player.Name,
                link.Player.TotalScore,
                link.Player.Session.FinishedAtUtc ?? link.Player.Session.CreatedAtUtc,
                link.Player.Session.Quiz.HostId,
                link.Player.Session.Quiz.IsPublic,
                link.Player.Session.HostId,
                link.Player.Session.Players.Count))
            .ToListAsync(cancellationToken);

        var history = await BuildHistoryForAppearancesAsync(appearances, cancellationToken);
        var registered = await db.Hosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(host => host.Id == accountId, cancellationToken);
        var ownQuizWithOthers = appearances.Any(item =>
            item.PlayerCount >= 3 && string.Equals(item.QuizOwnerId, accountId, StringComparison.Ordinal));
        var publicQuizGuest = appearances.Any(IsPublicGuestAppearance);
        var gameIds = appearances.Select(item => item.GameSessionId).Distinct().ToArray();
        var playerRatingKeys = appearances
            .GroupBy(item => item.GameSessionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => $"player:{item.Name}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var ratingRows = await db.QuizRatings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(rating => gameIds.Contains(rating.GameSessionId))
            .Select(rating => new PlayerAchievementRatingSource(rating.GameSessionId, rating.RaterKey))
            .ToListAsync(cancellationToken);
        var ratedAsPlayer = ratingRows.Any(rating =>
            playerRatingKeys.TryGetValue(rating.GameSessionId, out var keys) && keys.Contains(rating.RaterKey));
        var ratedAsHost = await db.QuizRatings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(rating => rating.RaterKey == $"host:{accountId}", cancellationToken);
        var developerContacted = await db.UserQuestionAccountLinks
            .AsNoTracking()
            .AnyAsync(link => link.AccountId == accountId, cancellationToken);
        var developerReplied = await db.UserQuestionAccountLinks
            .AsNoTracking()
            .AnyAsync(
                link => link.AccountId == accountId &&
                    link.UserQuestion.Messages.Any(message =>
                        message.AuthorType == UserQuestionAuthorType.Developer),
                cancellationToken);

        return history with
        {
            Registered = registered,
            OwnQuizWithOthers = ownQuizWithOthers,
            PublicQuizGuest = publicQuizGuest,
            QuizRated = ratedAsPlayer || ratedAsHost,
            DeveloperContacted = developerContacted,
            DeveloperReplied = developerReplied
        };
    }

    private async Task<PlayerAchievementHistory> BuildHistoryForAppearancesAsync(
        IReadOnlyCollection<PlayerAchievementGameSource> appearances,
        CancellationToken cancellationToken)
    {
        if (appearances.Count == 0)
        {
            return PlayerAchievementHistory.Empty;
        }
        var gameIds = appearances.Select(item => item.GameSessionId).Distinct().ToArray();
        var playerIds = appearances.Select(item => item.GamePlayerId).Distinct().ToArray();
        var gameScores = await db.GamePlayers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(player => gameIds.Contains(player.GameSessionId))
            .Select(player => new PlayerAchievementGameScoreSource(player.GameSessionId, player.TotalScore))
            .ToListAsync(cancellationToken);
        var answerRows = await db.PlayerQuestionResults
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(result => playerIds.Contains(result.GamePlayerId))
            .OrderBy(result => result.CreatedAtUtc)
            .Select(result => new
            {
                result.GamePlayerId,
                result.IsCorrect,
                result.PointsAwarded,
                result.CreatedAtUtc,
                result.GameQuestion.QuizQuestionId
            })
            .ToListAsync(cancellationToken);
        var quizQuestionIds = answerRows
            .Select(answer => answer.QuizQuestionId)
            .Distinct()
            .ToArray();
        var mediaRows = await db.QuestionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block =>
                quizQuestionIds.Contains(block.QuizQuestionId) &&
                (block.BlockType == ContentBlockType.Audio || block.BlockType == ContentBlockType.Video))
            .Select(block => new { block.QuizQuestionId, block.BlockType })
            .ToListAsync(cancellationToken);
        var audioQuestionIds = mediaRows
            .Where(block => block.BlockType == ContentBlockType.Audio)
            .Select(block => block.QuizQuestionId)
            .ToHashSet();
        var videoQuestionIds = mediaRows
            .Where(block => block.BlockType == ContentBlockType.Video)
            .Select(block => block.QuizQuestionId)
            .ToHashSet();
        var tagRows = await db.QuizQuestionTags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tag => quizQuestionIds.Contains(tag.QuizQuestionId))
            .Select(tag => new { tag.QuizQuestionId, tag.NormalizedName })
            .ToListAsync(cancellationToken);
        var tagsByQuestion = tagRows
            .GroupBy(tag => tag.QuizQuestionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(tag => tag.NormalizedName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
        var answers = answerRows
            .Select(answer => new PlayerAchievementAnswerSource(
                answer.GamePlayerId,
                answer.IsCorrect,
                answer.PointsAwarded,
                answer.CreatedAtUtc,
                tagsByQuestion.TryGetValue(answer.QuizQuestionId, out var tags) ? tags : [],
                audioQuestionIds.Contains(answer.QuizQuestionId),
                videoQuestionIds.Contains(answer.QuizQuestionId)))
            .ToArray();
        return BuildHistory(appearances, gameScores, answers);
    }

    private static bool IsPublicGuestAppearance(PlayerAchievementGameSource item) =>
        item.QuizIsPublic &&
        !string.IsNullOrWhiteSpace(item.QuizOwnerId) &&
        !string.Equals(item.GameHostId, item.QuizOwnerId, StringComparison.Ordinal);

    public static PlayerAchievementHistory BuildHistory(
        IReadOnlyCollection<PlayerAchievementGameSource> appearances,
        IReadOnlyCollection<PlayerAchievementGameScoreSource> gameScores,
        IReadOnlyCollection<PlayerAchievementAnswerSource> answers)
    {
        if (appearances.Count == 0)
        {
            return PlayerAchievementHistory.Empty;
        }
        var playersByGame = appearances
            .GroupBy(item => item.GameSessionId)
            .Select(group => group.OrderByDescending(item => item.FinishedAtUtc).First())
            .ToArray();
        var gameByPlayer = playersByGame.ToDictionary(item => item.GamePlayerId, item => item.GameSessionId);
        var maxScoreByGame = gameScores
            .GroupBy(item => item.GameSessionId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.FinalScore));
        var wins = playersByGame.Count(player =>
            maxScoreByGame.TryGetValue(player.GameSessionId, out var maximum) && player.FinalScore == maximum);
        var correctAnswers = answers.Count(answer => answer.IsCorrect == true);
        var bestFinalScore = playersByGame.Max(player => player.FinalScore);
        var totalScore = playersByGame.Sum(player => player.FinalScore);
        var bestCorrectStreak = 0;
        var bestFlawlessAttempts = 0;
        var recoveredFromNegative = false;

        foreach (var player in playersByGame)
        {
            var gameAnswers = answers
                .Where(answer =>
                    gameByPlayer.TryGetValue(answer.GamePlayerId, out var gameId) && gameId == player.GameSessionId)
                .OrderBy(answer => answer.CreatedAtUtc)
                .ToArray();
            var streak = 0;
            foreach (var answer in gameAnswers)
            {
                if (answer.IsCorrect == true)
                {
                    bestCorrectStreak = Math.Max(bestCorrectStreak, ++streak);
                }
                else
                {
                    streak = 0;
                }
            }
            if (gameAnswers.Length > 0 && gameAnswers.All(answer => answer.IsCorrect == true))
            {
                bestFlawlessAttempts = Math.Max(bestFlawlessAttempts, gameAnswers.Length);
            }
            if (player.FinalScore > 0)
            {
                var runningScore = 0;
                var wasNegative = false;
                foreach (var answer in gameAnswers)
                {
                    runningScore += answer.PointsAwarded;
                    wasNegative |= runningScore < 0;
                }
                recoveredFromNegative |= wasNegative;
            }
        }

        return new PlayerAchievementHistory(
            playersByGame.Length,
            correctAnswers,
            wins,
            bestCorrectStreak,
            bestFinalScore,
            bestFlawlessAttempts,
            recoveredFromNegative,
            TotalScore: totalScore,
            TaggedAnswers: PlayerTagAchievementCatalog.CountAnswers(answers),
            AudioQuestionAnswers: answers.Count(answer => answer.HasAudioBlock),
            VideoQuestionAnswers: answers.Count(answer => answer.HasVideoBlock));
    }

    public static IReadOnlyList<PlayerAchievementProgress> BuildProgress(
        PlayerAchievementHistory history,
        IReadOnlyCollection<PlayerAchievement> unlocked,
        int? currentGameSessionId = null)
    {
        var unlockedByCode = unlocked
            .GroupBy(item => item.AchievementCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.UnlockedAtUtc).First(),
                StringComparer.Ordinal);
        return Catalog
            .Select((definition, index) =>
            {
                var metricValue = GetMetricValue(definition, history);
                var isUnlocked = unlockedByCode.TryGetValue(definition.Code, out var record);
                return new
                {
                    Index = index,
                    Progress = new PlayerAchievementProgress(
                        definition.Code,
                        definition.Icon,
                        definition.IsSecret,
                        isUnlocked,
                        isUnlocked && currentGameSessionId.HasValue && record!.SourceGameSessionId == currentGameSessionId,
                        isUnlocked ? definition.Target : Math.Min(metricValue, definition.Target),
                        definition.Target)
                };
            })
            .OrderByDescending(item => item.Progress.IsUnlocked)
            .ThenBy(item => item.Index)
            .Select(item => item.Progress)
            .ToArray();
    }

    private static bool IsSatisfied(PlayerAchievementDefinition definition, PlayerAchievementHistory history) =>
        GetMetricValue(definition, history) >= definition.Target;

    private static int GetMetricValue(PlayerAchievementDefinition definition, PlayerAchievementHistory history)
    {
        if (definition.Metric == PlayerAchievementMetric.TaggedAnswers)
        {
            return PlayerTagAchievementCatalog.GetCount(history, definition.TagGroup);
        }

        return definition.Metric switch
        {
            PlayerAchievementMetric.GamesPlayed => history.GamesPlayed,
            PlayerAchievementMetric.CorrectAnswers => history.CorrectAnswers,
            PlayerAchievementMetric.Wins => history.Wins,
            PlayerAchievementMetric.BestCorrectStreak => history.BestCorrectStreak,
            PlayerAchievementMetric.BestFinalScore => Math.Max(0, history.BestFinalScore),
            PlayerAchievementMetric.TotalScore => Math.Max(0, history.TotalScore),
            PlayerAchievementMetric.BestFlawlessAttempts => history.BestFlawlessAttempts,
            PlayerAchievementMetric.RecoveredFromNegative => history.RecoveredFromNegative ? 1 : 0,
            PlayerAchievementMetric.Registered => history.Registered ? 1 : 0,
            PlayerAchievementMetric.OwnQuizWithOthers => history.OwnQuizWithOthers ? 1 : 0,
            PlayerAchievementMetric.PublicQuizGuest => history.PublicQuizGuest ? 1 : 0,
            PlayerAchievementMetric.QuizRated => history.QuizRated ? 1 : 0,
            PlayerAchievementMetric.DeveloperContacted => history.DeveloperContacted ? 1 : 0,
            PlayerAchievementMetric.DeveloperReplied => history.DeveloperReplied ? 1 : 0,
            PlayerAchievementMetric.AudioQuestionAnswers => history.AudioQuestionAnswers,
            PlayerAchievementMetric.VideoQuestionAnswers => history.VideoQuestionAnswers,
            PlayerAchievementMetric.DirectUnlock => 0,
            _ => 0
        };
    }

    public static string NormalizePlayerKey(string? name) =>
        (name ?? string.Empty).Trim().ToUpperInvariant();
}

public enum PlayerAchievementMetric
{
    GamesPlayed,
    CorrectAnswers,
    Wins,
    BestCorrectStreak,
    BestFinalScore,
    TotalScore,
    BestFlawlessAttempts,
    RecoveredFromNegative,
    Registered,
    OwnQuizWithOthers,
    PublicQuizGuest,
    QuizRated,
    DeveloperContacted,
    DeveloperReplied,
    TaggedAnswers,
    AudioQuestionAnswers,
    VideoQuestionAnswers,
    DirectUnlock
}

public sealed record PlayerAchievementDefinition(
    string Code,
    string Icon,
    bool IsSecret,
    PlayerAchievementMetric Metric,
    int Target,
    string? TagGroup = null);

public sealed record PlayerAchievementProgress(
    string Code,
    string Icon,
    bool IsSecret,
    bool IsUnlocked,
    bool IsNewInCurrentGame,
    int Progress,
    int Target);

public sealed record PlayerAchievementIdentity(string? AccountId, string? HostId, string? PlayerKey)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AccountId) ||
        (!string.IsNullOrWhiteSpace(HostId) && !string.IsNullOrWhiteSpace(PlayerKey));

    public static PlayerAchievementIdentity Create(string? accountId, string? hostId, string? playerName)
    {
        var normalizedAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId.Trim();
        if (normalizedAccountId is not null)
        {
            return new PlayerAchievementIdentity(normalizedAccountId, null, null);
        }
        var playerKey = PlayerAchievementService.NormalizePlayerKey(playerName);
        return new PlayerAchievementIdentity(
            null,
            string.IsNullOrWhiteSpace(hostId) ? null : hostId.Trim(),
            playerKey.Length == 0 ? null : playerKey);
    }
}

public sealed record PlayerAchievementHistory(
    int GamesPlayed,
    int CorrectAnswers,
    int Wins,
    int BestCorrectStreak,
    int BestFinalScore,
    int BestFlawlessAttempts,
    bool RecoveredFromNegative,
    bool Registered = false,
    bool OwnQuizWithOthers = false,
    bool PublicQuizGuest = false,
    bool QuizRated = false,
    bool DeveloperContacted = false,
    bool DeveloperReplied = false,
    int TotalScore = 0,
    IReadOnlyDictionary<string, int>? TaggedAnswers = null,
    int AudioQuestionAnswers = 0,
    int VideoQuestionAnswers = 0)
{
    public static PlayerAchievementHistory Empty { get; } = new(0, 0, 0, 0, 0, 0, false);
}

public sealed record PlayerAchievementGameSource(
    int GamePlayerId,
    int GameSessionId,
    string Name,
    int FinalScore,
    DateTime FinishedAtUtc,
    string? QuizOwnerId = null,
    bool QuizIsPublic = false,
    string? GameHostId = null,
    int PlayerCount = 1);

public sealed record PlayerAchievementGameScoreSource(int GameSessionId, int FinalScore);

public sealed record PlayerAchievementAnswerSource(
    int GamePlayerId,
    bool? IsCorrect,
    int PointsAwarded,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<string>? Tags = null,
    bool HasAudioBlock = false,
    bool HasVideoBlock = false);

public sealed record PlayerAchievementRatingSource(int GameSessionId, string RaterKey);
