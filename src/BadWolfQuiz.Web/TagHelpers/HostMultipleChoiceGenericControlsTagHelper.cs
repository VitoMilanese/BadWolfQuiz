using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("form", Attributes = "asp-page-handler")]
public sealed class HostMultipleChoiceGenericControlsTagHelper : TagHelper
{
    [HtmlAttributeName("asp-page-handler")]
    public string? PageHandler { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not LobbyModel
            {
                CurrentQuestion:
                {
                    IsHostMultipleChoice: true,
                    Status: RuntimeQuestionStatus.Selected or RuntimeQuestionStatus.Active
                }
            })
        {
            return;
        }

        if (string.Equals(
                PageHandler,
                "JudgeQuestionAnswer",
                StringComparison.Ordinal) ||
            string.Equals(
                PageHandler,
                "ResolveQuestion",
                StringComparison.Ordinal))
        {
            output.SuppressOutput();
        }
    }
}
