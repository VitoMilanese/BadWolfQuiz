using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using BadWolfQuiz.Web.Localization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "class")]
public sealed class FinalQuestionJudgingProgressTagHelper(
    IStringLocalizer<SharedResource> localizer,
    HtmlEncoder htmlEncoder) : TagHelper
{
    private static readonly Regex ProgressParagraphRegex = new(
        "<p class=\"dialog-warning\">\\s*(?<text>[^<]*?)\\s*</p>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumberRegex = new(
        "\\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var classValue = context.AllAttributes["class"]?.Value?.ToString();
        if (!HasCssClass(classValue, "final-judging-list"))
        {
            return;
        }

        var childContent = await output.GetChildContentAsync();
        var html = childContent.GetContent();
        var progressMatch = ProgressParagraphRegex.Match(html);
        if (!progressMatch.Success)
        {
            return;
        }

        var numbers = NumberRegex.Matches(progressMatch.Groups["text"].Value);
        if (numbers.Count < 2)
        {
            return;
        }

        var answerLabel = htmlEncoder.Encode(localizer["GameBoard_Answer"].Value);
        var replacement =
            $"<p class=\"dialog-warning\">{answerLabel} {numbers[0].Value} / {numbers[1].Value}</p>";
        var updatedHtml =
            html[..progressMatch.Index] +
            replacement +
            html[(progressMatch.Index + progressMatch.Length)..];

        output.Content.SetHtmlContent(updatedHtml);
    }

    private static bool HasCssClass(string? classes, string className) =>
        classes?
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(className, StringComparer.Ordinal) == true;
}
