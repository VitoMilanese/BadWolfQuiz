using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorGameSettingsTagHelper(
    IFileVersionProvider fileVersionProvider) : TagHelper
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

        var requestPathBase = ViewContext.HttpContext.Request.PathBase;
        var html = HtmlEncoder.Default;

        if (string.Equals(output.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            var stylesheetPath = fileVersionProvider.AddFileVersionToPath(
                requestPathBase,
                "/css/contributor-frames.css");
            output.PostContent.AppendHtml(
                $"<link rel=\"stylesheet\" href=\"{html.Encode(stylesheetPath)}\" />");
            return;
        }

        if (!string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var scriptPath = fileVersionProvider.AddFileVersionToPath(
            requestPathBase,
            "/js/contributor-game-settings.js");
        output.PostContent.AppendHtml(
            $"<script src=\"{html.Encode(scriptPath)}\"></script>");
    }
}
