using System.Text;
using System.Text.Encodings.Web;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
public sealed class BackgroundStarsAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PostContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/background-stars.css?v=1\" />" +
            "<script defer src=\"/js/background-stars.js?v=1\"></script>");
    }
}

[HtmlTargetElement("body")]
public sealed class BackgroundStarsBodyTagHelper(
    GameSettingsStore settingsStore,
    CurrentHost currentHost) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var settings = ViewContext.ViewData["GameThemeSettings"] as GameSessionSettings;
        if (settings is null &&
            ViewContext.HttpContext.User.Identity?.IsAuthenticated == true &&
            currentHost.Id is { Length: > 0 } hostId)
        {
            settings = await settingsStore.LoadAsync(
                hostId,
                ViewContext.HttpContext.RequestAborted);
        }

        var enabled = settings?.AnimatedStarsEnabled ?? true;
        output.Attributes.SetAttribute(
            "data-animated-stars",
            enabled ? "true" : "false");
        output.PreContent.PrependHtml(
            "<div class=\"site-starfield\" data-site-starfield aria-hidden=\"true\"></div>");
    }
}

[HtmlTargetElement("details")]
public sealed class BackgroundStarsSettingsTagHelper(
    IStringLocalizer<BackgroundStarsResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                ViewContext.HttpContext.Request.Path.Value,
                "/Admin/Settings",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var classValue = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        if (!classValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("host-settings-theme-picker", StringComparer.Ordinal))
        {
            return;
        }

        var enabled = ViewContext.ViewData.Model is
            BadWolfQuiz.Web.Pages.Admin.Settings.IndexModel settingsModel
            ? settingsModel.Input.AnimatedStarsEnabled
            : true;

        var html = new StringBuilder();
        html.Append("<div class=\"host-settings-grid background-stars-settings\">");
        html.Append("<label class=\"settings-checkbox host-settings-checkbox host-settings-field-wide\" for=\"Input_AnimatedStarsEnabled\">");
        html.Append("<input id=\"Input_AnimatedStarsEnabled\" type=\"checkbox\" name=\"Input.AnimatedStarsEnabled\" value=\"true\"");
        if (enabled)
        {
            html.Append(" checked=\"checked\"");
        }
        html.Append(" />");
        html.Append("<input type=\"hidden\" name=\"Input.AnimatedStarsEnabled\" value=\"false\" />");
        html.Append("<span><strong style=\"display:block\">");
        html.Append(Encode(localizer["Label"].Value));
        html.Append("</strong><small style=\"display:block;margin-top:4px;color:var(--muted);font-weight:500;line-height:1.45\">");
        html.Append(Encode(localizer["Hint"].Value));
        html.Append("</small></span></label></div>");

        output.PostElement.AppendHtml(html.ToString());
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}