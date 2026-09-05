using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class PeerRatedAllPlayerAssetsTagHelper : TagHelper
{
    public override int Order => 2000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PostContent.AppendHtml(
            "<script src=\"/js/peer-rated-all-player-question.js?v=2\"></script>");
        output.PostContent.AppendHtml(
            "<script src=\"/js/peer-rated-host-mount-guard.js?v=1\"></script>");
        output.PostContent.AppendHtml(
            "<script src=\"/js/peer-rated-all-player-rating-confirmation.js?v=2\"></script>");
        output.PostContent.AppendHtml(
            "<script src=\"/js/peer-rated-all-player-polish.js?v=3\"></script>");
        output.PostContent.AppendHtml(
            "<script src=\"/js/peer-rated-question-context.js?v=1\"></script>");
    }
}
