using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class FinishModel(
    GameSessionRegistry sessionRegistry,
    QuizRatingService quizRatingService,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Admin/Quizzes/Index");

    public async Task<IActionResult> OnPostAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);
        if (game is null)
        {
            return NotFound();
        }

        await quizRatingService.FinalizeRatingPhaseAsync(
            game.Session,
            cancellationToken);

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync("QuizCompleted", cancellationToken);

        return RedirectToPage("/Admin/Quizzes/Index");
    }
}
