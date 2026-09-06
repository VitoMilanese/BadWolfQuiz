using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorGameSettingsTagHelper(
    IFileVersionProvider fileVersionProvider) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => -1000;

    public override void Process(
        TagHelperContext context,
        TagHelperOutput output)
    {
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        var isGlobalSettingsPage = string.Equals(
            page,
            "/Admin/Settings/Index",
            StringComparison.Ordinal);
        var isHostSettingsPage = page is
            "/Admin/Games/Lobby" or
            "/Admin/Settings/Index";
        var isPlayerPage = string.Equals(
            page,
            "/Player/Lobby",
            StringComparison.Ordinal);
        if (!isHostSettingsPage && !isPlayerPage)
        {
            return;
        }

        var requestPathBase = ViewContext.HttpContext.Request.PathBase;
        var html = HtmlEncoder.Default;

        if (string.Equals(output.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            if (isHostSettingsPage)
            {
                var stylesheetPath = fileVersionProvider.AddFileVersionToPath(
                    requestPathBase,
                    "/css/contributor-frames.css");
                output.PostContent.AppendHtml(
                    $"<link rel=\"stylesheet\" href=\"{html.Encode(stylesheetPath)}\" />");
            }

            if (isPlayerPage)
            {
                var playerStylesheetPath = fileVersionProvider.AddFileVersionToPath(
                    requestPathBase,
                    "/css/contributor-player-frame-settings.css");
                output.PostContent.AppendHtml(
                    $"<link rel=\"stylesheet\" href=\"{html.Encode(playerStylesheetPath)}\" />");
            }
            return;
        }

        if (!string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (isHostSettingsPage)
        {
            var scriptPath = fileVersionProvider.AddFileVersionToPath(
                requestPathBase,
                "/js/contributor-game-settings.js");
            output.PostContent.AppendHtml(
                $"<script src=\"{html.Encode(scriptPath)}\"></script>");

            if (isGlobalSettingsPage)
            {
                output.PostContent.AppendHtml(
                    """
                    <script>
                        (() => {
                            const mountGlobalHostFrameSettings = () => {
                                const template = document.getElementById("contributor-host-frame-template");
                                const grid = document.querySelector(
                                    ".host-settings-form .host-settings-host-grid");
                                if (!template || !grid ||
                                    grid.querySelector("[data-contributor-host-frame]")) {
                                    return;
                                }

                                const avatarField = grid.querySelector(":scope > .host-avatar-field");
                                const fragment = template.content.cloneNode(true);
                                if (!avatarField) {
                                    grid.append(fragment);
                                    return;
                                }

                                const frameRowHost = document.createElement("div");
                                frameRowHost.className =
                                    "settings-grid host-avatar-frame-global-settings";
                                frameRowHost.style.gridColumn = "1 / -1";
                                avatarField.before(frameRowHost);
                                frameRowHost.append(avatarField, fragment);
                            };

                            if (document.readyState === "loading") {
                                document.addEventListener(
                                    "DOMContentLoaded",
                                    mountGlobalHostFrameSettings,
                                    { once: true });
                            } else {
                                mountGlobalHostFrameSettings();
                            }
                        })();
                    </script>
                    """);
            }

            if (string.Equals(page, "/Admin/Games/Lobby", StringComparison.Ordinal))
            {
                var dialogScriptPath = fileVersionProvider.AddFileVersionToPath(
                    requestPathBase,
                    "/js/contributor-game-settings-dialog.js");
                output.PostContent.AppendHtml(
                    $"<script src=\"{html.Encode(dialogScriptPath)}\"></script>");
            }
        }

        if (isPlayerPage)
        {
            var playerScriptPath = fileVersionProvider.AddFileVersionToPath(
                requestPathBase,
                "/js/contributor-player-frame-settings.js");
            output.PostContent.AppendHtml(
                $"<script src=\"{html.Encode(playerScriptPath)}\"></script>");
        }
    }
}
