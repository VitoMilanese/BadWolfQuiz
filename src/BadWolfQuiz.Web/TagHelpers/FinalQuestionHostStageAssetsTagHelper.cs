using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-host-gameplay-view")]
public sealed class FinalQuestionHostStageAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Host gameplay uses soft navigation and does not replace <head>.
        // Keep the stage assets inside the replaceable gameplay view so
        // entering Final Question always carries them without an F5. The
        // responsiveness script initializes only once and then survives view
        // replacements through its document-level listeners.
        output.PreContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/final-question-host-stage.css?v=3\" />" +
            "<link rel=\"stylesheet\" href=\"/css/final-question-host-answer-space.css?v=2\" />" +
            "<script src=\"/js/final-question-host-responsiveness.js?v=1\"></script>");
    }
}
