using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class HostCardResizeRuntimeAssetsTagHelper(
    IFileVersionProvider fileVersionProvider) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => 2100;

    public override void Process(
        TagHelperContext context,
        TagHelperOutput output)
    {
        if (!string.Equals(
                ViewContext.RouteData.Values["page"]?.ToString(),
                "/Admin/Games/Lobby",
                StringComparison.Ordinal) ||
            !string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var scriptPath = fileVersionProvider.AddFileVersionToPath(
            ViewContext.HttpContext.Request.PathBase,
            "/js/host-card-resize-persistence.js");
        output.PostContent.AppendHtml(
            $"<script src=\"{HtmlEncoder.Default.Encode(scriptPath)}\" defer></script>");
    }
}
