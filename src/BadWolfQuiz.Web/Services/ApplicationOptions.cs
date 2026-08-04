namespace BadWolfQuiz.Web.Services;

public sealed class QuizEditorOptions
{
    public const string SectionName = "QuizEditor";
    public int MinimumCategoryCount { get; set; } = 3;
    public int MaximumCategoryCount { get; set; } = 7;
    public int MinimumQuestionCount { get; set; } = 5;
    public int MaximumQuestionCount { get; set; } = 10;

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
