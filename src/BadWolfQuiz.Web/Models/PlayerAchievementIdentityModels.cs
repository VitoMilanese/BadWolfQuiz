using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Models;

[Index(nameof(GamePlayerId), IsUnique = true)]
[Index(nameof(AccountId))]
public sealed class PlayerGameAccountLink
{
    public int Id { get; set; }

    public int GamePlayerId { get; set; }

    [Required, MaxLength(36)]
    public string AccountId { get; set; } = string.Empty;

    public GamePlayer Player { get; set; } = null!;
}

[Index(nameof(UserQuestionId), IsUnique = true)]
[Index(nameof(AccountId))]
public sealed class UserQuestionAccountLink
{
    public int Id { get; set; }

    public int UserQuestionId { get; set; }

    [Required, MaxLength(36)]
    public string AccountId { get; set; } = string.Empty;

    public UserQuestion UserQuestion { get; set; } = null!;
}
