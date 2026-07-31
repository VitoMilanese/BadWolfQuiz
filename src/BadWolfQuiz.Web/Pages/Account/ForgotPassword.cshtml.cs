using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Account;

public sealed class ForgotPasswordModel(
    HostAccountService accounts,
    PasswordResetEmailSender emailSender,
    ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var token = await accounts.CreatePasswordResetTokenAsync(Input.Email, cancellationToken);
        if (token is not null)
        {
            var resetUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { email = Input.Email.Trim(), token },
                protocol: Request.Scheme)!;
            try
            {
                await emailSender.SendAsync(Input.Email.Trim(), resetUrl, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not send a password reset email.");
            }
        }
        return RedirectToPage("ForgotPasswordConfirmation");
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Account_Required"), EmailAddress(ErrorMessage = "Account_InvalidEmail")]
        public string Email { get; set; } = string.Empty;
    }
}
