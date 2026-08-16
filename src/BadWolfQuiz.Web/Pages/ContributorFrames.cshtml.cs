using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

public sealed class ContributorFramesModel(
    GameSessionRegistry sessionRegistry,
    IOptions<FooterOptions> footerOptions) : PageModel
{
    public IActionResult OnGet(string code)
    {
        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return NotFound();
        }

        var players = sessionRegistry.GetPlayers(game)
            .Where(player => ContributorRecognition.IsContributor(footerOptions.Value, player.Name))
            .Select(player => new
            {
                id = player.Id.Value,
                enabled = player.AvatarFrameEnabled,
                frameId = player.AvatarFrameId
            })
            .ToArray();

        Response.Headers.CacheControl = "no-store";
        return new JsonResult(new { players });
    }
}
