namespace BadWolfQuiz.Web.Services;

public sealed class DiscordQuestionBotOptions
{
    public const string SectionName = "DiscordQuestionBot";

    public bool Enabled { get; set; }
    public string BotToken { get; set; } = string.Empty;

    public bool IsValid =>
        !Enabled ||
        !string.IsNullOrWhiteSpace(BotToken);
}
