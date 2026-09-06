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
            // The host lobby's existing player fingerprint still reads the legacy
            // uploaded-image field names while GameHub sends imageDataUrl. Load a
            // tiny contract adapter before the host SignalR connection is created
            // so avatar/image changes invalidate that fingerprint immediately.
            output.PreContent.AppendHtml(
                "<script src=\"/js/host-lobby-player-visual-contract.js?v=1\"></script>");
            return;
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
            "<link rel=\"stylesheet\" href=\"/css/gameplay-polish.css?v=3\" />" +
            "<script src=\"/js/gameplay-polish.js?v=3\"></script>");
    }
}
