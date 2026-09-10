using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Models;

public sealed class QuizQuestionTag
{
    public int Id { get; set; }
    public int QuizQuestionId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string NormalizedName { get; set; } = string.Empty;

    public QuizQuestion Question { get; set; } = null!;
}
