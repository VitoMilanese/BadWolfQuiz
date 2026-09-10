using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("a", Attributes = "href")]
public sealed class GitHubAchievementLinkTagHelper(
    IOptions<ProjectOptions> projectOptions) : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var configuredUrl = projectOptions.Value.GetGitHubUrl();
        var href = output.Attributes["href"]?.Value?.ToString();
        if (configuredUrl is null ||
            !string.Equals(href, configuredUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        output.Attributes.SetAttribute("href", "/project/github");
    }
}
