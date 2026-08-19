using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class HostMultipleChoiceAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var savedPresentationType =
            ViewContext.ViewData.Model is QuestionEditorModel editor
                ? (int)editor.Input.PresentationType
                : -1;

        output.PostContent.AppendHtml(
            $"<script src=\"/js/host-multiple-choice.js?v=1.20.0\" data-saved-question-type=\"{savedPresentationType}\"></script>");
    }
}
