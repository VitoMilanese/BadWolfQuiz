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

    public IActionResult OnPostWager(
        string code,
        Guid playerId,
        string accessToken,
        int sourceQuestionId,
        int amount)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Forbid();
        }

        var validationConnectionId = $"all-player-wager-http:{Guid.NewGuid():N}";
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
                    item.IsSpecial &&
                    item.IsAllPlayerQuestion &&
                    item.Status == RuntimeQuestionStatus.AwaitingWager);
                if (question is null)
                {
                    return ConflictState(game, connection.Player);
                }

                if (question.AllPlayerWagers.Any(wager =>
                        wager.PlayerId == connection.Player.Id))
                {
                    return new JsonResult(new
                    {
                        success = true,
                        duplicate = true,
                        state = CreatePlayerState(game, question, connection.Player)
                    });
                }

                try
                {
                    game.Session.SubmitAllPlayerQuestionWager(
                        sourceQuestionId,
                        connection.Player.Id,
                        amount);
                }
                catch (GameRuleViolationException exception)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = exception.Message,
                        state = CreatePlayerState(game, question, connection.Player)
                    });
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

    public IActionResult OnPostMinimumWager(
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
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.IsSpecial &&
                item.IsAllPlayerQuestion &&
                item.Status == RuntimeQuestionStatus.AwaitingWager);
            var runtimePlayerId = new GamePlayerId(playerId);
            if (question is null ||
                game.Session.Players.All(player => player.Id != runtimePlayerId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            if (question.AllPlayerWagers.All(wager =>
                    wager.PlayerId != runtimePlayerId))
            {
                var limits = game.Session.GetAllPlayerQuestionWagerLimits(
                    sourceQuestionId,
                    runtimePlayerId);
                game.Session.SubmitAllPlayerQuestionWager(
                    sourceQuestionId,
                    runtimePlayerId,
                    limits.Minimum);
                game.MarkPersistenceChanged();
            }

            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question)
            });
        }
    }

    public IActionResult OnPostStartQuestion(
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
            try
            {
                var question = game.Session.StartAllPlayerQuestionAfterWagers(
                    sourceQuestionId);
                game.MarkPersistenceChanged();
                return new JsonResult(new
                {
                    success = true,
                    state = CreateHostState(game, question)
                });
            }
            catch (GameRuleViolationException exception)
            {
                return BadRequest(new
                {
                    success = false,
                    error = exception.Message
                });
            }
        }
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
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.PresentationType == QuestionPresentationType.AllPlayerText &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active);
            var runtimePlayerId = new GamePlayerId(playerId);
            if (question is null ||
                CurrentParticipants(game, question).All(player =>
                    player.Id != runtimePlayerId))
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            var review = GetTextReview(game, question);
            if (question.AnswerAttempts.All(attempt =>
                    attempt.PlayerId != runtimePlayerId))
            {
                game.Session.AddQuestionAnswerHistoryEntry(
                    sourceQuestionId,
                    runtimePlayerId,
                    isCorrect: false,
                    value: 0,
                    resolveQuestionIfAvailable: false);
                review.Answers[runtimePlayerId] = "-";
                game.MarkPersistenceChanged();
            }

            if (AllCurrentPlayersSubmitted(game, question))
            {
                review.Accepting = false;
                GetAnsweringTimer(game, question).Stop();
            }

            return new JsonResult(new
            {
                success = true,
                state = CreateHostState(game, question)
            });
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
                        not RuntimeQuestionStatus.Active ||
                    CurrentParticipants(game, question).All(player =>
                        player.Id != connection.Player.Id))
                {
                    return ConflictState(game, connection.Player);
                }

                EnsureQuestionLifecycle(game, question);
                if (question.Status == RuntimeQuestionStatus.ShowingAnswer ||
                    HasAnsweringTimerExpired(game, question))
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
                        GetAnsweringTimer(game, question).Stop();
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
                        GetScoreMagnitude(
                            question,
                            connection.Player.Id,
                            isCorrect),
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
                !AllCurrentPlayersSubmitted(game, question) ||
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
                    GetScoreMagnitude(
                        question,
                        runtimePlayerId,
                        isCorrect));
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
                RuntimeQuestionStatus.AwaitingWager or
                RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active or
                RuntimeQuestionStatus.ShowingAnswer);

    private static void EnsureQuestionLifecycle(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (question.Status == RuntimeQuestionStatus.AwaitingWager ||
            question.Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active)
        {
            return;
        }

        var timer = GetAnsweringTimer(game, question);
        if (!question.IsSpecial && timer.Status == GameTimerStatus.Stopped)
        {
            game.Session.ActivateQuestionBuzzer(question.SourceQuestionId);
            game.MarkPersistenceChanged();
            timer = GetAnsweringTimer(game, question);
        }

        if (AllCurrentPlayersSubmitted(game, question))
        {
            if (question.PresentationType == QuestionPresentationType.AllPlayerText)
            {
                GetTextReview(game, question).Accepting = false;
                timer.Stop();
                game.MarkPersistenceChanged();
            }
            else
            {
                game.Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
                game.MarkPersistenceChanged();
                return;
            }
        }

        _ = timer.Remaining;
        if (timer.Status == GameTimerStatus.Expired)
        {
            // Timer expiration stops player input, but only the host advances
            // to answer review.
            return;
        }
    }

    private static object CreateHostState(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        var isWagering = question.Status == RuntimeQuestionStatus.AwaitingWager;
        var isClosed = question.Status == RuntimeQuestionStatus.ShowingAnswer;
        var isText = question.PresentationType == QuestionPresentationType.AllPlayerText;
        var review = isText ? GetTextReview(game, question) : null;
        var participants = CurrentParticipants(game, question).ToArray();
        var attemptsByPlayer = question.AnswerAttempts
            .ToDictionary(attempt => attempt.PlayerId);
        var wagersByPlayer = question.AllPlayerWagers
            .ToDictionary(wager => wager.PlayerId);
        var allSubmitted = AllCurrentPlayersSubmitted(game, question);
        var timerExpired = HasAnsweringTimerExpired(game, question);
        var phase = isWagering
            ? "wagering"
            : isClosed
                ? "closed"
                : isText && review is { Accepting: false }
                    ? allSubmitted
                        ? "judging"
                        : "awaitingMissing"
                    : "answering";

        var players = participants
            .Select(player =>
            {
                attemptsByPlayer.TryGetValue(player.Id, out var attempt);
                var isJudged = review?.JudgedPlayers.Contains(player.Id) == true;
                return new
                {
                    id = player.Id.Value,
                    player.Name,
                    wagerSubmitted = wagersByPlayer.ContainsKey(player.Id),
                    submitted = attempt is not null,
                    isJudged,
                    isCorrect = isClosed || isJudged ? attempt?.IsCorrect : null
                };
            })
            .ToArray();

        var judgePlayer = phase == "judging"
            ? participants.FirstOrDefault(player =>
                attemptsByPlayer.ContainsKey(player.Id) &&
                !review!.JudgedPlayers.Contains(player.Id))
            : null;
        object? judgeSubmission = null;
        if (judgePlayer is not null)
        {
            review!.Answers.TryGetValue(judgePlayer.Id, out var submittedAnswer);
            judgeSubmission = new
            {
                id = judgePlayer.Id.Value,
                judgePlayer.Name,
                answer = submittedAnswer ?? "-"
            };
        }

        var options = phase == "answering" &&
            question.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice
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
            phase,
            isClosed,
            isAccepting = phase == "answering" &&
                !timerExpired &&
                (!isText || review!.Accepting),
            isJudging = phase == "judging",
            wageredCount = participants.Count(player =>
                wagersByPlayer.ContainsKey(player.Id)),
            answeredCount = participants.Count(player =>
                attemptsByPlayer.ContainsKey(player.Id)),
            judgedCount = review?.JudgedPlayers.Count ?? 0,
            playerCount = participants.Length,
            canStartQuestion = isWagering &&
                participants.Length > 0 &&
                participants.All(player => wagersByPlayer.ContainsKey(player.Id)),
            remainingMilliseconds = phase == "answering"
                ? GetRemainingMilliseconds(game, question, review)
                : 0,
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
        var isWagering = question.Status == RuntimeQuestionStatus.AwaitingWager;
        var wager = question.AllPlayerWagers.SingleOrDefault(item =>
            item.PlayerId == player.Id);
        var participates = !question.IsSpecial ||
            isWagering ||
            wager is not null;
        if (!participates)
        {
            return new { active = false };
        }

        var attempt = question.AnswerAttempts.SingleOrDefault(item =>
            item.PlayerId == player.Id);
        var isClosed = question.Status == RuntimeQuestionStatus.ShowingAnswer;
        var isText = question.PresentationType == QuestionPresentationType.AllPlayerText;
        var review = isText ? GetTextReview(game, question) : null;
        var allSubmitted = AllCurrentPlayersSubmitted(game, question);
        var timerExpired = HasAnsweringTimerExpired(game, question);
        var phase = isWagering
            ? "wagering"
            : isClosed
                ? "closed"
                : isText && review is { Accepting: false }
                    ? allSubmitted
                        ? "judging"
                        : "awaitingMissing"
                    : "answering";
        WagerLimits? wagerLimits = isWagering
            ? game.Session.GetAllPlayerQuestionWagerLimits(
                question.SourceQuestionId,
                player.Id)
            : null;

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            mode = GetMode(question),
            phase,
            hasWager = wager is not null,
            minimumWager = wagerLimits?.Minimum,
            maximumWager = wagerLimits?.Maximum,
            options = phase == "answering" &&
                question.PresentationType ==
                    QuestionPresentationType.AllPlayerMultipleChoice
                    ? ShuffleOptions(
                        CreateChoiceOptions(game, question),
                        HashCode.Combine(
                            question.SourceQuestionId,
                            player.Id.Value.GetHashCode()))
                    : Array.Empty<ChoiceOption>(),
            hasSubmitted = attempt is not null,
            isAccepting = phase == "answering" &&
                !timerExpired &&
                (!isText || review!.Accepting),
            isJudging = phase is "judging" or "awaitingMissing",
            isClosed,
            isCorrect = isClosed ? attempt?.IsCorrect : null,
            remainingMilliseconds = phase == "answering"
                ? GetRemainingMilliseconds(game, question, review)
                : 0
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

    private static bool HasAnsweringTimerExpired(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (question.Status is
            RuntimeQuestionStatus.AwaitingWager or
            RuntimeQuestionStatus.ShowingAnswer)
        {
            return false;
        }

        var timer = GetAnsweringTimer(game, question);
        _ = timer.Remaining;
        return timer.Status == GameTimerStatus.Expired;
    }

    private static int GetRemainingMilliseconds(
        GameSessionRegistration game,
        RuntimeQuestion question,
        AllPlayerTextReviewState? review = null)
    {
        if (question.Status is
                RuntimeQuestionStatus.AwaitingWager or
                RuntimeQuestionStatus.ShowingAnswer ||
            review is { Accepting: false })
        {
            return 0;
        }

        var timer = GetAnsweringTimer(game, question);
        _ = timer.Remaining;
        return timer.Status == GameTimerStatus.Expired
            ? 0
            : Math.Max(
                0,
                (int)Math.Ceiling(timer.Remaining.TotalMilliseconds));
    }

    private static GameTimer GetAnsweringTimer(
        GameSessionRegistration game,
        RuntimeQuestion question) => question.IsSpecial
            ? game.Session.AnswerTimer
            : game.Session.Timer;

    private static int GetScoreMagnitude(
        RuntimeQuestion question,
        GamePlayerId playerId,
        bool isCorrect)
    {
        if (!question.IsSpecial)
        {
            return isCorrect ? question.Points : 0;
        }

        return question.AllPlayerWagers.SingleOrDefault(wager =>
                wager.PlayerId == playerId)?.Amount
            ?? throw new GameRuleViolationException(
                "An all-player wager question cannot score an answer before that player submits a wager.");
    }

    private static string GetMode(RuntimeQuestion question) =>
        question.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice
            ? "multipleChoice"
            : "text";

    private static IReadOnlyList<GamePlayer> CurrentParticipants(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (!question.IsSpecial ||
            question.Status == RuntimeQuestionStatus.AwaitingWager)
        {
            return game.Session.Players.ToArray();
        }

        var wagerPlayerIds = question.AllPlayerWagers
            .Select(wager => wager.PlayerId)
            .ToHashSet();
        return game.Session.Players
            .Where(player => wagerPlayerIds.Contains(player.Id))
            .ToArray();
    }

    private static bool AllCurrentPlayersSubmitted(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        var participants = CurrentParticipants(game, question);
        return participants.Count > 0 &&
            participants.All(player => question.AnswerAttempts.Any(attempt =>
                attempt.PlayerId == player.Id));
    }

    private static IEnumerable<GamePlayer> CurrentPlayersWithSubmissions(
        GameSessionRegistration game,
        RuntimeQuestion question) => CurrentParticipants(game, question)
        .Where(player => question.AnswerAttempts.Any(attempt =>
            attempt.PlayerId == player.Id));

    private static bool AllSubmittedCurrentPlayersJudged(
        GameSessionRegistration game,
        RuntimeQuestion question,
        AllPlayerTextReviewState review)
    {
        var submittedPlayers = CurrentPlayersWithSubmissions(game, question).ToArray();
        return submittedPlayers.Length > 0 &&
            submittedPlayers.All(player => review.JudgedPlayers.Contains(player.Id));
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
