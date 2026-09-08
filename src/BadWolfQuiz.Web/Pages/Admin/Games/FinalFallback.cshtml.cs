using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class FinalFallbackModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public async Task<IActionResult> OnPostAsync(
        Guid gameId,
        Guid playerId,
        string kind,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(gameId),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var normalizedKind = kind.Trim().ToLowerInvariant();
        if (normalizedKind is not "wager" and not "answer")
        {
            return Rejected();
        }

        var runtimePlayerId = new GamePlayerId(playerId);
        bool allSubmitted;
        var submissionChanged = false;

        lock (game)
        {
            var final = game.Session.FinalQuestion;
            var existingSubmission = final?.Submissions.SingleOrDefault(item =>
                item.PlayerId == runtimePlayerId);
            var alreadySubmitted = normalizedKind == "wager"
                ? existingSubmission?.Wager is not null
                : existingSubmission?.Answer is not null;

            if (!alreadySubmitted)
            {
                try
                {
                    if (normalizedKind == "wager")
                    {
                        game.Session.SubmitFinalWager(
                            runtimePlayerId,
                            FinalQuestion.MinimumWager);
                    }
                    else
                    {
                        game.Session.SubmitFinalAnswer(runtimePlayerId, "-");
                    }

                    game.MarkPersistenceChanged();
                    submissionChanged = true;
                }
                catch (GameRuleViolationException)
                {
                    return Rejected();
                }
            }

            final = game.Session.FinalQuestion!;
            allSubmitted = normalizedKind == "wager"
                ? final.Submissions.All(item => item.Wager is not null)
                : final.Submissions.All(item => item.Answer is not null);
        }

        if (submissionChanged)
        {
            await gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync(
                    "FinalQuestionPlayerFallbackChanged",
                    new { playerId = runtimePlayerId.Value },
                    cancellationToken);
        }

        return new JsonResult(new
        {
            success = true,
            playerId,
            kind = normalizedKind,
            allSubmitted,
            submittedLabel = localizer["FinalQuestion_Submitted"].Value
        });
    }

    private IActionResult Rejected() =>
        BadRequest(new
        {
            success = false,
            error = localizer["FinalQuestion_ActionRejected"].Value
        });
}
