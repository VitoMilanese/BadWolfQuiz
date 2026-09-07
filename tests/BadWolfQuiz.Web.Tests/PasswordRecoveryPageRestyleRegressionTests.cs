namespace BadWolfQuiz.Web.Tests;

public sealed class PasswordRecoveryPageRestyleRegressionTests
{
    [Theory]
    [InlineData("ResetPassword.cshtml", "BadWolfQuiz.Web.Pages.Account.ResetPasswordModel")]
    [InlineData("ForgotPasswordConfirmation.cshtml", "BadWolfQuiz.Web.Pages.Account.ForgotPasswordConfirmationModel")]
    [InlineData("ResetPasswordConfirmation.cshtml", "BadWolfQuiz.Web.Pages.Account.ResetPasswordConfirmationModel")]
    public void Remaining_recovery_pages_use_shared_portal_presentation(string fileName, string pageModel)
    {
        var markup = ReadAccountPage(fileName);

        Assert.Contains($"@model {pageModel}", markup, StringComparison.Ordinal);
        Assert.Contains("IStringLocalizer<PublicAccountPagesResource>", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/public-account-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/password-recovery-pages.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-page auth-page password-recovery-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"portal-content\"", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ForgotKicker\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"RecoveryChannel\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ForgotMarkPrimary\"]", markup, StringComparison.Ordinal);
        Assert.Contains("PublicAccountLocalizer[\"ForgotMarkAccent\"]", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-card content-panel", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_password_preserves_form_token_and_password_contracts()
    {
        var markup = ReadAccountPage("ResetPassword.cshtml");

        Assert.Contains("method=\"post\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Email\" type=\"hidden\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Token\" type=\"hidden\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Password\" autocomplete=\"new-password\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.ConfirmPassword\" autocomplete=\"new-password\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"Input.Password\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"Input.ConfirmPassword\"", markup, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\">@Localizer[\"Account_ResetPassword\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Forgot_password_confirmation_preserves_check_email_copy_and_login_navigation()
    {
        var markup = ReadAccountPage("ForgotPasswordConfirmation.cshtml");

        Assert.Contains("Localizer[\"Account_CheckEmail\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Account_CheckEmailDescription\"]", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"recovery-state-glyph\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Login\">@Localizer[\"Account_SignIn\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_password_confirmation_preserves_completion_copy_and_primary_login_action()
    {
        var markup = ReadAccountPage("ResetPasswordConfirmation.cshtml");

        Assert.Contains("Localizer[\"Account_PasswordResetComplete\"]", markup, StringComparison.Ordinal);
        Assert.Contains("recovery-state-glyph-success", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"button button-primary\" asp-page=\"Login\"", markup, StringComparison.Ordinal);
        Assert.Contains("@Localizer[\"Account_SignIn\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Recovery_specific_styles_cover_status_cards_actions_and_shared_hero_word_sizing()
    {
        var styles = File.ReadAllText(FindWebFile("wwwroot", "css", "password-recovery-pages.css"))
            .ReplaceLineEndings("\n");
        var sharedStyles = File.ReadAllText(FindWebFile("wwwroot", "css", "public-account-pages.css"))
            .ReplaceLineEndings("\n");

        Assert.Contains(".password-recovery-page .portal-hero h1", styles, StringComparison.Ordinal);
        Assert.DoesNotContain(".password-recovery-page .portal-hero-mark strong", styles, StringComparison.Ordinal);
        Assert.Contains("container-type: inline-size;", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(2.25rem, 18cqw, 4.8rem);", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(1.9rem, 14cqw, 3.8rem);", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: normal;", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("word-break: normal;", sharedStyles, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", sharedStyles, StringComparison.Ordinal);
        Assert.Contains(".recovery-state-card", styles, StringComparison.Ordinal);
        Assert.Contains(".recovery-state-glyph", styles, StringComparison.Ordinal);
        Assert.Contains(".recovery-state-glyph-success", styles, StringComparison.Ordinal);
        Assert.Contains(".recovery-state-copy", styles, StringComparison.Ordinal);
        Assert.Contains(".recovery-state-actions", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", styles, StringComparison.Ordinal);
    }

    private static string ReadAccountPage(string fileName)
        => File.ReadAllText(FindWebFile("Pages", "Account", fileName));

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
