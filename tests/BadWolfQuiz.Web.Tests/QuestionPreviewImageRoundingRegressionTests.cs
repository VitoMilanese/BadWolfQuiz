namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionPreviewImageRoundingRegressionTests
{
    [Fact]
    public void Shared_preview_loads_contained_image_rounding_controller()
    {
        var partial = ReadWebFile(
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_QuestionPreviewModal.cshtml")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "question-preview-image-rounding.js",
            partial,
            StringComparison.Ordinal);
        Assert.Contains(
            "asp-append-version=\"true\"",
            partial,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_controller_rounds_the_actual_object_fit_contain_bitmap()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "question-preview-image-rounding.js")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "const previewImageSelector = \"img.question-preview-image\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const previewImageRadius = \"clamp(12px, 1.3vw, 20px)\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const naturalRatio = image.naturalWidth / image.naturalHeight;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const boxRatio = bounds.width / bounds.height;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const paintedWidth = bounds.height * naturalRatio;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const paintedHeight = bounds.width / naturalRatio;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"clip-path\",",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "round ${previewImageRadius}",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_rounding_recalculates_after_open_load_and_resize()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "question-preview-image-rounding.js")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "modal.addEventListener(\"load\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const observer = new MutationObserver(scheduleApply);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "attributeFilter: [\"hidden\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "childList: true",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "subtree: true",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"resize\", scheduleApply);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.requestAnimationFrame(() =>",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Container_preview_images_keep_object_fit_contain_layout()
    {
        var containers = ReadWebFile(
            "wwwroot",
            "js",
            "content-block-containers.js")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            ".content-block-container-layout .question-preview-image",
            containers,
            StringComparison.Ordinal);
        Assert.Contains(
            "object-fit: contain;",
            containers,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
