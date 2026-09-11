using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Models;

[Index(nameof(HostId), nameof(IsDeleted))]
public sealed class HostCustomAchievement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(36)]
    public string HostId { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Target { get; set; } = 1;

    [Required]
    public byte[] ArtworkPng { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<HostCustomAchievementTag> Tags { get; set; } = new List<HostCustomAchievementTag>();
}

[Index(nameof(HostCustomAchievementId), nameof(NormalizedName), IsUnique = true)]
public sealed class HostCustomAchievementTag
{
    public int Id { get; set; }
    public Guid HostCustomAchievementId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string NormalizedName { get; set; } = string.Empty;

    public HostCustomAchievement Achievement { get; set; } = null!;
}
