namespace BadWolfQuiz.Web.Services;

public sealed class DiscordIntegrationOptions
{
    public const string SectionName = "DiscordIntegration";

    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BotToken { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public int AutomaticMuteTimeoutMinutes { get; set; } = 15;
    public int MaximumParallelOperations { get; set; } = 4;

    public bool IsValid => !Enabled ||
        (!string.IsNullOrWhiteSpace(ClientId) &&
         !string.IsNullOrWhiteSpace(ClientSecret) &&
         !string.IsNullOrWhiteSpace(BotToken) &&
         Uri.TryCreate(CallbackUrl, UriKind.Absolute, out _) &&
         AutomaticMuteTimeoutMinutes is >= 1 and <= 60 &&
         MaximumParallelOperations is >= 1 and <= 16);
}
