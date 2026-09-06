using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class FinalQuestionHostStageAssetsTagHelper(
    IHttpContextAccessor httpContextAccessor) : TagHelper
{
    private static readonly PathString LobbyPath = new("/Admin/Games/Lobby");

    public override int Order => 2000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var requestPath = httpContextAccessor.HttpContext?.Request.Path;
        if (!requestPath.HasValue ||
            !requestPath.Value.StartsWithSegments(
                LobbyPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/final-question-host-stage.css?v=1\" />");
    }
}
