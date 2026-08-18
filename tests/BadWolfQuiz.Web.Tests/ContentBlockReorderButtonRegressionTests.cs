namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockReorderButtonRegressionTests
{
    [Fact]
    public void Shared_content_block_toolbar_exposes_up_and_down_controls()
    {
        var root = FindRepositoryRoot();
        var card = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Shared",
            "_ContentBlockCard.cshtml"));

        var upIndex = card.IndexOf(
            "data-content-block-move=\"up\"",
            StringComparison.Ordinal);
        var downIndex = card.IndexOf(
            "data-content-block-move=\"down\"",
            StringComparison.Ordinal);
        var dragIndex = card.IndexOf(
            "content-block-drag-handle",
            StringComparison.Ordinal);

        Assert.True(upIndex >= 0);
        Assert.True(downIndex > upIndex);
        Assert.True(dragIndex > downIndex);
        Assert.Contains("@localizer[\"Button_Move\"] ↑", card, StringComparison.Ordinal);
        Assert.Contains("@localizer[\"Button_Move\"] ↓", card, StringComparison.Ordinal);
    }

    [Fact]
    public void Reorder_buttons_swap_only_sibling_blocks_and_reindex_the_section()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "content-block-reorder-buttons.js"));

        Assert.Contains("const host = card.parentElement", script, StringComparison.Ordinal);
        Assert.Contains("const cards = directCards(host)", script, StringComparison.Ordinal);
        Assert.Contains("host.insertBefore(card, target)", script, StringComparison.Ordinal);
        Assert.Contains("host.insertBefore(target, card)", script, StringComparison.Ordinal);
        Assert.Contains("reindexSection(card.closest(\".content-block-section\"))", script, StringComparison.Ordinal);
        Assert.Contains("upButton.disabled = index === 0", script, StringComparison.Ordinal);
        Assert.Contains(
            "downButton.disabled = index === cards.length - 1",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-content-block-list], [data-content-block-container-children]",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reorder_controls_are_loaded_and_styled_for_content_editors()
    {
        var root = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-escape-shortcuts.js"));
        var stylesheet = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "content-block-reorder-buttons.css"));

        Assert.Contains(".content-block-section", bootstrap, StringComparison.Ordinal);
        Assert.Contains(
            "content-block-reorder-buttons.css",
            bootstrap,
            StringComparison.Ordinal);
        Assert.Contains(
            "content-block-reorder-buttons.js",
            bootstrap,
            StringComparison.Ordinal);

        Assert.Contains(".content-block-move-button", stylesheet, StringComparison.Ordinal);
        Assert.Contains(":disabled", stylesheet, StringComparison.Ordinal);
    }

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
