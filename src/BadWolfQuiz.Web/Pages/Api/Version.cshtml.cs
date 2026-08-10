using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Api;

public sealed class VersionModel : PageModel
{
    public IActionResult OnGet()
    {
        var productVersion = ProductVersionInfo.Current;
        return new JsonResult(new
        {
            version = productVersion.Version,
            commit = productVersion.Commit
        });
    }
}
