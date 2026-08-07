namespace BadWolfQuiz.Web.Models;

public sealed class UserQuestion
{
    public int Id { get; set; }

    public string PublicToken { get; set; } = string.Empty;

    public string? SenderName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ulong? DiscordMessageId { get; set; }

    public ICollection<UserQuestionMessage> Messages { get; set; } = [];
}
