using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class PeerRatedQuestionMetadataModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost) : PageModel
{
    public IActionResult OnGet(string code, int sourceQuestionId)
    {
        var game = sessionRegistry.Find(code);
        var hostId = currentHost.Id;
        if (game is null ||
            string.IsNullOrWhiteSpace(hostId) ||
            !string.Equals(game.HostId, hostId, StringComparison.Ordinal))
        {
            return Forbid();
        }

        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId);
            if (question is null)
            {
                return NotFound();
            }

            return new JsonResult(new
            {
                peerRated = question.PresentationType ==
                    QuestionPresentationType.AllPlayerPeerRatedText,
                hasCorrectAnswer = question.PresentationType !=
                    QuestionPresentationType.AllPlayerPeerRatedText
            });
        }
    }
}
