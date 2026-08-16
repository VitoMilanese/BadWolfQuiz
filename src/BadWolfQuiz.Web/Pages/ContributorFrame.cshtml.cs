using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class ContributorFrameModel(IWebHostEnvironment environment) : PageModel
{
    public IActionResult OnGet(int id)
    {
        var frameId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!ContributorAvatarFrameCatalog.IsValid(frameId))
        {
            return NotFound();
        }

        var path = Path.Combine(
            ContributorAvatarFrameCatalog.ResolveRootPath(environment),
            $"{frameId}.png");
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return PhysicalFile(path, "image/png");
    }
}
