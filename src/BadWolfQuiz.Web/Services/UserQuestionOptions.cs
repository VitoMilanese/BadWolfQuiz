namespace BadWolfQuiz.Web.Services;

public sealed class UserQuestionOptions
{
    public const string SectionName = "UserQuestions";

    public int RetentionMonths { get; set; } = 6;

    public bool IsValid => RetentionMonths > 0;
}