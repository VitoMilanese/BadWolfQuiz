using BadWolfQuiz.Web.Pages.Admin.Settings;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class HostSettingsFloatingSaveAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not IndexModel)
        {
            return;
        }

        if (string.Equals(output.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/host-settings-floating-save.css?v=2\" />");
            return;
        }

        if (string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<script src=\"/js/host-settings-floating-save.js?v=2\"></script>" +
                "<script src=\"/js/host-settings-save-shortcut.js?v=1\"></script>");
        }
    }
}
