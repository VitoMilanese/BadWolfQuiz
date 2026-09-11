using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Models;

public sealed class QuizTag
{
    public int Id { get; set; }
    public int QuizId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string NormalizedName { get; set; } = string.Empty;

    public Quiz Quiz { get; set; } = null!;
}
