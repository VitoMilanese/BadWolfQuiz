using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-question-clues")]
public sealed class MultipleChoiceAnswerRevealTagHelper(
    IHtmlHelper htmlHelper) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not LobbyModel lobby ||
            lobby.CurrentQuestion is not { } question ||
            question.Status != RuntimeQuestionStatus.ShowingAnswer ||
            !MultipleChoiceAnswerContract.IsMultipleChoice(question.PresentationType))
        {
            return;
        }

        var definition = lobby.Game.Session.Quiz.Rounds
            .SelectMany(round => round.Questions)
            .SingleOrDefault(item =>
                item.SourceQuestionId == question.SourceQuestionId);
        var blocks = definition?.RevealAnswerBlocks ??
            question.AnswerBlocks.Take(1).ToArray();

        var classValue = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        var classes = classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(item => !string.Equals(
                item,
                "all-player-answer-grid",
                StringComparison.Ordinal))
            .Append("multiple-choice-answer-reveal-grid")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        output.Attributes.SetAttribute("class", string.Join(' ', classes));

        if (htmlHelper is IViewContextAware contextualizedHtmlHelper)
        {
            contextualizedHtmlHelper.Contextualize(ViewContext);
        }

        var rendered = await htmlHelper.PartialAsync(
            "_MultipleChoiceRevealBlocks",
            new MultipleChoiceRevealBlocksViewModel(
                lobby.Game.Session.Id.Value,
                question.SourceQuestionId,
                question.PresentationType,
                blocks));
        output.Content.SetHtmlContent(rendered);
    }
}
