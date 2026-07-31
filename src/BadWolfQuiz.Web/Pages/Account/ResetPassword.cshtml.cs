using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Account;

public sealed class ResetPasswordModel(
    HostAccountService accounts,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public IActionResult OnGet(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token)) return BadRequest();
        Input.Email = email;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        if (!await accounts.ResetPasswordAsync(Input.Email, Input.Token, Input.Password, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, localizer["Account_InvalidResetLink"]);
            return Page();
        }
        return RedirectToPage("ResetPasswordConfirmation");
    }

    public sealed class InputModel
    {
        [Required] public string Email { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), MinLength(8, ErrorMessage = "Account_PasswordMinLength"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), Compare(nameof(Password), ErrorMessage = "Account_PasswordMismatch"), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
