using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("button", Attributes = "id")]
public sealed class AnonymousSharedWagerBuzzerTagHelper(
    GameSessionRegistry sessions,
    IHttpContextAccessor httpContextAccessor) : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                output.Attributes["id"]?.Value?.ToString(),
                "player-buzzer",
                StringComparison.Ordinal))
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var code = httpContext?.Request.RouteValues["code"]?.ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var game = sessions.Find(code);
        var question = game?.Session.Board.Questions.FirstOrDefault(item =>
            (item.Status is RuntimeQuestionStatus.AwaitingWager or RuntimeQuestionStatus.Active) &&
            QuestionWagerModes.IsAnonymousShared(item.PresentationType));

        if (question is not null)
        {
            output.SuppressOutput();
        }
    }
}
