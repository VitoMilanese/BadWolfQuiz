namespace BadWolfQuiz.Web.Models;

public sealed class DiscordQuestionBotSettings
{
    public int Id { get; set; }

    public string? GuildId { get; set; }
    public string? GuildName { get; set; }

    public string? ChannelId { get; set; }
    public string? ChannelName { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
