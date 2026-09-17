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
        var isHintControlHost =
            string.Equals(pageHandler, "ResolveQuestion", StringComparison.Ordinal) ||
            string.Equals(pageHandler, "JudgeQuestionAnswer", StringComparison.Ordinal);
        if (!isHintControlHost ||
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
        var actionBase = $"/Admin/Games/QuestionHints?id={game.Session.Id.Value:D}";
        AppendActionButton(
            output,
            actionBase,
            revealedCount == 0 ? strings.ShowHint : strings.ShowAnotherHint,
            "question-hint-reveal-button",
            """
            <svg class="question-hint-action-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9 18h6"></path>
                <path d="M10 22h4"></path>
                <path d="M8.5 15.5C7 14.4 6 12.6 6 10.5a6 6 0 1 1 12 0c0 2.1-1 3.9-2.5 5L15 16H9l-.5-.5Z"></path>
            </svg>
            """);
        AppendActionButton(
            output,
            $"{actionBase}&handler=All",
            strings.ShowAllHints,
            "question-hint-reveal-all-button",
            """
            <svg class="question-hint-action-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m12 2 8 4-8 4-8-4 8-4Z"></path>
                <path d="m4 10 8 4 8-4"></path>
                <path d="m4 14 8 4 8-4"></path>
            </svg>
            """);
    }

    private static void AppendActionButton(
        TagHelperOutput output,
        string actionUrl,
        string label,
        string specificCssClass,
        string iconMarkup)
    {
        var button = new TagBuilder("button");
        button.Attributes["type"] = "button";
        button.Attributes["data-question-hint-action-url"] = actionUrl;
        button.Attributes["title"] = label;
        button.Attributes["aria-label"] = label;
        button.AddCssClass("button");
        button.AddCssClass("button-secondary");
        button.AddCssClass("icon-button");
        button.AddCssClass("question-hint-action-button");
        button.AddCssClass(specificCssClass);
        button.InnerHtml.AppendHtml(iconMarkup);
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
        panel.Attributes["data-source-question-id"] =
            question.SourceQuestionId.ToString(CultureInfo.InvariantCulture);
        panel.Attributes["aria-label"] = strings.Hints;

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
.question-hint-action-button {
    width: 2.25rem;
    min-width: 2.25rem;
    max-width: 2.25rem;
    height: 2.25rem;
    min-height: 2.25rem;
    max-height: 2.25rem;
    padding: 0;
    margin: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex: 0 0 2.25rem;
    box-sizing: border-box;
    line-height: 1;
}

.question-hint-action-icon {
    width: 1.05rem;
    height: 1.05rem;
    display: block;
    flex: 0 0 auto;
}

.question-hint-reveal-button .question-hint-action-icon {
    transform: translateY(-0.04rem);
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
    padding: clamp(0.55rem, 0.9vw, 0.8rem);
    overflow-x: hidden;
    overflow-y: auto;
    border: 1px solid var(--line);
    border-radius: 0.9rem 0.9rem 0 0;
    background: var(--panel-2);
    box-shadow: 0 -0.8rem 2rem rgb(0 0 0 / 12%);
    animation: question-hints-panel-rise 180ms ease-out both;
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
    string ShowAllHints,
    string Hints)
{
    internal static QuestionHintStrings Current =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "uk" => new(
                "Показати підказку",
                "Показати ще одну підказку",
                "Показати всі підказки",
                "Підказки"),
            "it" => new(
                "Mostra suggerimento",
                "Mostra un altro suggerimento",
                "Mostra tutti i suggerimenti",
                "Suggerimenti"),
            "ru" => new(
                "Показати підказку",
                "Показати ще одну підказку",
                "Показати всі підказки",
                "Підказки"),
            _ => new(
                "Show hint",
                "Show another hint",
                "Show all hints",
                "Hints")
        };
}
