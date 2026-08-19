using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class SitemapModel : PageModel
{
    public IActionResult OnGet() =>
        Content(
            SeoDiscoveryDocuments.BuildSitemapXml(),
            "application/xml; charset=utf-8");
}
