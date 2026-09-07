namespace BadWolfQuiz.Web.Tests;

public sealed class QuizCreateRestyleRegressionTests
{
    [Fact]
    public void Create_quiz_uses_dedicated_builder_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "Create.cshtml");

        Assert.Contains("@model BadWolfQuiz.Web.Pages.Admin.Quizzes.CreateModel", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/quiz-create.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-create-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-create-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-create-card\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"quiz-create-form-panel\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("form-card narrow", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Decorative_create_markers_do_not_use_fake_step_numbers()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "Create.cshtml");

        Assert.DoesNotContain("<strong>01</strong>", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("01 / CORE DETAILS", markup, StringComparison.Ordinal);
        Assert.Contains("<strong>NEW</strong>", markup, StringComparison.Ordinal);
        Assert.Contains("<strong>+</strong>", markup, StringComparison.Ordinal);
        Assert.Contains(">CORE DETAILS</span>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_create_form_contract_and_localization_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "Quizzes", "Create.cshtml");

        Assert.Contains("<form method=\"post\" class=\"quiz-create-card\">", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Title\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-validation-for=\"Input.Title\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Input.Description\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"Index\"", markup, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\"", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"CreateQuiz_Eyebrow\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Title_CreateQuiz\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Label_QuizName\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Label_Description\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Button_Cancel\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Button_Create\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Dedicated_styles_cover_full_width_layout_fields_and_responsiveness()
    {
        var styles = ReadWebFile("wwwroot", "css", "quiz-create.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.quiz-create-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-create-hero {", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-create-card {", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-create-field input,", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-create-field textarea {", styles, StringComparison.Ordinal);
        Assert.Contains(".quiz-create-actions {", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 800px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
