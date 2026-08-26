using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("form", Attributes = "data-ajax-question-editor")]
public sealed class MultipleChoiceAnswerOptionsAssetsTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => -2000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not QuestionEditorModel editor)
        {
            return;
        }

        output.PreContent.AppendHtml(
            "<script>" +
            "window.badWolfHostMultipleChoiceBootstrapInitialized=true;" +
            "</script>" +
            "<script src=\"/js/multiple-choice-answer-options-guard.js?v=382.7\"></script>" +
            $"<script src=\"/js/multiple-choice-answer-options.js?v=382.7\" " +
            $"data-saved-question-type=\"{(int)editor.Input.PresentationType}\"></script>" +
            "<script>" +
            "document.addEventListener('DOMContentLoaded',()=>{" +
            "window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver?.();" +
            "window.setTimeout(()=>{" +
            "const s=document.querySelector('[data-question-save-status]');" +
            "if(!s)return;" +
            "const p={h:s.hidden,t:s.textContent,c:s.className,d:s.style.display};" +
            "s.style.display='none';s.classList.add('alert-success');" +
            "s.classList.remove('alert-error');s.textContent='editor-state-synchronized';" +
            "s.hidden=false;window.setTimeout(()=>{" +
            "s.hidden=p.h;s.textContent=p.t;s.className=p.c;s.style.display=p.d;" +
            "},0);" +
            "},0);" +
            "},{once:true});" +
            "</script>");
    }
}
