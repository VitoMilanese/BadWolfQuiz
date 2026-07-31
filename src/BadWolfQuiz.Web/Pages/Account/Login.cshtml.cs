using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Account;

public sealed class LoginModel(
    HostAccountService accounts,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
        ? LocalRedirect("/Admin/Quizzes")
        : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var host = await accounts.ValidateCredentialsAsync(Input.Email, Input.Password, cancellationToken);
        if (host is null)
        {
            ModelState.AddModelError(string.Empty, localizer["Account_InvalidCredentials"]);
            return Page();
        }
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            HostAccountService.CreatePrincipal(host),
            new AuthenticationProperties { IsPersistent = Input.RememberMe });
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/Admin/Quizzes");
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Account_Required"), EmailAddress(ErrorMessage = "Account_InvalidEmail")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
