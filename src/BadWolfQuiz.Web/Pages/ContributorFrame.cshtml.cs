using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class ContributorFrameModel(IWebHostEnvironment environment) : PageModel
{
    public IActionResult OnGet(string id)
    {
        if (!ContributorAvatarFrameCatalog.TryResolvePath(
                environment,
                id,
                out var path))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return PhysicalFile(path, "image/png");
    }
}
