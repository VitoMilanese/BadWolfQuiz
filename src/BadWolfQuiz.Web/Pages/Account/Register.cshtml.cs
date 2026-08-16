using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Web.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Account;

public sealed class RegisterModel(
    HostAccountService accounts,
    GameSettingsStore settingsStore,
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
        var result = await accounts.RegisterAsync(
            Input.Email,
            Input.Password,
            Input.HostName,
            cancellationToken);
        if (result.IsEmailAlreadyUsed)
        {
            ModelState.AddModelError(nameof(Input.Email), localizer["Account_EmailAlreadyUsed"]);
            return Page();
        }
        await settingsStore.InitializeHostAsync(
            result.Host!.Id,
            result.Host.DisplayName,
            cancellationToken);
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
        [MaxLength(80)]
        public string? HostName { get; set; }

        [Required(ErrorMessage = "Account_Required"), EmailAddress(ErrorMessage = "Account_InvalidEmail"), MaxLength(254)]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), MinLength(8, ErrorMessage = "Account_PasswordMinLength"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Account_PasswordMismatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
