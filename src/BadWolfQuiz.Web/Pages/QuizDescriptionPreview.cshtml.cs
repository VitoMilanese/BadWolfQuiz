using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class QuizDescriptionPreviewModel(QuizDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        int id,
        string token,
        CancellationToken cancellationToken)
    {
        var quiz = await QuizDescriptionLink.LoadAsync(
            db,
            id,
            token,
            cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=300";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return File(
            SocialPreviewImageRenderer.RenderQuizDescription(
                quiz.Title,
                quiz.Description,
                quiz.AverageRating,
                quiz.RatingCount),
            "image/png");
    }
}
