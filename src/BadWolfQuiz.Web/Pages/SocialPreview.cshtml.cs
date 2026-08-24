using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class SocialPreviewModel : PageModel
{
    public IActionResult OnGet(string? variant = null)
    {
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(
            SocialPreviewImageRenderer.Render(variant),
            "image/png");
    }
}
