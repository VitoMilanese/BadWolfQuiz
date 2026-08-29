using BadWolfQuiz.Web.Pages.Admin.Games;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("a", Attributes = "asp-page")]
public sealed class FinishGameLinkTagHelper(
    IAntiforgery antiforgery,
    LinkGenerator linkGenerator) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => 1000;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not LobbyModel lobby ||
            !context.AllAttributes.TryGetAttribute("asp-page", out var pageAttribute) ||
            !string.Equals(
                pageAttribute.Value?.ToString(),
                "/Admin/Quizzes/Index",
                StringComparison.Ordinal) ||
            !output.Attributes.TryGetAttribute("class", out var classAttribute))
        {
            return;
        }

        var cssClasses = classAttribute.Value?.ToString() ?? string.Empty;
        if (!HasCssClass(cssClasses, "button-primary") &&
            !HasCssClass(cssClasses, "button-danger"))
        {
            return;
        }

        var action = linkGenerator.GetPathByPage(
            ViewContext.HttpContext,
            page: "/Admin/Games/Finish",
            values: new { id = lobby.Game.Session.Id.Value });
        var tokens = antiforgery.GetAndStoreTokens(ViewContext.HttpContext);
        if (string.IsNullOrWhiteSpace(action) ||
            string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            return;
        }

        var childContent = await output.GetChildContentAsync();
        var antiforgeryToken = new TagBuilder("input");
        antiforgeryToken.TagRenderMode = TagRenderMode.SelfClosing;
        antiforgeryToken.Attributes["type"] = "hidden";
        antiforgeryToken.Attributes["name"] = tokens.FormFieldName;
        antiforgeryToken.Attributes["value"] = tokens.RequestToken;

        var button = new TagBuilder("button");
        button.Attributes["type"] = "submit";
        button.Attributes["class"] = cssClasses;
        button.InnerHtml.AppendHtml(childContent);

        output.TagName = "form";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.Clear();
        output.Attributes.SetAttribute("method", "post");
        output.Attributes.SetAttribute("action", action);
        output.Attributes.SetAttribute("data-finish-game-form", string.Empty);
        output.Attributes.SetAttribute("style", "display: contents;");
        output.Content.Clear();
        output.Content.AppendHtml(antiforgeryToken);
        output.Content.AppendHtml(button);
    }

    private static bool HasCssClass(string cssClasses, string expected) =>
        cssClasses
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(expected, StringComparer.Ordinal);
}
