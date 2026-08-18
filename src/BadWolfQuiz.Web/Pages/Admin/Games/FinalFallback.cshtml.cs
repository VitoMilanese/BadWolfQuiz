using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class FinalFallbackModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public IActionResult OnPost(
        Guid gameId,
        Guid playerId,
        string kind)
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
                    sessionRegistry.SubmitMinimumFinalWagerForPlayer(
                        game.PublicCode,
                        runtimePlayerId);
                }
                else
                {
                    sessionRegistry.SubmitEmptyFinalAnswerForPlayer(
                        game.PublicCode,
                        runtimePlayerId);
                }
            }
            catch (GameRuleViolationException)
            {
                return Rejected();
            }
        }

        final = game.Session.FinalQuestion!;
        var allSubmitted = normalizedKind == "wager"
            ? final.Submissions.All(item => item.Wager is not null)
            : final.Submissions.All(item => item.Answer is not null);

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
