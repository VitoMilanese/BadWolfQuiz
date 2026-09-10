using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Models;

[Index(nameof(AccountId), nameof(AchievementCode), IsUnique = true)]
[Index(nameof(HostId), nameof(PlayerKey), nameof(AchievementCode), IsUnique = true)]
[Index(nameof(SourceGameSessionId))]
public sealed class PlayerAchievement
{
    public int Id { get; set; }

    [MaxLength(36)]
    public string? AccountId { get; set; }

    [MaxLength(36)]
    public string? HostId { get; set; }

    [MaxLength(60)]
    public string? PlayerKey { get; set; }

    [Required, MaxLength(64)]
    public string AchievementCode { get; set; } = string.Empty;

    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;
    public int? SourceGameSessionId { get; set; }
}
