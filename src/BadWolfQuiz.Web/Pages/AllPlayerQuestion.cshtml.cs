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
            .Select(question => (int?)question.PresentationType)
            .SingleOrDefaultAsync(cancellationToken);

        return presentationType.HasValue
            ? new JsonResult(new { presentationType = presentationType.Value })
            : NotFound();
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
            return question is null
                ? new JsonResult(new { active = false })
                : new JsonResult(CreatePlayerState(game, question, player));
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

                _ = game.Session.Timer.Remaining;
                if (game.Session.Timer.Status == GameTimerStatus.Expired)
                {
                    game.Session.ResolveQuestionWithoutCorrectAnswer(sourceQuestionId);
                    game.MarkPersistenceChanged();
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

                var normalizedAnswer = answer?.Trim() ?? string.Empty;
                if (normalizedAnswer.Length is < 1 or > MaximumAnswerLength)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "The submitted answer is invalid."
                    });
                }

                var configuredAnswers = question.AnswerBlocks
                    .Select(block => block.TextContent?.Trim() ?? string.Empty)
                    .ToArray();

                if (configuredAnswers.Length == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "The question answer configuration is invalid."
                    });
                }

                if (question.PresentationType ==
                        QuestionPresentationType.AllPlayerMultipleChoice &&
                    !configuredAnswers.Contains(
                        normalizedAnswer,
                        StringComparer.Ordinal))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "The selected answer is not an available option."
                    });
                }

                var comparison = question.PresentationType ==
                    QuestionPresentationType.AllPlayerText
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                var isCorrect = string.Equals(
                    normalizedAnswer,
                    configuredAnswers[0],
                    comparison);

                game.Session.AddQuestionAnswerHistoryEntry(
                    sourceQuestionId,
                    connection.Player.Id,
                    isCorrect,
                    isCorrect ? question.Points : 0,
                    resolveQuestionIfAvailable: false);

                var isComplete = question.AnswerAttempts.Count >=
                    game.Session.Players.Count;
                if (isComplete)
                {
                    game.Session.ResolveQuestionWithoutCorrectAnswer(sourceQuestionId);
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

        _ = game.Session.Timer.Remaining;
        if (game.Session.Timer.Status != GameTimerStatus.Expired)
        {
            return;
        }

        game.Session.ResolveQuestionWithoutCorrectAnswer(question.SourceQuestionId);
        game.MarkPersistenceChanged();
    }

    private static object CreateHostState(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        var isClosed = question.Status == RuntimeQuestionStatus.ShowingAnswer;
        var attemptsByPlayer = question.AnswerAttempts
            .ToDictionary(attempt => attempt.PlayerId);
        var players = game.Session.Players
            .Select(player =>
            {
                attemptsByPlayer.TryGetValue(player.Id, out var attempt);
                return new
                {
                    id = player.Id.Value,
                    player.Name,
                    submitted = attempt is not null,
                    isCorrect = isClosed ? attempt?.IsCorrect : null
                };
            })
            .ToArray();

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            mode = GetMode(question),
            isClosed,
            answeredCount = question.AnswerAttempts.Count,
            playerCount = game.Session.Players.Count,
            remainingMilliseconds = GetRemainingMilliseconds(game, question),
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
        var isMultipleChoice = question.PresentationType ==
            QuestionPresentationType.AllPlayerMultipleChoice;

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            mode = GetMode(question),
            options = isMultipleChoice
                ? RotateOptions(
                    question.AnswerBlocks
                        .Select(block => block.TextContent!.Trim())
                        .ToArray(),
                    question.SourceQuestionId)
                : Array.Empty<string>(),
            hasSubmitted = attempt is not null,
            isClosed,
            isCorrect = isClosed ? attempt?.IsCorrect : null,
            remainingMilliseconds = GetRemainingMilliseconds(game, question)
        };
    }

    private static int GetRemainingMilliseconds(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        if (question.Status == RuntimeQuestionStatus.ShowingAnswer)
        {
            return 0;
        }

        return Math.Max(
            0,
            (int)Math.Ceiling(game.Session.Timer.Remaining.TotalMilliseconds));
    }

    private static string GetMode(RuntimeQuestion question) =>
        question.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice
            ? "multipleChoice"
            : "text";

    private static string[] RotateOptions(string[] options, int sourceQuestionId)
    {
        if (options.Length <= 1)
        {
            return options;
        }

        var offset = 1 + Math.Abs(sourceQuestionId % (options.Length - 1));
        return options
            .Skip(offset)
            .Concat(options.Take(offset))
            .ToArray();
    }
}
