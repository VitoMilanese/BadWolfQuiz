using BadWolfQuiz.Game.Definitions;
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
        var savedPresentationType =
            ViewContext.ViewData.Model is QuestionEditorModel editor
                ? (int)editor.Input.PresentationType
                : -1;
        var lobby = ViewContext.ViewData.Model as LobbyModel;
        var isHostLobby = lobby is not null;
        var currentHostMultipleChoice =
            lobby?.CurrentQuestion?.IsHostMultipleChoice == true;
        var hostLobbyValue = isHostLobby.ToString().ToLowerInvariant();
        var currentHostMultipleChoiceValue =
            currentHostMultipleChoice.ToString().ToLowerInvariant();

        output.PostContent.AppendHtml(
            $"<script src=\"/js/host-multiple-choice.js?v=1.20.0-259.3\" data-saved-question-type=\"{savedPresentationType}\"></script>" +
            $"<script src=\"/js/host-multiple-choice-bootstrap.js?v=1.20.0-259.4\" data-host-lobby=\"{hostLobbyValue}\" data-current-host-multiple-choice=\"{currentHostMultipleChoiceValue}\"></script>");
    }
}
