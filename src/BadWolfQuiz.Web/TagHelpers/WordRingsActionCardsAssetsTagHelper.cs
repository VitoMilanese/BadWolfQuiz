using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-word-rings-root")]
public sealed class WordRingsActionCardsAssetsTagHelper : TagHelper
{
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.PreElement.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards-v2.css?v=7\" />" +
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards-patch.css?v=4\" />" +
            "<link rel=\"stylesheet\" href=\"/css/word-rings-action-cards-layout.css?v=3\" />" +
            "<link rel=\"stylesheet\" href=\"/css/word-rings-gameplay-options.css?v=1\" />");
        output.PostElement.AppendHtml(
            "<script src=\"/js/word-rings-gameplay-options.js?v=1\" defer></script>" +
            "<script src=\"/js/word-rings-gameplay-options-followup.js?v=1\" defer></script>" +
            "<script src=\"/js/word-rings-theme-sync.js?v=1\" defer></script>" +
            "<script src=\"/js/word-rings-action-cards-patch.js?v=2\" defer></script>" +
            "<script src=\"/js/word-rings-action-achievements.js?v=1\" defer></script>" +
            "<script src=\"/js/word-rings-action-cards-v2.js?v=5\" defer></script>" +
            "<script src=\"/js/word-rings-action-cards-layout.js?v=2\" defer></script>" +
            "<script src=\"/js/word-rings-action-cards-visible-words.js?v=1\" defer></script>");
    }
}
