using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Pages.Admin.Games;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class QuestionBuzzerModeAssetsTagHelper(
    IStringLocalizer<SharedResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var model = ViewContext.ViewData.Model;
        if (model is not LobbyModel && model is not QuestionEditorModel)
        {
            return;
        }

        var savedDelay = model is QuestionEditorModel editor
            ? editor.Input.BuzzDelaySeconds
            : 0;
        var delayLabel = HtmlEncoder.Default.Encode(
            localizer["Label_BuzzDelay"].Value);

        output.PostContent.AppendHtml(
            $"<script src='/js/question-buzzer-modes.js?v=1.22.3' " +
            $"data-saved-buzz-delay='{savedDelay}' " +
            $"data-buzz-delay-label='{delayLabel}'></script>");
    }
}
