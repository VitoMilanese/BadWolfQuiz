using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Account;

public sealed class RegisterModel(HostAccountService accounts) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
        ? LocalRedirect("/Admin/Quizzes")
        : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var result = await accounts.RegisterAsync(Input.Email, Input.Password, cancellationToken);
        if (result.IsEmailAlreadyUsed)
        {
            ModelState.AddModelError(nameof(Input.Email), "This email is already registered.");
            return Page();
        }
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            HostAccountService.CreatePrincipal(result.Host!),
            new AuthenticationProperties { IsPersistent = true });
        return LocalRedirect(GetSafeReturnUrl(ReturnUrl));
    }

    private string GetSafeReturnUrl(string? value) =>
        Url.IsLocalUrl(value) ? value! : "/Admin/Quizzes";

    public sealed class InputModel
    {
        [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = string.Empty;
        [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
