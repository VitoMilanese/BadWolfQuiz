using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("div", Attributes = "data-game-intro-page")]
[HtmlTargetElement("div", Attributes = "data-game-code,data-player-id,data-final-status")]
[HtmlTargetElement("div", Attributes = "data-host-gameplay-view")]
[HtmlTargetElement("section", Attributes = "data-game-code,data-game-status,data-remove-player-label")]
public sealed class GameplayPolishAssetsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.Equals(output.TagName, "section", StringComparison.OrdinalIgnoreCase))
        {
            // GameHub sends the current player image as imageDataUrl while the
            // host lobby fingerprint still reads the legacy uploaded-image
            // field names. Load the contract adapter before _Layout loads
            // SignalR; the adapter hooks that later global assignment so the
            // host connection is patched before Lobby.cshtml builds it.
            output.PreContent.AppendHtml(
                "<script src=\"/js/host-lobby-player-visual-contract.js?v=2\"></script>");
            return;
        }

        if (output.Attributes.ContainsName("data-player-id") &&
            output.Attributes.ContainsName("data-final-status"))
        {
            // Player pages can survive for a long time on phones while the browser
            // suspends JavaScript and network activity. Install the recovery adapter
            // before SignalR is loaded so the eventual player connection can restart
            // itself and refresh presence/question state when the page resumes.
            output.PreContent.AppendHtml(
                "<script src=\"/js/player-mobile-recovery.js?v=1\"></script>");
        }

        if (output.Attributes.TryGetAttribute("data-final-status", out var finalStatus) &&
            string.Equals(
                finalStatus.Value?.ToString(),
                "running",
                StringComparison.OrdinalIgnoreCase))
        {
            // The player page is intentionally long-lived through regular gameplay.
            // Keep the large waiting-room buzzer geometry for both Lobby and Running,
            // including a direct refresh while the game is already running.
            output.Attributes.SetAttribute("data-final-status", "lobby");
        }

        output.PreContent.AppendHtml(
            "<link rel=\"stylesheet\" href=\"/css/gameplay-polish.css?v=4\" />" +
            "<script src=\"/js/game-content-viewport-fit.js?v=8\"></script>" +
            "<script src=\"/js/gameplay-polish.js?v=3\"></script>");
    }
}
