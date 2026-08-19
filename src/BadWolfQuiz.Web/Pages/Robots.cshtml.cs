using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class RobotsModel : PageModel
{
    public IActionResult OnGet() =>
        Content(
            SeoDiscoveryDocuments.BuildRobotsTxt(),
            "text/plain; charset=utf-8");
}
