using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class ContributorGameSettingsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => -1000;

    public override void Process(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        if (page is not "/Admin/Games/Lobby" and not "/Admin/Settings/Index")
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/contributor-game-settings.js\"></script>");
    }
}
