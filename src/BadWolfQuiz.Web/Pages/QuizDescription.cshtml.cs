using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class QuizDescriptionModel(QuizDbContext db) : PageModel
{
    public QuizDescriptionData Quiz { get; private set; } = null!;

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

        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Quiz = quiz;
        return Page();
    }
}
