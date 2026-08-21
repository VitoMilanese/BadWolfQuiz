using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;

namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockBindingRegressionTests
{
    [Fact]
    public void Undefined_content_block_type_fails_model_validation()
    {
        var model = new ContentBlockInputModel
        {
            BlockType = (ContentBlockType)0
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(
                nameof(ContentBlockInputModel.BlockType)));
    }

    [Fact]
    public void Defined_content_block_type_passes_model_validation()
    {
        var model = new ContentBlockInputModel
        {
            BlockType = ContentBlockType.YouTube
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(results);
    }

    [Fact]
    public void Content_block_cards_keep_checkbox_hidden_inputs_inside_the_card()
    {
        var root = FindRepositoryRoot();
        var card = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_ContentBlockCard.cshtml");
        var audioCard = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_AudioBlockCard.cshtml");
        var videoCard = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_VideoBlockCard.cshtml");

        Assert.Contains(
            "ViewContext.FormContext.CanRenderAtEndOfForm = false;",
            card,
            StringComparison.Ordinal);
        Assert.Contains(
            "canRenderCheckboxHiddenInputsAtEndOfForm",
            card,
            StringComparison.Ordinal);
        Assert.Contains(
            "asp-for=\"Autoplay\"",
            audioCard,
            StringComparison.Ordinal);
        Assert.Contains(
            "asp-for=\"Autoplay\"",
            videoCard,
            StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
