using System.Net.Http.Json;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionSender(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordQuestionSender> logger)
{
    public async Task<bool> SendAsync(
        string? senderName,
        string question,
        CancellationToken cancellationToken = default)
    {
        var webhookUrl = configuration["Discord:QuestionWebhookUrl"];
        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var webhookUri) ||
            !string.Equals(webhookUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(webhookUri.Host, "discord.com", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("The Discord question webhook is not configured.");
            return false;
        }

        var author = string.IsNullOrWhiteSpace(senderName)
            ? "Anonymous"
            : senderName.Trim();
        var content = $"**Question from {author}:**\n{question.Trim()}";

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                webhookUri,
                new
                {
                    content,
                    allowed_mentions = new { parse = Array.Empty<string>() }
                },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (responseBody.Length > 500)
            {
                responseBody = responseBody[..500];
            }

            logger.LogWarning(
                "Discord rejected a question with status code {StatusCode}: {ResponseBody}",
                response.StatusCode,
                responseBody);
            return false;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Failed to send a question to Discord.");
            return false;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The Discord webhook request timed out.");
            return false;
        }
    }
}
