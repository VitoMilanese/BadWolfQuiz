using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class BuzzerActivationModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub) : PageModel
{
    public IActionResult OnGetPolicy(Guid id, int sourceQuestionId)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active);
            if (question is null)
            {
                return new JsonResult(new { active = false });
            }

            var policy = QuestionBuzzerPolicy.Get(
                game.Session,
                sourceQuestionId);
            return new JsonResult(new
            {
                active = true,
                sourceQuestionId,
                mode = policy.Mode.ToString().ToLowerInvariant(),
                delayMilliseconds = (long)policy.DelaySeconds * 1000L,
                policy.HasInitialMedia,
                buzzerStatus = question.BuzzerStatus
                    .ToString()
                    .ToLowerInvariant()
            });
        }
    }

    public async Task<IActionResult> OnPostActivateAsync(
        Guid id,
        int sourceQuestionId,
        string trigger,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        var activated = false;
        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active);
            if (question is null)
            {
                return new JsonResult(new { success = false }) { StatusCode = 409 };
            }

            if (question.BuzzerStatus != QuestionBuzzerStatus.Inactive)
            {
                return new JsonResult(new { success = true, activated = false });
            }

            var policy = QuestionBuzzerPolicy.Get(
                game.Session,
                sourceQuestionId);
            var allowed =
                string.Equals(trigger, "media", StringComparison.Ordinal) &&
                    policy.Mode == QuestionBuzzerMode.AfterMedia &&
                    policy.HasInitialMedia ||
                string.Equals(trigger, "delay", StringComparison.Ordinal) &&
                    policy.Mode == QuestionBuzzerMode.AfterDelay &&
                    policy.DelaySeconds > 0;

            if (!allowed)
            {
                return new JsonResult(new { success = false }) { StatusCode = 409 };
            }

            game.Session.ActivateQuestionBuzzer(sourceQuestionId);
            game.MarkPersistenceChanged();
            activated = true;
        }

        if (activated)
        {
            var clients = gameHub.Clients.Group(
                GameHub.GroupName(game.PublicCode));
            await clients.SendAsync(
                "BuzzerStateChanged",
                GameHub.CreateBuzzerUpdate(game),
                cancellationToken);
            await clients.SendAsync(
                "TimerStateChanged",
                GameHub.CreateTimerUpdate(game),
                cancellationToken);
        }

        return new JsonResult(new { success = true, activated });
    }
}
