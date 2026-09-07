using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-game-intro-page")]
public sealed class GameIntroStageAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Intro pages replace only the intro frame during soft navigation, so keep
        // the presentation assets attached to the frame itself instead of relying
        // on a one-time <head> update.
        output.PreContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/game-intro-stage.css?v=9\" />" +
            "<link rel=\"stylesheet\" href=\"/css/game-intro-stage-refinements.css?v=4\" />");
    }
}
