using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class HostMultipleChoiceModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub) : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnGetState(Guid id)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        object state;
        lock (game)
        {
            state = CreateState(game);
        }

        return new JsonResult(state);
    }

    public async Task<IActionResult> OnPostSelectAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        int sourceContentBlockId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        HostMultipleChoiceSelectionResult result;
        GamePlayer player;
        try
        {
            lock (game)
            {
                result = game.Session.SelectHostMultipleChoiceOption(
                    sourceQuestionId,
                    new GamePlayerId(playerId),
                    sourceContentBlockId);
                player = game.Session.Players.Single(item =>
                    item.Id == result.Attempt.PlayerId);
                game.BuzzerRace = null;
                game.MarkPersistenceChanged();
            }
        }
        catch (GameRuleViolationException exception)
        {
            return BadRequest(new
            {
                success = false,
                error = exception.Message
            });
        }

        await BroadcastAsync(game, cancellationToken);

        return new JsonResult(new
        {
            success = true,
            result.IsCorrect,
            result.QuestionClosed,
            result.RewardPercentage,
            result.RewardValue,
            scoreDelta = result.Attempt.ScoreDelta,
            playerId = player.Id.Value,
            playerName = player.Name,
            state = CreateState(game)
        });
    }

    private object CreateState(GameSessionRegistration game)
    {
        var question = game.Session.Board.Questions.FirstOrDefault(item =>
            item.IsHostMultipleChoice &&
            item.Status is RuntimeQuestionStatus.Selected or
                RuntimeQuestionStatus.Active or
                RuntimeQuestionStatus.ShowingAnswer);

        if (question is null)
        {
            return new
            {
                active = false
            };
        }

        var answeringPlayer = question.AnsweringPlayerId is { } answeringPlayerId
            ? game.Session.Players.SingleOrDefault(player =>
                player.Id == answeringPlayerId)
            : null;
        var remainingOptionsById = question.RemainingHostMultipleChoiceOptions
            .ToDictionary(option => option.SourceContentBlockId);
        var optionDisplayOrder = CreateOptionDisplayOrder(
            game.Session.Id.Value,
            question.SourceQuestionId,
            question.AnswerBlocks
                .OrderBy(option => option.SortOrder)
                .Select(option => option.SourceContentBlockId)
                .ToArray());
        var options = optionDisplayOrder
            .Where(remainingOptionsById.ContainsKey)
            .Select(optionId => remainingOptionsById[optionId])
            .ToArray();

        return new
        {
            active = true,
            sourceQuestionId = question.SourceQuestionId,
            status = question.Status.ToString().ToLowerInvariant(),
            buzzerStatus = question.BuzzerStatus.ToString().ToLowerInvariant(),
            answeringPlayerId = answeringPlayer?.Id.Value,
            answeringPlayerName = answeringPlayer?.Name,
            rewardPercentage = question.HostMultipleChoiceRewardPercentage,
            rewardValue = question.HostMultipleChoiceRewardValue,
            originalOptionCount = question.HostMultipleChoiceOriginalOptionCount,
            remainingOptionCount = question.RemainingHostMultipleChoiceOptionIds.Count,
            options = options.Select(option => new
            {
                id = option.SourceContentBlockId,
                text = option.TextContent
            }).ToArray()
        };
    }

    private static IReadOnlyList<int> CreateOptionDisplayOrder(
        Guid gameId,
        int sourceQuestionId,
        IReadOnlyList<int> optionIds)
    {
        var originalOrder = optionIds.ToArray();
        var shuffledOrder = optionIds.ToArray();
        var random = new Random(CreateOptionDisplaySeed(gameId, sourceQuestionId));

        for (var index = shuffledOrder.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (shuffledOrder[index], shuffledOrder[swapIndex]) =
                (shuffledOrder[swapIndex], shuffledOrder[index]);
        }

        if (shuffledOrder.Length > 1 && shuffledOrder.SequenceEqual(originalOrder))
        {
            (shuffledOrder[0], shuffledOrder[1]) =
                (shuffledOrder[1], shuffledOrder[0]);
        }

        return shuffledOrder;
    }

    private static int CreateOptionDisplaySeed(Guid gameId, int sourceQuestionId)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in gameId.ToByteArray())
            {
                hash = (hash * 31) + value;
            }

            return (hash * 31) + sourceQuestionId;
        }
    }

    private async Task BroadcastAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        var group = gameHub.Clients.Group(GameHub.GroupName(game.PublicCode));
        await Task.WhenAll(
            group.SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken),
            group.SendAsync(
                "BuzzerStateChanged",
                GameHub.CreateBuzzerUpdate(game),
                cancellationToken),
            group.SendAsync(
                "TimerStateChanged",
                GameHub.CreateTimerUpdate(game),
                cancellationToken));
    }
}
