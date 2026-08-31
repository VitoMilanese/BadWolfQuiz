using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("a", Attributes = "asp-page")]
public sealed class HeaderSeoNavigationTagHelper(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IStringLocalizer<MinigameEditorResource> minigameEditorLocalizer) : TagHelper
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
        if (page == "/Admin/MasterGames" && IsMasterHost())
        {
            var label = HtmlEncoder.Default.Encode(
                minigameEditorLocalizer["MenuTitle"].Value);
            output.PostElement.AppendHtml(
                "<a class=\"action-menu-item\" href=\"/Admin/MinigameEditor\" " +
                "onclick=\"if(window.BadWolfBusy){window.BadWolfBusy.navigate(this.href);return false;}\">" +
                label +
                "</a>");
            return;
        }

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

    private bool IsMasterHost()
    {
        var configuredId = configuration["MasterHostId"]?.Trim();
        var currentId = httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(configuredId) &&
               string.Equals(currentId, configuredId, StringComparison.Ordinal);
    }

    private static bool HasCssClass(string? value, string expected) =>
        value?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(expected, StringComparer.Ordinal) == true;
}
