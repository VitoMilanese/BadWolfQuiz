using System.Globalization;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class FinalQuestionTransitionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public Guid GameId { get; private set; }
    public bool Force { get; private set; }

    public string Heading => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "uk" => "ФІНАЛЬНЕ ПИТАННЯ",
        "it" => "DOMANDA FINALE",
        _ => "FINAL QUESTION"
    };

    public IActionResult OnGet(Guid id, bool force = false)
    {
        var game = sessionRegistry.FindOwned(
            new GameSessionId(id),
            currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running ||
            game.Session.Quiz.FinalQuestion is null)
        {
            return RedirectToPage("Lobby", new { id });
        }

        GameId = id;
        Force = force;
        return Page();
    }
}
