using BadWolfQuiz.Web.Localization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("button", Attributes = "class")]
public sealed class QuizEditorCategoryExchangeTooltipTagHelper(
    IStringLocalizer<SharedResource> localizer) : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classValue = output.Attributes["class"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(classValue) ||
            !classValue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("js-category-exchange", StringComparer.Ordinal))
        {
            return;
        }

        var tooltip = localizer["QuizEditor_ExchangeCategory"].Value;
        output.Attributes.SetAttribute("title", tooltip);
        output.Attributes.SetAttribute("aria-label", tooltip);
    }
}
