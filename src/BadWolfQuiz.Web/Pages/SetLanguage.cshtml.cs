using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public class SetLanguageModel : PageModel
{
    private static readonly HashSet<string> SupportedCultures =
    [
        "en",
        "uk",
        "ru",
        "it"
    ];

    public IActionResult OnGet(string culture, string? returnUrl = null)
    {
        if (!SupportedCultures.Contains(culture))
        {
            culture = "en";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(
                new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(
                SeoRouteCatalog.RewriteLocalizedReturnUrl(returnUrl, culture));
        }

        return RedirectToPage("/Index");
    }
}