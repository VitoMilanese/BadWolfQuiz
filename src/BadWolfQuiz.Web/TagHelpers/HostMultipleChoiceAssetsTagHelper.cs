using BadWolfQuiz.Web.Pages.Admin.Games;
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
        var model = ViewContext.ViewData.Model;
        if (model is not LobbyModel && model is not QuestionEditorModel)
        {
            return;
        }

        var savedPresentationType = model is QuestionEditorModel editor
            ? (int)editor.Input.PresentationType
            : -1;

        output.PostContent.AppendHtml(
            $"<script src=\"/js/host-multiple-choice.js?v=1.20.0-259.8\" data-saved-question-type=\"{savedPresentationType}\"></script>" +
            "<script src=\"/js/host-multiple-choice-bootstrap.js?v=1.26.5\"></script>");
    }
}
