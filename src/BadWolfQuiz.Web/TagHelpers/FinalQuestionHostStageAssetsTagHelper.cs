using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-host-gameplay-view")]
public sealed class FinalQuestionHostStageAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Host gameplay uses soft navigation and does not replace <head>.
        // Keep the stage stylesheet inside the replaceable gameplay view so
        // entering Final Question always carries the asset without an F5.
        output.PreContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/final-question-host-stage.css?v=2\" />");
    }
}
