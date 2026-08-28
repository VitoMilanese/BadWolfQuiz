using BadWolfQuiz.Game.Definitions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("select", Attributes = "asp-for")]
public sealed class AnonymousSharedWagerEditorTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                For.Name,
                "Input.PresentationType",
                StringComparison.Ordinal))
        {
            return;
        }

        var selected = For.Model is QuestionPresentationType presentationType &&
            QuestionWagerModes.IsAnonymousShared(presentationType)
                ? " selected"
                : string.Empty;

        output.PostContent.AppendHtml(
            $"<option value=\"6\"{selected}>Anonymous shared wager</option>");
    }
}
