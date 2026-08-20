using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("article", Attributes = "data-question-clue")]
public sealed class HostMultipleChoiceAnswerBlockTagHelper : TagHelper
{
    private static readonly object RenderIndexKey = new();

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not LobbyModel lobby ||
            lobby.CurrentQuestion is not
            {
                IsHostMultipleChoice: true,
                Status: RuntimeQuestionStatus.ShowingAnswer
            } question)
        {
            return;
        }

        var items = ViewContext.HttpContext.Items;
        var renderIndex =
            items.TryGetValue(RenderIndexKey, out var storedIndex) &&
            storedIndex is int currentIndex
                ? currentIndex
                : 0;
        items[RenderIndexKey] = renderIndex + 1;

        if (renderIndex >= question.AnswerBlocks.Count ||
            question.AnswerBlocks[renderIndex].SourceContentBlockId !=
                question.HostMultipleChoiceCorrectOptionId)
        {
            output.SuppressOutput();
        }
    }
}
