using System.Globalization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("a", Attributes = "asp-page")]
public sealed class HeaderSeoNavigationTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!context.AllAttributes.TryGetAttribute("class", out var classAttribute) ||
            !HasCssClass(classAttribute.Value?.ToString(), "action-menu-item") ||
            !context.AllAttributes.TryGetAttribute("asp-page", out var pageAttribute))
        {
            return;
        }

        var page = pageAttribute.Value?.ToString();
        if (page is not "/PublicQuizzes" and not "/Faq" and not "/About")
        {
            return;
        }

        output.Attributes.SetAttribute(
            "href",
            SeoRouteCatalog.BuildNavigationPath(
                page,
                CultureInfo.CurrentUICulture.Name));
    }

    private static bool HasCssClass(string? value, string expected) =>
        value?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(expected, StringComparer.Ordinal) == true;
}
