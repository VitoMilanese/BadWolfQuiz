using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using RuntimeGamePlayer = BadWolfQuiz.Game.Runtime.GamePlayer;
using RuntimeGameSession = BadWolfQuiz.Game.Runtime.GameSession;
using StoredGamePlayer = BadWolfQuiz.Web.Models.GamePlayer;
using StoredGameQuestion = BadWolfQuiz.Web.Models.GameQuestion;
using StoredGameSession = BadWolfQuiz.Web.Models.GameSession;

namespace BadWolfQuiz.Web.Services;

public sealed class GameHistoryStore(QuizDbContext db)
{
    public async Task<bool> SaveCompletedGameAsync(
        GameSessionRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var runtime = registration.Session;

        if (!IsComplete(runtime))
        {
            return false;
        }

        var quizExists = await db.Quizzes
            .IgnoreQueryFilters()
            .AnyAsync(
            quiz => quiz.Id == runtime.Quiz.SourceQuizId,
            cancellationToken);

        if (!quizExists)
        {
            return false;
        }

        var stored = await db.GameSessions
            .Include(session => session.Players)
            .Include(session => session.Questions)
                .ThenInclude(question => question.Results)
            .SingleOrDefaultAsync(
                session => session.PublicCode == registration.PublicCode,
                cancellationToken);

        if (stored is null)
        {
            stored = new StoredGameSession
            {
                QuizId = runtime.Quiz.SourceQuizId,
                HostId = registration.HostId,
                PublicCode = registration.PublicCode
            };
            db.GameSessions.Add(stored);
        }
        else
        {
            db.PlayerQuestionResults.RemoveRange(
                stored.Questions.SelectMany(question => question.Results));
            db.GameQuestions.RemoveRange(stored.Questions);
            db.GamePlayers.RemoveRange(stored.Players);
            stored.Questions.Clear();
            stored.Players.Clear();
        }

        stored.Status = BadWolfQuiz.Web.Models.GameSessionStatus.Finished;
        stored.CreatedAtUtc = runtime.CreatedAtUtc.UtcDateTime;
        stored.StartedAtUtc = runtime.StartedAtUtc?.UtcDateTime;
        stored.FinishedAtUtc = DateTime.UtcNow;

        var players = runtime.AllPlayers.ToDictionary(
            player => player.Id,
            player => new StoredGamePlayer
            {
                Name = player.Name,
                ReconnectToken = string.Empty,
                TotalScore = player.Score,
                JoinedAtUtc = player.JoinedAtUtc.UtcDateTime,
                LastSeenAtUtc = stored.FinishedAtUtc,
                IsActive = false
            });

        foreach (var pair in players)
        {
            stored.Players.Add(pair.Value);

            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(
                registration,
                pair.Key);
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
                {
                    Player = pair.Value,
                    AccountId = accountId
                });
            }
        }

        foreach (var question in runtime.Board.Questions)
        {
            var storedQuestion = new StoredGameQuestion
            {
                QuizQuestionId = question.SourceQuestionId,
                Status = question.Status == RuntimeQuestionStatus.Resolved
                    ? GameQuestionStatus.Finished
                    : GameQuestionStatus.Pending,
                FinishedAtUtc = question.Status == RuntimeQuestionStatus.Resolved
                    ? stored.FinishedAtUtc
                    : null
            };

            foreach (var attempt in question.AnswerAttempts)
            {
                storedQuestion.Results.Add(new PlayerQuestionResult
                {
                    Player = players[attempt.PlayerId],
                    IsCorrect = attempt.IsCorrect,
                    PointsAwarded = attempt.ScoreDelta,
                    CreatedAtUtc = attempt.JudgedAtUtc.UtcDateTime
                });
            }

            stored.Questions.Add(storedQuestion);
        }

        await db.SaveChangesAsync(cancellationToken);

        var achievements = new PlayerAchievementService(db);
        var allAttempts = runtime.Board.Questions
            .SelectMany(question => question.AnswerAttempts)
            .ToArray();

        foreach (var question in runtime.Board.Questions)
        {
            var highestQuestionValue = runtime.Board.Questions
                .Where(item => item.SourceRoundId == question.SourceRoundId)
                .Max(item => item.Points);
            var wagers = question.Wager is null
                ? question.AllPlayerWagers
                : question.AllPlayerWagers.Prepend(question.Wager);

            foreach (var wager in wagers)
            {
                var scoreBeforeWager = allAttempts
                    .Where(attempt =>
                        attempt.PlayerId == wager.PlayerId &&
                        attempt.JudgedAtUtc < wager.SubmittedAtUtc)
                    .Sum(attempt => attempt.ScoreDelta);
                var maximumWager = Math.Max(scoreBeforeWager, highestQuestionValue);
                if (wager.Amount != maximumWager)
                {
                    continue;
                }

                var attempt = question.AnswerAttempts
                    .SingleOrDefault(item => item.PlayerId == wager.PlayerId);
                if (attempt is null)
                {
                    continue;
                }

                var player = runtime.AllPlayers.SingleOrDefault(item => item.Id == wager.PlayerId);
                if (player is null)
                {
                    continue;
                }

                await achievements.UnlockPlayerAsync(
                    PlayerAchievementRuntimeState.GetPlayerAccountId(registration, player.Id),
                    registration.HostId,
                    player.Name,
                    attempt.IsCorrect ? "AllInCorrect" : "AllInWrong",
                    stored.Id,
                    cancellationToken);
            }
        }

        foreach (var submission in runtime.FinalQuestion?.Submissions ?? [])
        {
            if (submission.Wager?.Amount != submission.MaximumWager ||
                !submission.IsCorrect.HasValue)
            {
                continue;
            }

            var player = runtime.AllPlayers.SingleOrDefault(item => item.Id == submission.PlayerId);
            if (player is null)
            {
                continue;
            }

            await achievements.UnlockPlayerAsync(
                PlayerAchievementRuntimeState.GetPlayerAccountId(registration, player.Id),
                registration.HostId,
                player.Name,
                submission.IsCorrect.Value ? "AllInCorrect" : "AllInWrong",
                stored.Id,
                cancellationToken);
        }

        await UnlockGameplayAchievementsAsync(
            achievements,
            registration,
            runtime,
            stored.Id,
            cancellationToken);

        await achievements.EvaluateCompletedGameAsync(
            stored.Id,
            cancellationToken);
        return true;
    }

    private static async Task UnlockGameplayAchievementsAsync(
        PlayerAchievementService achievements,
        GameSessionRegistration registration,
        RuntimeGameSession runtime,
        int storedGameSessionId,
        CancellationToken cancellationToken)
    {
        async Task UnlockAsync(RuntimeGamePlayer player, string code)
        {
            await achievements.UnlockPlayerAsync(
                PlayerAchievementRuntimeState.GetPlayerAccountId(registration, player.Id),
                registration.HostId,
                player.Name,
                code,
                storedGameSessionId,
                cancellationToken);
        }

        var orderedRounds = runtime.Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .ToArray();
        var selectedQuestions = runtime.Board.Questions
            .Select(question => new
            {
                Question = question,
                Sequence = registration.GetQuestionOpenSequence(question.SourceQuestionId)
            })
            .Where(item =>
                item.Question.SelectedByPlayerId.HasValue &&
                item.Sequence.HasValue)
            .OrderBy(item => item.Sequence!.Value)
            .ToArray();

        if (selectedQuestions.FirstOrDefault()?.Question.SelectedByPlayerId is { } firstPlayerId)
        {
            var firstPlayer = runtime.AllPlayers.SingleOrDefault(player => player.Id == firstPlayerId);
            if (firstPlayer is not null)
            {
                await UnlockAsync(firstPlayer, "FirstPick");
            }
        }

        if (orderedRounds.Length >= 2)
        {
            var secondRoundFirst = PlayerAchievementRuntimeState.GetRoundFirstPick(
                registration,
                orderedRounds[1].SourceRoundId);
            if (secondRoundFirst is not null)
            {
                var secondRoundPlayer = runtime.AllPlayers.SingleOrDefault(player =>
                    player.Id == secondRoundFirst.PlayerId);
                if (secondRoundPlayer is not null)
                {
                    await UnlockAsync(secondRoundPlayer, "SecondRoundFirstPick");
                }
            }

            var lastRoundFirst = PlayerAchievementRuntimeState.GetRoundFirstPick(
                registration,
                orderedRounds[^1].SourceRoundId);
            if (lastRoundFirst is { WasLowestScore: true } && runtime.AllPlayers.Count > 0)
            {
                var winningScore = runtime.AllPlayers.Max(player => player.Score);
                var lastRoundPlayer = runtime.AllPlayers.SingleOrDefault(player =>
                    player.Id == lastRoundFirst.PlayerId);
                if (lastRoundPlayer is not null && lastRoundPlayer.Score == winningScore)
                {
                    await UnlockAsync(lastRoundPlayer, "LastRoundComebackWin");
                }
            }
        }

        foreach (var question in runtime.Board.Questions.Where(question =>
                     question.PresentationType == QuestionPresentationType.FourClues &&
                     question.RevealedClueCount == 2))
        {
            foreach (var attempt in question.AnswerAttempts.Where(attempt => attempt.IsCorrect))
            {
                var player = runtime.AllPlayers.SingleOrDefault(item => item.Id == attempt.PlayerId);
                if (player is not null)
                {
                    await UnlockAsync(player, "FourCluesTwoClues");
                }
            }
        }

        if (runtime.FinalQuestion is { } finalQuestion)
        {
            var finalStartingScores = finalQuestion.Submissions
                .Where(submission =>
                    submission.Wager is not null &&
                    submission.IsCorrect.HasValue)
                .Select(submission =>
                {
                    var player = runtime.AllPlayers.SingleOrDefault(item =>
                        item.Id == submission.PlayerId);
                    if (player is null)
                    {
                        return null;
                    }

                    var finalDelta = submission.IsCorrect!.Value
                        ? submission.Wager!.Amount
                        : -submission.Wager!.Amount;
                    return new FinalStartingScore(player, player.Score - finalDelta);
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray();

            if (finalStartingScores.Length > 0)
            {
                var leadingScore = finalStartingScores.Max(item => item.Score);
                foreach (var leader in finalStartingScores.Where(item =>
                             item.Score == leadingScore &&
                             item.Player.Score == 0))
                {
                    await UnlockAsync(leader.Player, "FinalLeaderZero");
                }
            }
        }

        foreach (var player in runtime.AllPlayers)
        {
            foreach (var achievementCode in
                     PlayerAchievementRuntimeState.GetPendingAchievementCodes(registration, player.Id))
            {
                await UnlockAsync(player, achievementCode);
            }

            if (PlayerAchievementRuntimeState.DidCompleteFirstToThirdReturn(registration, player.Id))
            {
                await UnlockAsync(player, "FirstToThirdReturn");
            }

            var attemptedEveryCategory = false;
            var correctInEveryCategory = false;
            var silentRound = false;
            var silentRoundGain = false;

            foreach (var round in orderedRounds)
            {
                var roundQuestions = runtime.Board.Questions
                    .Where(question => question.SourceRoundId == round.SourceRoundId)
                    .ToArray();
                var categoryIds = roundQuestions
                    .Select(question => question.SourceCategoryId)
                    .Distinct()
                    .ToArray();

                if (categoryIds.Length >= 5)
                {
                    var attemptedCategoryIds = roundQuestions
                        .Where(question => question.AnswerAttempts.Any(attempt =>
                            attempt.PlayerId == player.Id))
                        .Select(question => question.SourceCategoryId)
                        .ToHashSet();
                    var correctCategoryIds = roundQuestions
                        .Where(question => question.AnswerAttempts.Any(attempt =>
                            attempt.PlayerId == player.Id && attempt.IsCorrect))
                        .Select(question => question.SourceCategoryId)
                        .ToHashSet();

                    attemptedEveryCategory |= categoryIds.All(attemptedCategoryIds.Contains);
                    correctInEveryCategory |= categoryIds.All(correctCategoryIds.Contains);
                }

                var firstPick = PlayerAchievementRuntimeState.GetRoundFirstPick(
                    registration,
                    round.SourceRoundId);
                var hasOpenedBuzzerQuestion = roundQuestions.Any(question =>
                    registration.GetQuestionOpenSequence(question.SourceQuestionId).HasValue &&
                    !question.IsSpecial &&
                    question.PresentationType is
                        QuestionPresentationType.Standard or
                        QuestionPresentationType.FourClues or
                        QuestionPresentationType.HostMultipleChoice);
                var isSilentRound = firstPick is not null &&
                    firstPick.PresentPlayerIds.Contains(player.Id) &&
                    hasOpenedBuzzerQuestion &&
                    !PlayerAchievementRuntimeState.HasBuzzerPressInRound(
                        registration,
                        round.SourceRoundId,
                        player.Id);

                silentRound |= isSilentRound;
                if (isSilentRound)
                {
                    var roundScoreDelta = roundQuestions
                        .SelectMany(question => question.AnswerAttempts)
                        .Where(attempt => attempt.PlayerId == player.Id)
                        .Sum(attempt => attempt.ScoreDelta);
                    silentRoundGain |= roundScoreDelta > 0;
                }
            }

            if (attemptedEveryCategory)
            {
                await UnlockAsync(player, "EveryCategoryAttempt");
            }
            if (correctInEveryCategory)
            {
                await UnlockAsync(player, "EveryCategoryCorrect");
            }
            if (silentRound)
            {
                await UnlockAsync(player, "SilentRound");
            }
            if (silentRoundGain)
            {
                await UnlockAsync(player, "SilentRoundGain");
            }
        }
    }

    private static bool IsComplete(RuntimeGameSession session)
    {
        return session.Status == BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed ||
            session.Quiz.FinalQuestion is null &&
            !session.HasNextRound &&
            session.IsCurrentRoundComplete;
    }

    private sealed record FinalStartingScore(RuntimeGamePlayer Player, int Score);
}
