using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class PeerRatedAllPlayerQuestionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    QuizDbContext db) : PageModel
{
    private const int MaximumAnswerLength = 500;

    public async Task<IActionResult> OnGetEditorAsync(
        int questionId,
        CancellationToken cancellationToken)
    {
        var hostId = currentHost.Id;
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return Forbid();
        }

        var presentationType = await db.QuizQuestions
            .AsNoTracking()
            .Where(question =>
                question.Id == questionId &&
                question.Category.Round.Quiz.HostId == hostId)
            .Select(question => (QuestionPresentationType?)question.PresentationType)
            .SingleOrDefaultAsync(cancellationToken);

        if (presentationType is null)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            peerRated = presentationType ==
                QuestionPresentationType.AllPlayerPeerRatedText
        });
    }

    public IActionResult OnGetHost(string code)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindCurrentQuestion(game);
            if (question is null)
            {
                return new JsonResult(new { active = false });
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            question = FindCurrentQuestion(game);

            return question is null
                ? new JsonResult(new { active = false })
                : new JsonResult(CreateHostState(game, question, review));
        }
    }

    public IActionResult OnGetPlayer(string code, Guid playerId)
    {
        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return NotFound();
        }

        lock (game)
        {
            var player = game.Session.Players.SingleOrDefault(item =>
                item.Id == new GamePlayerId(playerId));
            if (player is null)
            {
                return NotFound();
            }

            var question = FindCurrentQuestion(game);
            if (question is null)
            {
                return new JsonResult(new { active = false });
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            question = FindCurrentQuestion(game);

            return question is null
                ? new JsonResult(new { active = false })
                : new JsonResult(CreatePlayerState(game, question, review, player));
        }
    }

    public IActionResult OnPostSubmit(
        string code,
        Guid playerId,
        string accessToken,
        int sourceQuestionId,
        string answer)
    {
        return WithAuthenticatedPlayer(
            code,
            playerId,
            accessToken,
            (game, player) =>
            {
                lock (game)
                {
                    var question = FindQuestion(game, sourceQuestionId);
                    if (question is null)
                    {
                        return ConflictState(game, player);
                    }

                    var review = GetReview(game, question);
                    EnsureLifecycle(game, question, review);

                    if (!IsAnswering(review) ||
                        !IsActiveParticipant(game, review, player.Id) ||
                        review.Answers.ContainsKey(player.Id))
                    {
                        return ConflictState(game, player);
                    }

                    var normalizedAnswer = answer?.Trim() ?? string.Empty;
                    if (normalizedAnswer.Length is < 1 or > MaximumAnswerLength)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            error = "The submitted answer is invalid."
                        });
                    }

                    game.Session.AddQuestionAnswerHistoryEntry(
                        sourceQuestionId,
                        player.Id,
                        isCorrect: false,
                        value: 0,
                        resolveQuestionIfAvailable: false);
                    review.Answers[player.Id] = normalizedAnswer;
                    game.MarkPersistenceChanged();
                    EnsureLifecycle(game, question, review);

                    return new JsonResult(new
                    {
                        success = true,
                        state = CreatePlayerState(game, question, review, player)
                    });
                }
            });
    }

    public IActionResult OnPostRate(
        string code,
        Guid playerId,
        string accessToken,
        int sourceQuestionId,
        Guid answerPlayerId,
        int stars)
    {
        return WithAuthenticatedPlayer(
            code,
            playerId,
            accessToken,
            (game, player) =>
            {
                lock (game)
                {
                    if (stars is < 0 or > 5)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            error = "A peer rating must be between 0 and 5 stars."
                        });
                    }

                    var question = FindQuestion(game, sourceQuestionId);
                    if (question is null)
                    {
                        return ConflictState(game, player);
                    }

                    var review = GetReview(game, question);
                    EnsureLifecycle(game, question, review);
                    if (IsAnswering(review) || IsShowingResults(review))
                    {
                        return ConflictState(game, player);
                    }

                    var authorId = GetCurrentRatingAnswerPlayerId(review);
                    var requestedAuthorId = new GamePlayerId(answerPlayerId);

                    if (authorId is null ||
                        authorId.Value != requestedAuthorId ||
                        player.Id == requestedAuthorId ||
                        !IsActiveParticipant(game, review, player.Id))
                    {
                        return ConflictState(game, player);
                    }

                    var ratings = GetOrCreateRatings(review, requestedAuthorId);
                    if (!ratings.ContainsKey(player.Id))
                    {
                        ratings[player.Id] = stars;
                        CompleteCurrentRatingIfReady(review);
                        game.MarkPersistenceChanged();
                    }

                    return new JsonResult(new
                    {
                        success = true,
                        state = CreatePlayerState(game, question, review, player)
                    });
                }
            });
    }

    public IActionResult OnPostEmptyAnswer(
        string code,
        int sourceQuestionId,
        Guid playerId)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindQuestion(game, sourceQuestionId);
            var runtimePlayerId = new GamePlayerId(playerId);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            if (!IsAnswering(review) ||
                !IsActiveParticipant(game, review, runtimePlayerId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            if (!review.Answers.ContainsKey(runtimePlayerId))
            {
                game.Session.AddQuestionAnswerHistoryEntry(
                    sourceQuestionId,
                    runtimePlayerId,
                    isCorrect: false,
                    value: 0,
                    resolveQuestionIfAvailable: false);
                review.Answers[runtimePlayerId] = "—";
                game.MarkPersistenceChanged();
                EnsureLifecycle(game, question, review);
            }

            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question, review)
            });
        }
    }

    public IActionResult OnPostExclude(
        string code,
        int sourceQuestionId,
        Guid playerId)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindQuestion(game, sourceQuestionId);
            var excludedId = new GamePlayerId(playerId);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            if (IsAnswering(review) || IsShowingResults(review))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var authorId = GetCurrentRatingAnswerPlayerId(review);
            if (authorId is null ||
                excludedId == authorId.Value ||
                !review.ParticipantIds.Contains(excludedId) ||
                review.ExcludedPlayerIds.Contains(excludedId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var currentRatings = GetOrCreateRatings(review, authorId.Value);
            if (currentRatings.ContainsKey(excludedId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            review.ExcludedPlayerIds.Add(excludedId);
            RemoveRatingsByPlayer(review, excludedId);
            ForceExcludedAuthorScoreToZero(game, question, review, excludedId);
            CompleteCurrentRatingIfReady(review);
            game.MarkPersistenceChanged();

            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question, review)
            });
        }
    }

    public IActionResult OnPostNext(
        string code,
        int sourceQuestionId)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindQuestion(game, sourceQuestionId);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            if (IsAnswering(review) || IsShowingResults(review))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var currentId = GetCurrentRatingAnswerPlayerId(review);
            if (currentId is null ||
                !review.CompletedAnswerPlayerIds.Contains(currentId.Value))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            if (MoveToNextRatingAnswer(review))
            {
                CompleteCurrentRatingIfReady(review);
            }
            else
            {
                if (!AreAllRatingAnswersComplete(review))
                {
                    return StatusCode(StatusCodes.Status409Conflict);
                }

                BeginResults(game, question, review);
            }

            game.MarkPersistenceChanged();
            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question, review)
            });
        }
    }

    public IActionResult OnPostNextResult(
        string code,
        int sourceQuestionId)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindQuestion(game, sourceQuestionId);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            if (!IsShowingResults(review) || !MoveToNextResult(review))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            ApplyCurrentResultScore(game, question, review);
            game.MarkPersistenceChanged();
            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question, review)
            });
        }
    }

    public IActionResult OnPostReturnToBoard(
        string code,
        int sourceQuestionId)
    {
        var game = FindOwnedGame(code);
        if (game is null)
        {
            return Forbid();
        }

        lock (game)
        {
            var question = FindQuestion(game, sourceQuestionId);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetReview(game, question);
            EnsureLifecycle(game, question, review);
            if (!IsShowingResults(review) || HasNextResult(review))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            ApplyCurrentResultScore(game, question, review);
            game.Session.ResolveQuestionWithoutCorrectAnswer(sourceQuestionId);
            game.Session.CloseQuestionAnswer(sourceQuestionId);
            game.MarkPersistenceChanged();

            return new JsonResult(new
            {
                success = true,
                state = new { active = false }
            });
        }
    }

    private IActionResult WithAuthenticatedPlayer(
        string code,
        Guid playerId,
        string accessToken,
        Func<GameSessionRegistration, GamePlayer, IActionResult> action)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Forbid();
        }

        var connectionId = $"peer-rated-all-player-http:{Guid.NewGuid():N}";
        var connection = sessionRegistry.ConnectPlayer(
            code,
            accessToken,
            connectionId,
            isVisible: false);
        if (connection is null)
        {
            return Forbid();
        }

        try
        {
            if (connection.RequiresApproval ||
                connection.Player.Id != new GamePlayerId(playerId))
            {
                return Forbid();
            }

            return action(connection.Game, connection.Player);
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(connectionId);
        }
    }

    private static RuntimeQuestion? FindCurrentQuestion(
        GameSessionRegistration game) => game.Session.Board.Questions
        .FirstOrDefault(question =>
            question.PresentationType ==
                QuestionPresentationType.AllPlayerPeerRatedText &&
            question.Status is
                RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active);

    private static RuntimeQuestion? FindQuestion(
        GameSessionRegistration game,
        int sourceQuestionId) => game.Session.Board.Questions
        .SingleOrDefault(question =>
            question.SourceQuestionId == sourceQuestionId &&
            question.PresentationType ==
                QuestionPresentationType.AllPlayerPeerRatedText &&
            question.Status is
                RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active);

    private static PeerRatedAllPlayerReviewState GetReview(
        GameSessionRegistration game,
        RuntimeQuestion question) =>
        game.GetOrCreatePeerRatedAllPlayerReview(
            question,
            game.Session.Players.Select(player => player.Id));

    private static void EnsureLifecycle(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        var changed = SynchronizeRemovedPlayers(game, question, review);

        if (IsAnswering(review))
        {
            // Rating must never advance while answers are still being collected.
            // The previous implementation normalized ReviewIndex here and could
            // permanently move it past every participant before the first answer
            // existed, leaving the game stuck at review 0/N.
            if (review.CompletedAnswerPlayerIds.Count == 0 && review.ReviewIndex != 0)
            {
                review.ReviewIndex = 0;
                changed = true;
            }
        }
        else
        {
            game.Session.Timer.Stop();
            game.Session.AnswerTimer.Stop();

            if (!IsShowingResults(review))
            {
                if (NormalizeRatingPosition(review))
                {
                    changed = true;
                }

                if (CompleteCurrentRatingIfReady(review))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    private static bool SynchronizeRemovedPlayers(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        var currentIds = game.Session.Players
            .Select(player => player.Id)
            .ToHashSet();
        var newlyExcluded = review.ParticipantIds
            .Where(playerId =>
                !currentIds.Contains(playerId) &&
                !review.ExcludedPlayerIds.Contains(playerId))
            .ToArray();
        if (newlyExcluded.Length == 0)
        {
            return false;
        }

        foreach (var playerId in newlyExcluded)
        {
            review.ExcludedPlayerIds.Add(playerId);
            RemoveRatingsByPlayer(review, playerId);
            ForceExcludedAuthorScoreToZero(game, question, review, playerId);
        }

        return true;
    }

    private static bool IsAnswering(PeerRatedAllPlayerReviewState review)
    {
        var activeIds = review.ParticipantIds
            .Where(playerId => !review.ExcludedPlayerIds.Contains(playerId))
            .ToArray();
        return activeIds.Length == 0 ||
            activeIds.Any(playerId => !review.Answers.ContainsKey(playerId));
    }

    private static bool IsActiveParticipant(
        GameSessionRegistration game,
        PeerRatedAllPlayerReviewState review,
        GamePlayerId playerId) =>
        review.ParticipantIds.Contains(playerId) &&
        !review.ExcludedPlayerIds.Contains(playerId) &&
        game.Session.Players.Any(player => player.Id == playerId);

    private static GamePlayerId[] GetRatingAnswerIds(
        PeerRatedAllPlayerReviewState review) => review.ParticipantIds
        .Where(playerId =>
            !review.ExcludedPlayerIds.Contains(playerId) &&
            review.Answers.ContainsKey(playerId))
        .ToArray();

    private static GamePlayerId[] GetResultAnswerIds(
        PeerRatedAllPlayerReviewState review) => review.ParticipantIds
        .Where(review.Answers.ContainsKey)
        .ToArray();

    private static bool AreAllRatingAnswersComplete(
        PeerRatedAllPlayerReviewState review)
    {
        var answerIds = GetRatingAnswerIds(review);
        return answerIds.Length > 0 &&
            answerIds.All(review.CompletedAnswerPlayerIds.Contains);
    }

    private static bool IsShowingResults(
        PeerRatedAllPlayerReviewState review) =>
        !IsAnswering(review) &&
        review.ReviewIndex >= review.ParticipantIds.Count &&
        AreAllRatingAnswersComplete(review);

    private static bool NormalizeRatingPosition(
        PeerRatedAllPlayerReviewState review)
    {
        if (IsAnswering(review) || IsShowingResults(review))
        {
            return false;
        }

        var changed = false;
        if (review.ReviewIndex >= review.ParticipantIds.Count)
        {
            var firstIncompleteIndex = review.ParticipantIds.FindIndex(playerId =>
                !review.ExcludedPlayerIds.Contains(playerId) &&
                review.Answers.ContainsKey(playerId) &&
                !review.CompletedAnswerPlayerIds.Contains(playerId));
            review.ReviewIndex = firstIncompleteIndex >= 0 ? firstIncompleteIndex : 0;
            changed = true;
        }

        while (review.ReviewIndex < review.ParticipantIds.Count)
        {
            var playerId = review.ParticipantIds[review.ReviewIndex];
            if (!review.ExcludedPlayerIds.Contains(playerId) &&
                review.Answers.ContainsKey(playerId))
            {
                break;
            }

            review.ReviewIndex++;
            changed = true;
        }

        return changed;
    }

    private static GamePlayerId? GetCurrentRatingAnswerPlayerId(
        PeerRatedAllPlayerReviewState review)
    {
        if (IsAnswering(review) || IsShowingResults(review))
        {
            return null;
        }

        NormalizeRatingPosition(review);
        return review.ReviewIndex < review.ParticipantIds.Count
            ? review.ParticipantIds[review.ReviewIndex]
            : null;
    }

    private static bool MoveToNextRatingAnswer(
        PeerRatedAllPlayerReviewState review)
    {
        if (review.ReviewIndex >= review.ParticipantIds.Count)
        {
            return false;
        }

        for (var index = review.ReviewIndex + 1;
             index < review.ParticipantIds.Count;
             index++)
        {
            var playerId = review.ParticipantIds[index];
            if (!review.ExcludedPlayerIds.Contains(playerId) &&
                review.Answers.ContainsKey(playerId))
            {
                review.ReviewIndex = index;
                return true;
            }
        }

        return false;
    }

    private static bool HasNextRatingAnswer(
        PeerRatedAllPlayerReviewState review)
    {
        if (review.ReviewIndex >= review.ParticipantIds.Count)
        {
            return false;
        }

        for (var index = review.ReviewIndex + 1;
             index < review.ParticipantIds.Count;
             index++)
        {
            var playerId = review.ParticipantIds[index];
            if (!review.ExcludedPlayerIds.Contains(playerId) &&
                review.Answers.ContainsKey(playerId))
            {
                return true;
            }
        }

        return false;
    }

    private static void BeginResults(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        review.ReviewIndex = review.ParticipantIds.Count;
        ApplyCurrentResultScore(game, question, review);
    }

    private static int GetResultOrdinal(PeerRatedAllPlayerReviewState review) =>
        Math.Max(0, review.ReviewIndex - review.ParticipantIds.Count);

    private static GamePlayerId? GetCurrentResultAnswerPlayerId(
        PeerRatedAllPlayerReviewState review)
    {
        if (!IsShowingResults(review))
        {
            return null;
        }

        var answerIds = GetResultAnswerIds(review);
        var ordinal = GetResultOrdinal(review);
        return ordinal < answerIds.Length ? answerIds[ordinal] : null;
    }

    private static bool MoveToNextResult(PeerRatedAllPlayerReviewState review)
    {
        var answerIds = GetResultAnswerIds(review);
        var ordinal = GetResultOrdinal(review);
        if (ordinal + 1 >= answerIds.Length)
        {
            return false;
        }

        review.ReviewIndex++;
        return true;
    }

    private static bool HasNextResult(PeerRatedAllPlayerReviewState review)
    {
        var answerIds = GetResultAnswerIds(review);
        return GetResultOrdinal(review) + 1 < answerIds.Length;
    }

    private static Dictionary<GamePlayerId, int> GetOrCreateRatings(
        PeerRatedAllPlayerReviewState review,
        GamePlayerId answerPlayerId)
    {
        if (!review.RatingsByAnswerPlayer.TryGetValue(
                answerPlayerId,
                out var ratings))
        {
            ratings = [];
            review.RatingsByAnswerPlayer[answerPlayerId] = ratings;
        }

        return ratings;
    }

    private static GamePlayerId[] GetRequiredRaterIds(
        PeerRatedAllPlayerReviewState review,
        GamePlayerId answerPlayerId) => review.ParticipantIds
        .Where(playerId =>
            playerId != answerPlayerId &&
            !review.ExcludedPlayerIds.Contains(playerId))
        .ToArray();

    private static bool CompleteCurrentRatingIfReady(
        PeerRatedAllPlayerReviewState review)
    {
        if (IsAnswering(review) || IsShowingResults(review))
        {
            return false;
        }

        var answerPlayerId = GetCurrentRatingAnswerPlayerId(review);
        if (answerPlayerId is null ||
            review.CompletedAnswerPlayerIds.Contains(answerPlayerId.Value))
        {
            return false;
        }

        var requiredRaters = GetRequiredRaterIds(review, answerPlayerId.Value);
        var ratings = GetOrCreateRatings(review, answerPlayerId.Value);
        if (requiredRaters.Any(raterId => !ratings.ContainsKey(raterId)))
        {
            return false;
        }

        review.CompletedAnswerPlayerIds.Add(answerPlayerId.Value);
        return true;
    }

    private static void ApplyCurrentResultScore(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        var answerPlayerId = GetCurrentResultAnswerPlayerId(review);
        if (answerPlayerId is { } playerId)
        {
            ApplyCalculatedScore(game, question, review, playerId);
        }
    }

    private static void ApplyCalculatedScore(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review,
        GamePlayerId answerPlayerId)
    {
        var attempt = question.AnswerAttempts.SingleOrDefault(item =>
            item.PlayerId == answerPlayerId);
        if (attempt is null)
        {
            return;
        }

        var points = review.ExcludedPlayerIds.Contains(answerPlayerId)
            ? 0
            : PeerRatedAllPlayerScoring.CalculateAwardedPoints(
                question.Points,
                CalculateAverageStars(review, answerPlayerId));

        game.Session.UpdateQuestionAnswerHistoryEntry(
            question.SourceQuestionId,
            attempt.Id,
            answerPlayerId,
            isCorrect: points > 0,
            value: points);
    }

    private static void ForceExcludedAuthorScoreToZero(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review,
        GamePlayerId excludedId)
    {
        if (!review.Answers.ContainsKey(excludedId))
        {
            return;
        }

        var attempt = question.AnswerAttempts.SingleOrDefault(item =>
            item.PlayerId == excludedId);
        if (attempt is null)
        {
            return;
        }

        game.Session.UpdateQuestionAnswerHistoryEntry(
            question.SourceQuestionId,
            attempt.Id,
            excludedId,
            isCorrect: false,
            value: 0);
    }

    private static double CalculateAverageStars(
        PeerRatedAllPlayerReviewState review,
        GamePlayerId answerPlayerId)
    {
        var requiredRaters = GetRequiredRaterIds(review, answerPlayerId);
        if (requiredRaters.Length == 0 ||
            !review.RatingsByAnswerPlayer.TryGetValue(answerPlayerId, out var ratings))
        {
            return 0;
        }

        var values = requiredRaters
            .Where(ratings.ContainsKey)
            .Select(raterId => ratings[raterId])
            .ToArray();
        return values.Length == 0 ? 0 : values.Average();
    }

    private static void RemoveRatingsByPlayer(
        PeerRatedAllPlayerReviewState review,
        GamePlayerId playerId)
    {
        foreach (var ratings in review.RatingsByAnswerPlayer.Values)
        {
            ratings.Remove(playerId);
        }
    }

    private static object CreateHostState(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        var answering = IsAnswering(review);
        var showingResults = !answering && IsShowingResults(review);
        var phase = answering ? "answering" : showingResults ? "results" : "rating";
        var currentAnswerPlayerId = answering
            ? (GamePlayerId?)null
            : showingResults
                ? GetCurrentResultAnswerPlayerId(review)
                : GetCurrentRatingAnswerPlayerId(review);
        var currentPlayer = currentAnswerPlayerId is { } authorId
            ? game.Session.AllPlayers.SingleOrDefault(player => player.Id == authorId)
            : null;
        var ratings = currentAnswerPlayerId is { } currentId
            ? GetOrCreateRatings(review, currentId)
            : null;
        var ratingComplete = !answering && !showingResults &&
            currentAnswerPlayerId is { } completedId &&
            review.CompletedAnswerPlayerIds.Contains(completedId);
        var averageStars = showingResults && currentAnswerPlayerId is { } averageId
            ? CalculateAverageStars(review, averageId)
            : (double?)null;
        var percentage = averageStars.HasValue &&
            currentAnswerPlayerId is { } percentageId &&
            !review.ExcludedPlayerIds.Contains(percentageId)
                ? PeerRatedAllPlayerScoring.CalculateRewardPercentage(averageStars.Value)
                : averageStars.HasValue ? 0 : (int?)null;
        var awardedPoints = averageStars.HasValue &&
            currentAnswerPlayerId is { } pointsId &&
            !review.ExcludedPlayerIds.Contains(pointsId)
                ? PeerRatedAllPlayerScoring.CalculateAwardedPoints(
                    question.Points,
                    averageStars.Value)
                : averageStars.HasValue ? 0 : (int?)null;

        var players = review.ParticipantIds.Select(playerId =>
        {
            var player = game.Session.AllPlayers.SingleOrDefault(item =>
                item.Id == playerId);
            return new
            {
                id = playerId.Value,
                name = player?.Name ?? "—",
                submitted = review.Answers.ContainsKey(playerId),
                excluded = review.ExcludedPlayerIds.Contains(playerId)
            };
        }).ToArray();

        var raters = currentAnswerPlayerId is null
            ? Array.Empty<object>()
            : review.ParticipantIds
                .Where(playerId => playerId != currentAnswerPlayerId.Value)
                .Select(playerId =>
                {
                    var player = game.Session.AllPlayers.SingleOrDefault(item =>
                        item.Id == playerId);
                    var excluded = review.ExcludedPlayerIds.Contains(playerId);
                    int? rating = ratings is not null &&
                        ratings.TryGetValue(playerId, out var value)
                            ? value
                            : null;
                    return (object)new
                    {
                        id = playerId.Value,
                        name = player?.Name ?? "—",
                        excluded,
                        rating,
                        canExclude = phase == "rating" &&
                            !excluded &&
                            rating is null
                    };
                })
                .ToArray();

        var ratingAnswerIds = GetRatingAnswerIds(review);
        var resultAnswerIds = GetResultAnswerIds(review);
        var ratingPosition = phase == "rating" && currentAnswerPlayerId is { } ratingId
            ? Array.IndexOf(ratingAnswerIds, ratingId) + 1
            : 0;
        var resultPosition = phase == "results" && currentAnswerPlayerId is { } resultId
            ? Array.IndexOf(resultAnswerIds, resultId) + 1
            : 0;

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            phase,
            answeredCount = players.Count(player => player.submitted),
            playerCount = players.Length,
            players,
            reviewPosition = ratingPosition,
            reviewCount = ratingAnswerIds.Length,
            resultPosition,
            resultCount = resultAnswerIds.Length,
            reviewSubmission = currentAnswerPlayerId is { } submissionId
                ? new
                {
                    id = submissionId.Value,
                    name = currentPlayer?.Name ?? "—",
                    answer = review.Answers.GetValueOrDefault(submissionId, "—"),
                    excluded = review.ExcludedPlayerIds.Contains(submissionId)
                }
                : null,
            raters,
            ratingComplete,
            averageStars,
            rewardPercentage = percentage,
            awardedPoints,
            hasNextAnswer = ratingComplete && HasNextRatingAnswer(review),
            canShowResults = ratingComplete &&
                !HasNextRatingAnswer(review) &&
                AreAllRatingAnswersComplete(review),
            hasNextResult = showingResults && HasNextResult(review),
            canReturnToBoard = showingResults && !HasNextResult(review)
        };
    }

    private static object CreatePlayerState(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review,
        GamePlayer player)
    {
        var answering = IsAnswering(review);
        var showingResults = !answering && IsShowingResults(review);
        var phase = answering ? "answering" : showingResults ? "results" : "rating";
        var excluded = review.ExcludedPlayerIds.Contains(player.Id);
        var currentAnswerPlayerId = answering
            ? (GamePlayerId?)null
            : showingResults
                ? GetCurrentResultAnswerPlayerId(review)
                : GetCurrentRatingAnswerPlayerId(review);
        var ratings = currentAnswerPlayerId is { } authorId
            ? GetOrCreateRatings(review, authorId)
            : null;
        int? rating = ratings is not null &&
            ratings.TryGetValue(player.Id, out var value)
                ? value
                : null;
        var author = currentAnswerPlayerId is { } currentId
            ? game.Session.AllPlayers.SingleOrDefault(item => item.Id == currentId)
            : null;
        var averageStars = showingResults && currentAnswerPlayerId is { } averageId
            ? CalculateAverageStars(review, averageId)
            : (double?)null;
        var awardedPoints = averageStars.HasValue &&
            currentAnswerPlayerId is { } pointsId &&
            !review.ExcludedPlayerIds.Contains(pointsId)
                ? PeerRatedAllPlayerScoring.CalculateAwardedPoints(
                    question.Points,
                    averageStars.Value)
                : averageStars.HasValue ? 0 : (int?)null;

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            phase,
            excluded,
            hasSubmitted = review.Answers.ContainsKey(player.Id),
            reviewSubmission = currentAnswerPlayerId is { } submissionId
                ? new
                {
                    id = submissionId.Value,
                    name = author?.Name ?? "—",
                    answer = review.Answers.GetValueOrDefault(submissionId, "—"),
                    excluded = review.ExcludedPlayerIds.Contains(submissionId)
                }
                : null,
            isAuthor = currentAnswerPlayerId == player.Id,
            canRate = phase == "rating" &&
                !excluded &&
                currentAnswerPlayerId is { } answerId &&
                answerId != player.Id &&
                rating is null,
            hasRated = phase == "rating" && rating.HasValue,
            rating,
            averageStars,
            awardedPoints
        };
    }

    private static IActionResult ConflictState(
        GameSessionRegistration game,
        GamePlayer player)
    {
        var question = FindCurrentQuestion(game);
        if (question is null)
        {
            return new JsonResult(new
            {
                success = false,
                error = "The peer-rated question is no longer active.",
                state = new { active = false }
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }

        var review = GetReview(game, question);
        EnsureLifecycle(game, question, review);
        return new JsonResult(new
        {
            success = false,
            error = "The requested peer-rating action is not available.",
            state = CreatePlayerState(game, question, review, player)
        })
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    private GameSessionRegistration? FindOwnedGame(string code)
    {
        var game = sessionRegistry.Find(code);
        var hostId = currentHost.Id;
        return game is not null &&
            !string.IsNullOrWhiteSpace(hostId) &&
            string.Equals(game.HostId, hostId, StringComparison.Ordinal)
                ? game
                : null;
    }
}
