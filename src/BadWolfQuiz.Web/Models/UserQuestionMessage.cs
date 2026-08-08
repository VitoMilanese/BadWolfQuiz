namespace BadWolfQuiz.Web.Models;

public sealed class UserQuestionMessage
{
    public int Id { get; set; }

    public int UserQuestionId { get; set; }

    public UserQuestion UserQuestion { get; set; } = null!;

    public UserQuestionAuthorType AuthorType { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ulong? DiscordMessageId { get; set; }
}
