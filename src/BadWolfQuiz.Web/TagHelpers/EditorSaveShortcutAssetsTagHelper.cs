using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("body")]
public sealed class EditorSaveShortcutAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var model = ViewContext.ViewData.Model;
        if (model is not EditorModel &&
            model is not QuestionEditorModel &&
            model is not FinalQuestionEditorModel &&
            model is not DescriptionEditorModel)
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/editor-save-shortcut.js?v=1\"></script>");
    }
}
