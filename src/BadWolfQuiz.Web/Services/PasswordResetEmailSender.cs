using System.Net;
using Resend;
using BadWolfQuiz.Web.Localization;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Services;

public sealed class PasswordResetEmailSender(
    IResend resend,
    IConfiguration configuration,
    IStringLocalizer<SharedResource> localizer)
{
    public async Task SendAsync(
        string recipient,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var apiToken = configuration["Resend:ApiToken"];
        var from = configuration["Resend:From"];
        if (string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException("Resend email settings are not configured.");
        }

        var encodedUrl = WebUtility.HtmlEncode(resetUrl);
        var message = new EmailMessage
        {
            From = from,
            Subject = localizer["Account_EmailSubject"],
            HtmlBody = $"""
                <p>{WebUtility.HtmlEncode(localizer["Account_EmailIntro"])}</p>
                <p><a href="{encodedUrl}">{WebUtility.HtmlEncode(localizer["Account_ResetPassword"])}</a></p>
                <p>{WebUtility.HtmlEncode(localizer["Account_EmailExpiry"])}</p>
                """
        };
        message.To.Add(recipient);
        cancellationToken.ThrowIfCancellationRequested();
        await resend.EmailSendAsync(message);
    }
}
