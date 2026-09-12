using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class DebugAchievementUnlockAssetsTagHelper(
    IAntiforgery antiforgery,
    IConfiguration configuration) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!configuration.GetValue<bool>("DebugMode"))
        {
            return;
        }

        var page = ViewContext.RouteData.Values["page"]?.ToString();
        if (page is not "/Admin/Games/Lobby" and
            not "/Admin/Games/RoundIntro" and
            not "/Admin/Games/RunningRoundIntro" and
            not "/Admin/Games/FinalQuestionTransition")
        {
            return;
        }

        var requestToken = antiforgery
            .GetAndStoreTokens(ViewContext.HttpContext)
            .RequestToken;
        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return;
        }

        output.PostContent.AppendHtml($$"""
            <input type="hidden"
                   value="{{HtmlEncoder.Default.Encode(requestToken)}}"
                   data-debug-achievement-antiforgery />
            <script src="/js/debug-achievement-unlock.js?v=1" defer></script>
            """);
    }
}
