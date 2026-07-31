using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(
    HostAccountService accounts,
    CurrentHost currentHost,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsChanged { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        if (!await accounts.ChangePasswordAsync(currentHost.RequiredId, Input.CurrentPassword, Input.NewPassword, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, localizer["Account_CurrentPasswordInvalid"]);
            return Page();
        }
        IsChanged = true;
        ModelState.Clear();
        Input = new();
        return Page();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Account_Required"), DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), MinLength(8, ErrorMessage = "Account_PasswordMinLength"), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Account_Required"), Compare(nameof(NewPassword), ErrorMessage = "Account_PasswordMismatch"), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
