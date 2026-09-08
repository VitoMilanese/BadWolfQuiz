using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-final-status")]
public sealed class PlayerFinalQuestionStageAssetsTagHelper : TagHelper
{
    private static readonly HashSet<string> FinalStatuses = new(
        ["finalwagering", "finalanswering", "finaljudging", "completed"],
        StringComparer.OrdinalIgnoreCase);

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var status = output.Attributes["data-final-status"]?.Value?.ToString();
        if (string.IsNullOrWhiteSpace(status) || !FinalStatuses.Contains(status))
        {
            return;
        }

        // Final Question has its own player runtime. Regular all-player, anonymous
        // shared-wager and peer-rated controllers are unrelated here, but they are
        // normally bootstrapped globally on Player/Lobby. Mark them initialized
        // before their scripts run so they do not start background polling or
        // document-wide observers behind the finale stage.
        output.PreContent.AppendHtml(
            "<script>" +
            "window.badWolfPlayerFinalStage=true;" +
            "window.badWolfAllPlayerQuestionInitialized=true;" +
            "window.BadWolfAnonymousSharedWagerPlayerStarted=true;" +
            "window.badWolfPeerRatedAllPlayerInitialized=true;" +
            "window.badWolfPeerRatedHostMountGuardInitialized=true;" +
            "window.badWolfPeerRatedRatingConfirmationInitialized=true;" +
            "window.badWolfPeerRatedPolishInitialized=true;" +
            "window.badWolfPeerRatedQuestionContextInitialized=true;" +
            "window.badWolfPeerRatedLayoutInitialized=true;" +
            "</script>" +
            "<script src=\"/js/player-final-fallback-refresh.js?v=1\"></script>" +
            "<link rel=\"stylesheet\" href=\"/css/player-final-question-stage.css?v=4\" />" +
            "<link rel=\"stylesheet\" href=\"/css/player-final-question-stage-stability.css?v=1\" />");
    }
}
