using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RuntimeGameSession = BadWolfQuiz.Game.Runtime.GameSession;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class LobbyModel(GameSessionRegistry sessionRegistry) : PageModel
{
    public RuntimeGameSession Session { get; private set; } = null!;

    public IActionResult OnGet(Guid id)
    {
        var session = sessionRegistry.Find(new GameSessionId(id));

        if (session is null)
        {
            return NotFound();
        }

        Session = session;
        return Page();
    }
}
