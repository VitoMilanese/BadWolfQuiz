using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class AnonymousSharedWagerModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public IActionResult OnGetPlayerStatus(
        string code,
        Guid playerId,
        string? accessToken)
    {
        var access = ValidatePlayer(code, playerId, accessToken);
        if (access is null)
        {
            return StatusCode(403);
        }

        try
        {
            var state = AnonymousSharedWagerWebStore.GetOrStart(access.Value.Game);
            return state is null
                ? new JsonResult(new { active = false })
                : PlayerStatus(access.Value.Game, access.Value.Player, state);
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(access.Value.ConnectionId);
        }
    }

    public IActionResult OnPostSubmit(
        string code,
        Guid playerId,
        string? accessToken,
        int percentage)
    {
        var access = ValidatePlayer(code, playerId, accessToken);
        if (access is null)
        {
            return StatusCode(403);
        }

        try
        {
            var state = AnonymousSharedWagerWebStore.Submit(
                access.Value.Game,
                access.Value.Player.Id,
                percentage);
            return PlayerStatus(access.Value.Game, access.Value.Player, state);
        }
        catch (GameRuleViolationException exception)
        {
            return new JsonResult(new { error = exception.Message }) { StatusCode = 409 };
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(access.Value.ConnectionId);
        }
    }

    public IActionResult OnGetHostStatus(Guid gameId)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(gameId),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var state = AnonymousSharedWagerWebStore.GetOrStart(game);
        if (state is null)
        {
            return new JsonResult(new { active = false });
        }

        var calculation = AnonymousSharedWagerWebStore.GetCalculation(game, state);
        var names = game.Session.AllPlayers.ToDictionary(player => player.Id, player => player.Name);
        return new JsonResult(new
        {
            active = true,
            phase = state.IsComplete ? "answering" : "collecting",
            sourceQuestionId = state.SourceQuestionId,
            answeringPlayerId = state.AnsweringPlayerId.Value,
            answeringPlayerName = names.GetValueOrDefault(state.AnsweringPlayerId, "—"),
            participants = state.ParticipantIds.Select(playerId => new
            {
                playerId = playerId.Value,
                playerName = names.GetValueOrDefault(playerId, "—"),
                submitted = state.HasSubmitted(playerId)
            }),
            combinedWager = state.IsComplete ? calculation?.CombinedWager : null
        });
    }

    public IActionResult OnPostForce(Guid gameId, Guid playerId)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(gameId),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        try
        {
            AnonymousSharedWagerWebStore.ForceFullStake(
                game,
                new GamePlayerId(playerId));
            return new JsonResult(new { saved = true });
        }
        catch (GameRuleViolationException exception)
        {
            return new JsonResult(new { error = exception.Message }) { StatusCode = 409 };
        }
    }

    public IActionResult OnPostSettle(Guid gameId, bool isCorrect)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(gameId),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        try
        {
            var attempt = AnonymousSharedWagerWebStore.Settle(game, isCorrect);
            return new JsonResult(new
            {
                saved = true,
                scoreDelta = attempt.ScoreDelta
            });
        }
        catch (GameRuleViolationException exception)
        {
            return new JsonResult(new { error = exception.Message }) { StatusCode = 409 };
        }
    }

    private JsonResult PlayerStatus(
        GameSessionRegistration game,
        GamePlayer player,
        AnonymousSharedWagerState state)
    {
        var question = game.Session.Board.Questions.Single(item =>
            item.SourceQuestionId == state.SourceQuestionId);
        var isAnswering = state.AnsweringPlayerId == player.Id;
        var isParticipant = state.ParticipantIds.Contains(player.Id);
        var submitted = isParticipant && state.HasSubmitted(player.Id);
        var selectedChoice = isParticipant
            ? state.Choices.SingleOrDefault(choice => choice.PlayerId == player.Id)
            : null;
        var calculation = state.IsComplete
            ? AnonymousSharedWagerWebStore.GetCalculation(game, state)
            : null;

        return new JsonResult(new
        {
            active = true,
            phase = state.IsComplete ? "answering" : "collecting",
            sourceQuestionId = state.SourceQuestionId,
            role = isAnswering ? "answering" : isParticipant ? "funding" : "spectator",
            submitted,
            selectedPercentage = selectedChoice?.Percentage,
            maximumShare = isParticipant
                ? AnonymousSharedWagerCalculator.CalculateMaximumShare(
                    question.Points,
                    state.ParticipantIds.Count)
                : 0,
            combinedWager = isAnswering && state.IsComplete
                ? calculation?.CombinedWager
                : null
        });
    }

    private (GameSessionRegistration Game, GamePlayer Player, string ConnectionId)?
        ValidatePlayer(string code, Guid playerId, string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var connectionId = $"anonymous-shared-wager:{Guid.NewGuid():N}";
        var connection = sessionRegistry.ConnectPlayer(
            code,
            accessToken,
            connectionId,
            isVisible: false);
        if (connection is null ||
            connection.RequiresApproval ||
            connection.Player.Id != new GamePlayerId(playerId))
        {
            sessionRegistry.DisconnectPlayer(connectionId);
            return null;
        }

        return (connection.Game, connection.Player, connectionId);
    }
}
