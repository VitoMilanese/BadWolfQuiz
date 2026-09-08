namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyRestyleRegressionTests
{
    [Fact]
    public void Answer_key_uses_a_dedicated_full_stage_presentation_without_rewriting_behavior()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("data-answer-key-visibility-toggle", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-hidden-placeholder", page, StringComparison.Ordinal);
        Assert.Contains("data-answer-key-content", page, StringComparison.Ordinal);
        Assert.Contains(
            "const storageKey = `badwolf-answer-key-visible:${page.dataset.gameCode ?? \"\"}`;",
            page,
            StringComparison.Ordinal);
        Assert.Contains("connection.on(\"GameStatusChanged\"", page, StringComparison.Ordinal);
        Assert.Contains("connection.on(\"BuzzerStateChanged\"", page, StringComparison.Ordinal);

        Assert.Contains(".answer-key-page {", css, StringComparison.Ordinal);
        Assert.Contains("position: relative;", css, StringComparison.Ordinal);
        Assert.Contains("isolation: isolate;", css, StringComparison.Ordinal);
        Assert.Contains("repeating-linear-gradient(", css, StringComparison.Ordinal);
        Assert.Contains("radial-gradient(", css, StringComparison.Ordinal);
        Assert.Contains("overscroll-behavior: contain;", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-page::before", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-page::after", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Hidden_and_waiting_states_use_the_redesigned_finale_language_and_touch_safe_toggle()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains(".answer-key-hidden-placeholder::before", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-waiting::before", css, StringComparison.Ordinal);
        Assert.Contains("content: \"BAD WOLF QUIZ\";", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-hidden-icon {", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 50%;", css, StringComparison.Ordinal);
        Assert.Contains("font-size: clamp(2.6rem, 7vw, 6.4rem);", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-waiting p {", css, StringComparison.Ordinal);

        Assert.Contains(".answer-key-visibility-toggle {", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("touch-action: manipulation;", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-visibility-toggle:focus-visible", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Revealed_answer_keeps_all_media_contracts_and_scales_to_the_available_stage()
    {
        var page = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "AnswerKey.cshtml"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("ContentBlockKind.Text", page, StringComparison.Ordinal);
        Assert.Contains("ContentBlockKind.Image", page, StringComparison.Ordinal);
        Assert.Contains("ContentBlockKind.Audio", page, StringComparison.Ordinal);
        Assert.Contains("ContentBlockKind.Video", page, StringComparison.Ordinal);
        Assert.Contains("ContentBlockKind.YouTube", page, StringComparison.Ordinal);
        Assert.Contains("sourceContentBlockId = block.SourceContentBlockId", page, StringComparison.Ordinal);
        Assert.Contains("final = Model.IsFinalQuestion", page, StringComparison.Ordinal);
        Assert.Contains("block.TopCaption", page, StringComparison.Ordinal);
        Assert.Contains("block.BottomCaption", page, StringComparison.Ordinal);

        Assert.Contains(".answer-key-content {", css, StringComparison.Ordinal);
        Assert.Contains("height: 100%;", css, StringComparison.Ordinal);
        Assert.Contains("background: transparent;", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-content .game-content-blocks {", css, StringComparison.Ordinal);
        Assert.Contains("align-content: center;", css, StringComparison.Ordinal);
        Assert.Contains("justify-items: center;", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-content .game-content-image,", css, StringComparison.Ordinal);
        Assert.Contains("max-height: min(68dvh", css, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain;", css, StringComparison.Ordinal);
        Assert.Contains(".answer-key-content .game-content-audio-shell {", css, StringComparison.Ordinal);
        Assert.Contains("width: min(780px, 100%);", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_restyle_has_mobile_short_viewport_and_reduced_motion_fallbacks()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "answer-key.css"));

        Assert.Contains("@media (max-width: 720px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 720px) and (min-width: 721px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("transition-duration: 0s !important;", css, StringComparison.Ordinal);
        Assert.Contains("animation-duration: 0s !important;", css, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
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
