using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class FaviconHeadTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PostContent.AppendHtml(
            """
            <link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png" />
            <link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png" />
            <link rel="icon" href="/favicon.ico" />
            <link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png?v=3" />
            <link rel="manifest" href="/site-manifest.json?v=2" />
            <meta name="mobile-web-app-capable" content="yes" />
            <meta name="apple-mobile-web-app-capable" content="yes" />
            <meta name="apple-mobile-web-app-title" content="Bad Wolf Quiz" />
            """);
    }
}
