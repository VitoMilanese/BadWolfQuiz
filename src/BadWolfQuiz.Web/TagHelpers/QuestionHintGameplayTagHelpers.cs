using System.Globalization;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Games;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("form", Attributes = "asp-page-handler")]
public sealed class QuestionHintControlsTagHelper(QuizDbContext db) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var pageHandler =
            context.AllAttributes.TryGetAttribute("asp-page-handler", out var handlerAttribute)
                ? handlerAttribute.Value?.ToString()
                : null;
        if (!string.Equals(
                pageHandler,
                "ResolveQuestion",
                StringComparison.Ordinal) ||
            ViewContext.ViewData.Model is not LobbyModel
            {
                Game: { } game,
                CurrentQuestion:
                {
                    IsSpecial: false,
                    PresentationType: QuestionPresentationType.Standard,
                    Status: RuntimeQuestionStatus.Selected or RuntimeQuestionStatus.Active
                } question
            })
        {
            return;
        }

        var hints = await QuestionHintTagHelperData.LoadAsync(
            db,
            ViewContext,
            game.Session.Quiz.SourceQuizId,
            question.SourceQuestionId);
        if (hints.Count == 0)
        {
            return;
        }

        var revealedCount = Math.Min(question.RevealedHintCount, hints.Count);
        if (revealedCount >= hints.Count)
        {
            return;
        }

        var strings = QuestionHintStrings.Current;
        var button = new TagBuilder("button");
        button.Attributes["type"] = "submit";
        button.Attributes["formmethod"] = "post";
        button.Attributes["formaction"] =
            $"/Admin/Games/QuestionHints?id={game.Session.Id.Value:D}";
        button.AddCssClass("button");
        button.AddCssClass("button-secondary");
        button.AddCssClass("question-hint-reveal-button");
        button.InnerHtml.Append(
            revealedCount == 0
                ? strings.ShowHint
                : strings.ShowAnotherHint);
        output.PostContent.AppendHtml(button);
    }
}

[HtmlTargetElement("section", Attributes = "class")]
public sealed class QuestionHintPanelTagHelper(QuizDbContext db) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var cssClass =
            context.AllAttributes.TryGetAttribute("class", out var classAttribute)
                ? classAttribute.Value?.ToString()
                : null;
        if (!HasCssClass(cssClass, "question-presentation") ||
            ViewContext.ViewData.Model is not LobbyModel
            {
                Game: { } game,
                CurrentQuestion:
                {
                    IsSpecial: false,
                    PresentationType: QuestionPresentationType.Standard,
                    RevealedHintCount: > 0
                } question
            })
        {
            return;
        }

        var hints = await QuestionHintTagHelperData.LoadAsync(
            db,
            ViewContext,
            game.Session.Quiz.SourceQuizId,
            question.SourceQuestionId);
        var revealedCount = Math.Min(question.RevealedHintCount, hints.Count);
        if (revealedCount <= 0)
        {
            return;
        }

        var strings = QuestionHintStrings.Current;
        var panel = new TagBuilder("section");
        panel.AddCssClass("question-hints-panel");
        panel.AddCssClass("player-all-player-panel");
        panel.Attributes["data-question-hints-panel"] = string.Empty;
        panel.Attributes["aria-label"] = strings.Hints;

        var handle = new TagBuilder("span");
        handle.AddCssClass("question-hints-panel-handle");
        handle.Attributes["aria-hidden"] = "true";
        panel.InnerHtml.AppendHtml(handle);

        var heading = new TagBuilder("strong");
        heading.AddCssClass("question-hints-panel-title");
        heading.InnerHtml.Append($"{strings.Hints} {revealedCount}/{hints.Count}");
        panel.InnerHtml.AppendHtml(heading);

        var grid = new TagBuilder("div");
        grid.AddCssClass("question-hints-grid");

        foreach (var hint in hints.Take(revealedCount))
        {
            var item = new TagBuilder("article");
            item.AddCssClass("question-hint-item");

            AppendCaption(item, hint.TopCaption, "question-hint-caption-top");

            if (hint.BlockType == ContentBlockType.Text)
            {
                var text = new TagBuilder("p");
                text.AddCssClass("game-content-text");
                text.AddCssClass("question-hint-text");
                text.InnerHtml.Append(hint.TextContent?.Trim() ?? string.Empty);
                item.InnerHtml.AppendHtml(text);
            }
            else if (hint.BlockType == ContentBlockType.Image)
            {
                var image = new TagBuilder("img");
                image.AddCssClass("game-content-image");
                image.AddCssClass("question-hint-image");
                image.Attributes["src"] =
                    $"/Admin/Games/QuestionHints?id={game.Session.Id.Value:D}" +
                    $"&handler=ContentBlock&sourceQuestionId={question.SourceQuestionId}" +
                    $"&sourceContentBlockId={hint.Id}";
                image.Attributes["alt"] = hint.TopCaption ?? hint.BottomCaption ?? string.Empty;
                image.Attributes["loading"] = "eager";
                item.InnerHtml.AppendHtml(image);
            }

            AppendCaption(item, hint.BottomCaption, "question-hint-caption-bottom");
            grid.InnerHtml.AppendHtml(item);
        }

        panel.InnerHtml.AppendHtml(grid);
        output.PostContent.AppendHtml(BuildStyle());
        output.PostContent.AppendHtml(panel);
    }

    private static bool HasCssClass(string? cssClass, string value) =>
        (cssClass ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(value, StringComparer.Ordinal);

    private static void AppendCaption(
        TagBuilder item,
        string? caption,
        string cssClass)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return;
        }

        var element = new TagBuilder("p");
        element.AddCssClass("question-hint-caption");
        element.AddCssClass(cssClass);
        element.InnerHtml.Append(caption.Trim());
        item.InnerHtml.AppendHtml(element);
    }

    private static TagBuilder BuildStyle()
    {
        var style = new TagBuilder("style");
        style.Attributes["data-question-hints-styles"] = string.Empty;
        style.InnerHtml.AppendHtml("""
.question-hint-reveal-button {
    margin-inline-start: 0.55rem;
}

.question-presentation:has(> .question-hints-panel) {
    min-height: 0;
}

.question-presentation:has(> .question-hints-panel) > .game-content-blocks {
    min-height: 0;
    overflow: auto;
}

.question-presentation > .question-hints-panel {
    position: relative;
    z-index: 1;
    flex: 0 0 auto;
    width: min(100%, 84rem);
    max-height: min(34dvh, 20rem);
    margin: clamp(0.55rem, 1vh, 0.9rem) auto 0;
    padding: clamp(0.7rem, 1.1vw, 1rem);
    overflow-x: hidden;
    overflow-y: auto;
    border: 1px solid var(--line);
    border-radius: 0.9rem 0.9rem 0 0;
    background: var(--panel-2);
    box-shadow: 0 -0.8rem 2rem rgb(0 0 0 / 12%);
    animation: question-hints-panel-rise 180ms ease-out both;
}

.question-hints-panel-handle {
    display: block;
    width: 3.2rem;
    height: 0.28rem;
    margin: 0 auto 0.45rem;
    border-radius: 999px;
    background: var(--line);
}

.question-hints-panel-title {
    display: block;
    margin-bottom: 0.55rem;
    text-align: center;
}

.question-hints-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(min(16rem, 100%), 1fr));
    gap: 0.7rem;
}

.question-hint-item {
    min-width: 0;
    display: grid;
    place-items: center;
    gap: 0.35rem;
    padding: clamp(0.55rem, 0.9vw, 0.8rem);
    border: 1px solid var(--line);
    border-radius: 0.75rem;
    background: var(--panel);
    text-align: center;
}

.question-hint-text,
.question-hint-caption {
    margin: 0;
    overflow-wrap: anywhere;
}

.question-hint-text {
    font-size: clamp(1.15rem, 2vw, 2rem);
}

.question-hint-caption {
    color: var(--muted);
    font-size: 0.9rem;
}

.question-hint-image {
    display: block;
    width: auto;
    max-width: 100%;
    max-height: min(20dvh, 11rem);
    object-fit: contain;
}

@keyframes question-hints-panel-rise {
    from { transform: translateY(1.25rem); opacity: 0; }
    to { transform: translateY(0); opacity: 1; }
}

@media (max-width: 640px) {
    .question-hint-reveal-button {
        margin-inline-start: 0;
        margin-top: 0.5rem;
    }

    .question-hints-grid {
        grid-template-columns: 1fr;
    }
}
""");
        return style;
    }
}

internal static class QuestionHintTagHelperData
{
    internal static Task<IReadOnlyList<QuestionHintGameplayItem>> LoadAsync(
        QuizDbContext db,
        ViewContext viewContext,
        int quizId,
        int sourceQuestionId)
    {
        var key = (typeof(QuestionHintTagHelperData), quizId, sourceQuestionId);
        if (viewContext.HttpContext.Items.TryGetValue(key, out var existing) &&
            existing is Task<IReadOnlyList<QuestionHintGameplayItem>> cached)
        {
            return cached;
        }

        var task = QuestionHintGameplayData.LoadAsync(
            db,
            quizId,
            sourceQuestionId,
            viewContext.HttpContext.RequestAborted);
        viewContext.HttpContext.Items[key] = task;
        return task;
    }
}

internal sealed record QuestionHintStrings(
    string ShowHint,
    string ShowAnotherHint,
    string Hints)
{
    internal static QuestionHintStrings Current =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "uk" => new(
                "Показати підказку",
                "Показати ще одну підказку",
                "Підказки"),
            "it" => new(
                "Mostra suggerimento",
                "Mostra un altro suggerimento",
                "Suggerimenti"),
            "ru" => new(
                "Показати підказку",
                "Показати ще одну підказку",
                "Підказки"),
            _ => new(
                "Show hint",
                "Show another hint",
                "Hints")
        };
}
