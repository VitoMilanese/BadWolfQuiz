using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class AllPlayerQuestionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    QuizDbContext db) : PageModel
{
    private const int MaximumAnswerLength = 500;
    private const string ApiPath = "/api/all-player-question";
    public async Task<IActionResult> OnGetEditorAsync(
        int questionId,
        CancellationToken cancellationToken)
    {
        var hostId = currentHost.Id;
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return Forbid();
        }

        var question = await db.QuizQuestions
            .AsNoTracking()
            .Include(item => item.AnswerBlocks)
            .SingleOrDefaultAsync(item =>
                item.Id == questionId &&
                item.Category.Round.Quiz.HostId == hostId,
                cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var presentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(
                question);

        return new JsonResult(new
        {
            presentationType = (int)presentationType
        });
    }

    public IActionResult OnGetHost(string code)
    {
        var game = sessionRegistry.Find(code);
        var hostId = currentHost.Id;

        if (game is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(hostId) ||
            !string.Equals(game.HostId, hostId, StringComparison.Ordinal))
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

            EnsureQuestionLifecycle(game, question);
            question = FindCurrentQuestion(game);

            return question is null
                ? new JsonResult(new { active = false })
                : new JsonResult(CreateHostState(game, question));
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

            EnsureQuestionLifecycle(game, question);
            question = FindCurrentQuestion(game);

            return question is null
                ? new JsonResult(new { active = false })
                : new JsonResult(CreatePlayerState(game, question, player));
        }
    }

    public IActionResult OnGetOptionImage(
        string code,
        int sourceQuestionId,
        int sourceContentBlockId)
    {
        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return NotFound();
        }

        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active or
                    RuntimeQuestionStatus.ShowingAnswer);
            var block = question?.AnswerBlocks.SingleOrDefault(item =>
                item.SourceContentBlockId == sourceContentBlockId &&
                item.Kind == ContentBlockKind.Image);

            if (block?.FileData is null ||
                block.FileData.Length == 0 ||
                string.IsNullOrWhiteSpace(block.FileContentType))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=300";
            return File(block.FileData, block.FileContentType);
        }
    }

    public IActionResult OnPostSubmit(
        string code,
        Guid playerId,
        string accessToken,
        int sourceQuestionId,
        string answer)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Forbid();
        }

        var validationConnectionId = $"all-player-http:{Guid.NewGuid():N}";
        var connection = sessionRegistry.ConnectPlayer(
            code,
            accessToken,
            validationConnectionId,
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

            var game = connection.Game;
            lock (game)
            {
                var question = game.Session.Board.Questions.SingleOrDefault(item =>
                    item.SourceQuestionId == sourceQuestionId &&
                    item.IsAllPlayerQuestion);

                if (question is null ||
                    question.Status is not RuntimeQuestionStatus.Selected and
                        not RuntimeQuestionStatus.Active)
                {
                    return ConflictState(game, connection.Player);
                }

                EnsureQuestionLifecycle(game, question);
                if (question.Status == RuntimeQuestionStatus.ShowingAnswer)
                {
                    return ConflictState(game, connection.Player);
                }

                var existingAttempt = question.AnswerAttempts.SingleOrDefault(item =>
                    item.PlayerId == connection.Player.Id);
                if (existingAttempt is not null)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        duplicate = true,
                        state = CreatePlayerState(game, question, connection.Player)
                    });
                }

                if (question.PresentationType == QuestionPresentationType.AllPlayerText)
                {
                    var review = GetTextReview(game, question);
                    if (!review.Accepting)
                    {
                        return ConflictState(game, connection.Player);
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
                        connection.Player.Id,
                        isCorrect: false,
                        value: 0,
                        resolveQuestionIfAvailable: false);
                    review.Answers[connection.Player.Id] = normalizedAnswer;

                    if (AllCurrentPlayersSubmitted(game, question))
                    {
                        review.Accepting = false;
                        game.Session.Timer.Stop();
                    }
                }
                else
                {
                    if (!int.TryParse(answer, out var selectedBlockId))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            error = "The selected answer is invalid."
                        });
                    }

                    var selectedBlock = question.AnswerBlocks.SingleOrDefault(block =>
                        block.SourceContentBlockId == selectedBlockId);
                    if (selectedBlock is null)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            error = "The selected answer is not an available option."
                        });
                    }

                    var isCorrect = selectedBlock.SourceContentBlockId ==
                        question.AnswerBlocks[0].SourceContentBlockId;
                    game.Session.AddQuestionAnswerHistoryEntry(
                        sourceQuestionId,
                        connection.Player.Id,
                        isCorrect,
                        isCorrect ? question.Points : 0,
                        resolveQuestionIfAvailable: false);

                    if (AllCurrentPlayersSubmitted(game, question))
                    {
                        game.Session.ResolveQuestionWithoutCorrectAnswer(sourceQuestionId);
                    }
                }

                game.MarkPersistenceChanged();

                return new JsonResult(new
                {
                    success = true,
                    duplicate = false,
                    state = CreatePlayerState(game, question, connection.Player)
                });
            }
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(validationConnectionId);
        }
    }

    public IActionResult OnPostJudge(
        string code,
        int sourceQuestionId,
        Guid playerId,
        bool isCorrect)
    {
        var game = sessionRegistry.Find(code);
        var hostId = currentHost.Id;
        if (game is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(hostId) ||
            !string.Equals(game.HostId, hostId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.PresentationType == QuestionPresentationType.AllPlayerText &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active);
            if (question is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetTextReview(game, question);
            var runtimePlayerId = new GamePlayerId(playerId);
            if (review.Accepting ||
                !review.Answers.ContainsKey(runtimePlayerId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var attempt = question.AnswerAttempts.SingleOrDefault(item =>
                item.PlayerId == runtimePlayerId);
            if (attempt is null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            if (!review.JudgedPlayers.Contains(runtimePlayerId))
            {
                game.Session.UpdateQuestionAnswerHistoryEntry(
                    sourceQuestionId,
                    attempt.Id,
                    runtimePlayerId,
                    isCorrect,
                    isCorrect ? question.Points : 0);
                review.JudgedPlayers.Add(runtimePlayerId);
                game.MarkPersistenceChanged();
            }

            if (AllSubmittedCurrentPlayersJudged(game, question, review))
            {
                game.Session.ResolveQuestionWithoutCorrectAnswer(sourceQuestionId);
                game.MarkPersistenceChanged();
            }

            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question)
            });
        }
    }

    private static IActionResult ConflictState(
        GameSessionRegistration game,
        GamePlayer player)
    {
        var currentQuestion = FindCurrentQuestion(game);
        return new JsonResult(new
        {
            success = false,
            error = "The all-player question is no longer accepting answers.",
            state = currentQuestion is null
                ? new { active = false }
                : CreatePlayerState(game, currentQuestion, player)
        })
        {
            StatusCode = StatusCodes.Status409Conflict
        };
    }

    private static RuntimeQuestion? FindCurrentQuestion(
        GameSessionRegistration game) => game.Session.Board.Questions
        .FirstOrDefault(question =>
            question.IsAllPlayerQuestion &&
            question.Status is
                RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active or
                RuntimeQuestionStatus.ShowingAnswer);

    private static void EnsureQuestionLifecycle(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (question.Status is not RuntimeQuestionStatus.Selected and
            not RuntimeQuestionStatus.Active)
        {
            return;
        }

        if (game.Session.Timer.Status == GameTimerStatus.Stopped)
        {
            game.Session.ActivateQuestionBuzzer(question.SourceQuestionId);
            game.MarkPersistenceChanged();
        }

        if (AllCurrentPlayersSubmitted(game, question))
        {
            if (question.PresentationType == QuestionPresentationType.AllPlayerText)
            {
                GetTextReview(game, question).Accepting = false;
                game.Session.Timer.Stop();
                game.MarkPersistenceChanged();
            }
            else
            {
                game.Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
                game.MarkPersistenceChanged();
                return;
            }
        }

        _ = game.Session.Timer.Remaining;
        if (game.Session.Timer.Status != GameTimerStatus.Expired)
        {
            return;
        }

        if (question.PresentationType == QuestionPresentationType.AllPlayerText)
        {
            var review = GetTextReview(game, question);
            review.Accepting = false;
            if (!CurrentPlayersWithSubmissions(game, question).Any())
            {
                game.Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
            }
        }
        else
        {
            game.Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
        }

        game.MarkPersistenceChanged();
    }

    private static object CreateHostState(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        var isClosed = question.Status == RuntimeQuestionStatus.ShowingAnswer;
        var isText = question.PresentationType == QuestionPresentationType.AllPlayerText;
        var review = isText ? GetTextReview(game, question) : null;
        var attemptsByPlayer = question.AnswerAttempts
            .ToDictionary(attempt => attempt.PlayerId);
        var currentPlayers = game.Session.Players.ToArray();
        var players = currentPlayers
            .Select(player =>
            {
                attemptsByPlayer.TryGetValue(player.Id, out var attempt);
                var isJudged = review?.JudgedPlayers.Contains(player.Id) == true;
                return new
                {
                    id = player.Id.Value,
                    player.Name,
                    submitted = attempt is not null,
                    isJudged,
                    isCorrect = isClosed || isJudged ? attempt?.IsCorrect : null
                };
            })
            .ToArray();

        var judgePlayer = isText && !isClosed && review is { Accepting: false }
            ? currentPlayers.FirstOrDefault(player =>
                attemptsByPlayer.ContainsKey(player.Id) &&
                !review.JudgedPlayers.Contains(player.Id))
            : null;
        object? judgeSubmission = null;
        if (judgePlayer is not null)
        {
            review!.Answers.TryGetValue(judgePlayer.Id, out var submittedAnswer);
            judgeSubmission = new
            {
                id = judgePlayer.Id.Value,
                judgePlayer.Name,
                answer = submittedAnswer ?? "—"
            };
        }

        var options = question.PresentationType ==
            QuestionPresentationType.AllPlayerMultipleChoice
                ? ShuffleOptions(
                    CreateChoiceOptions(game, question),
                    HashCode.Combine(
                        question.SourceQuestionId,
                        game.Session.Id.Value.GetHashCode()))
                : Array.Empty<ChoiceOption>();

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            mode = GetMode(question),
            isClosed,
            isAccepting = isText ? review!.Accepting : !isClosed,
            isJudging = judgeSubmission is not null,
            answeredCount = currentPlayers.Count(player =>
                attemptsByPlayer.ContainsKey(player.Id)),
            playerCount = currentPlayers.Length,
            remainingMilliseconds = GetRemainingMilliseconds(game, question, review),
            options,
            judgeSubmission,
            players
        };
    }

    private static object CreatePlayerState(
        GameSessionRegistration game,
        RuntimeQuestion question,
        GamePlayer player)
    {
        var attempt = question.AnswerAttempts.SingleOrDefault(item =>
            item.PlayerId == player.Id);
        var isClosed = question.Status == RuntimeQuestionStatus.ShowingAnswer;
        var isText = question.PresentationType == QuestionPresentationType.AllPlayerText;
        var review = isText ? GetTextReview(game, question) : null;
        var isAccepting = !isClosed && (!isText || review!.Accepting);

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            mode = GetMode(question),
            options = question.PresentationType ==
                QuestionPresentationType.AllPlayerMultipleChoice
                    ? ShuffleOptions(
                        CreateChoiceOptions(game, question),
                        HashCode.Combine(
                            question.SourceQuestionId,
                            player.Id.Value.GetHashCode()))
                    : Array.Empty<ChoiceOption>(),
            hasSubmitted = attempt is not null,
            isAccepting,
            isJudging = isText && !isClosed && !review!.Accepting,
            isClosed,
            isCorrect = isClosed ? attempt?.IsCorrect : null,
            remainingMilliseconds = GetRemainingMilliseconds(game, question, review)
        };
    }

    private static ChoiceOption[] CreateChoiceOptions(
        GameSessionRegistration game,
        RuntimeQuestion question) => question.AnswerBlocks
        .Select(block => new ChoiceOption(
            block.SourceContentBlockId,
            block.Kind == ContentBlockKind.Image ? "image" : "text",
            block.Kind == ContentBlockKind.Text ? block.TextContent?.Trim() : null,
            block.Kind == ContentBlockKind.Image
                ? BuildOptionImageUrl(game, question, block)
                : null))
        .ToArray();

    private static string BuildOptionImageUrl(
        GameSessionRegistration game,
        RuntimeQuestion question,
        ContentBlockSnapshot block) =>
        $"{ApiPath}?handler=OptionImage" +
        $"&code={Uri.EscapeDataString(game.PublicCode)}" +
        $"&sourceQuestionId={question.SourceQuestionId}" +
        $"&sourceContentBlockId={block.SourceContentBlockId}";

    private static ChoiceOption[] ShuffleOptions(
        IReadOnlyList<ChoiceOption> options,
        int seed)
    {
        var shuffled = options.ToArray();
        var random = new Random(seed);
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (shuffled[index], shuffled[swapIndex]) =
                (shuffled[swapIndex], shuffled[index]);
        }

        return shuffled;
    }

    private static int GetRemainingMilliseconds(
        GameSessionRegistration game,
        RuntimeQuestion question,
        AllPlayerTextReviewState? review = null)
    {
        if (question.Status == RuntimeQuestionStatus.ShowingAnswer ||
            review is { Accepting: false })
        {
            return 0;
        }

        _ = game.Session.Timer.Remaining;
        return game.Session.Timer.Status == GameTimerStatus.Expired
            ? 0
            : Math.Max(
                0,
                (int)Math.Ceiling(game.Session.Timer.Remaining.TotalMilliseconds));
    }

    private static string GetMode(RuntimeQuestion question) =>
        question.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice
            ? "multipleChoice"
            : "text";

    private static bool AllCurrentPlayersSubmitted(
        GameSessionRegistration game,
        RuntimeQuestion question) =>
        game.Session.Players.Count > 0 &&
        game.Session.Players.All(player =>
            question.AnswerAttempts.Any(attempt => attempt.PlayerId == player.Id));

    private static IEnumerable<GamePlayer> CurrentPlayersWithSubmissions(
        GameSessionRegistration game,
        RuntimeQuestion question) => game.Session.Players.Where(player =>
        question.AnswerAttempts.Any(attempt => attempt.PlayerId == player.Id));

    private static bool AllSubmittedCurrentPlayersJudged(
        GameSessionRegistration game,
        RuntimeQuestion question,
        AllPlayerTextReviewState review)
    {
        var submittedPlayers = CurrentPlayersWithSubmissions(game, question).ToArray();
        return submittedPlayers.Length > 0 &&
            submittedPlayers.All(player => review.JudgedPlayers.Contains(player.Id));
    }

    private static AllPlayerTextReviewState GetTextReview(
        GameSessionRegistration game,
        RuntimeQuestion question) =>
        game.GetOrCreateAllPlayerTextReview(question);

    private sealed record ChoiceOption(
        int Id,
        string Kind,
        string? Text,
        string? ImageUrl);
}
