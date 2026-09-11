namespace BadWolfQuiz.Web.Tests;

public sealed class QuizMetadataDialogLocalizationRegressionTests
{
    [Fact]
    public void Rename_dialog_uses_existing_localization_keys_instead_of_rendering_key_names()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages", "Admin", "Quizzes", "Index.cshtml"));

        var dialogStart = markup.IndexOf(
            "<dialog id=\"renameQuizDialog\"",
            StringComparison.Ordinal);
        var dialogEnd = markup.IndexOf(
            "</dialog>",
            dialogStart,
            StringComparison.Ordinal);

        Assert.True(dialogStart >= 0);
        Assert.True(dialogEnd > dialogStart);

        var dialog = markup[dialogStart..dialogEnd];
        Assert.Contains("@Localizer[\"Button_Edit\"]", dialog);
        Assert.Contains("@Localizer[\"Dialog_RenameQuiz\"]", dialog);
        Assert.DoesNotContain("Dialog_EditQuizEyebrow", dialog);
        Assert.DoesNotContain("Dialog_RenameQuizTitle", dialog);
    }

    [Fact]
    public void Rename_dialog_localization_keys_exist_in_every_shared_resource()
    {
        var resources = new[]
        {
            "SharedResource.resx",
            "SharedResource.uk.resx",
            "SharedResource.it.resx",
            "SharedResource.ru.resx"
        };

        foreach (var resource in resources)
        {
            var content = File.ReadAllText(FindWebFile(
                "Resources", "Localization", resource));

            Assert.Contains("name=\"Button_Edit\"", content);
            Assert.Contains("name=\"Dialog_RenameQuiz\"", content);
        }
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                Path.Combine(parts));

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
