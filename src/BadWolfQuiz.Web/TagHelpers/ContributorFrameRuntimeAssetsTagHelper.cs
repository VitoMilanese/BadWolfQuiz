using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorFrameRuntimeAssetsTagHelper(
    IFileVersionProvider fileVersionProvider) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    // ContributorSupportTagHelper still emits the legacy unversioned assets.
    // Run after it so the fingerprinted runtime CSS/JS is the final copy in
    // document order and therefore wins both the stylesheet cascade and the
    // deferred-script observer order.
    public override int Order => 2000;

    public override void Process(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        var isHostGameFlowPage = page is
            "/Admin/Games/Lobby" or
            "/Admin/Games/RoundIntro" or
            "/Admin/Games/RunningRoundIntro" or
            "/Admin/Games/FinalQuestionTransition";
        if (!isHostGameFlowPage && page is not "/Player/Lobby")
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
            var parityStylesheetPath = fileVersionProvider.AddFileVersionToPath(
                requestPathBase,
                "/css/contributor-frame-media-parity.css");
            output.PostContent.AppendHtml(
                $"<link rel=\"stylesheet\" href=\"{html.Encode(stylesheetPath)}\" />" +
                $"<link rel=\"stylesheet\" href=\"{html.Encode(parityStylesheetPath)}\" />");
            return;
        }

        if (!string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var insetScriptPath = fileVersionProvider.AddFileVersionToPath(
            requestPathBase,
            "/js/contributor-frame-insets.js");
        var parityScriptPath = fileVersionProvider.AddFileVersionToPath(
            requestPathBase,
            "/js/contributor-frame-media-parity.js");
        output.PostContent.AppendHtml(
            $"<script src=\"{html.Encode(insetScriptPath)}\" defer></script>" +
            $"<script src=\"{html.Encode(parityScriptPath)}\" defer></script>");
    }
}
