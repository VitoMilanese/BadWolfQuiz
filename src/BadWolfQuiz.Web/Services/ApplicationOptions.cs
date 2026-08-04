namespace BadWolfQuiz.Web.Services;

public sealed class QuizEditorOptions
{
    public const string SectionName = "QuizEditor";
    public const int StandardCategoryCount = 6;
    public const int StandardQuestionCount = 5;
    public int MinimumCategoryCount { get; set; } = 3;
    public int MaximumCategoryCount { get; set; } = 7;
    public int MinimumQuestionCount { get; set; } = 5;
    public int MaximumQuestionCount { get; set; } = 10;

    public int InitialCategoryCount => Math.Clamp(
        StandardCategoryCount,
        MinimumCategoryCount,
        MaximumCategoryCount);

    public int InitialQuestionCount => Math.Clamp(
        StandardQuestionCount,
        MinimumQuestionCount,
        MaximumQuestionCount);

    public bool IsValid =>
        MinimumCategoryCount > 0 &&
        MaximumCategoryCount >= MinimumCategoryCount &&
        MinimumQuestionCount > 0 &&
        MaximumQuestionCount >= MinimumQuestionCount;
}

public sealed class SiteDefaultsOptions
{
    public const string SectionName = "SiteDefaults";
    public string Culture { get; set; } = "en";
    public string ThemeId { get; set; } = SiteThemeCatalog.DefaultId;
}
