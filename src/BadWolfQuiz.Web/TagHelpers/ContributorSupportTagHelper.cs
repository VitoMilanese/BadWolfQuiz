using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorSupportTagHelper(
    IAntiforgery antiforgery,
    GameSettingsStore settingsStore,
    CurrentHost currentHost,
    IOptions<FooterOptions> footerOptions,
    IStringLocalizer<ContributorResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        if (string.Equals(output.TagName, "head", StringComparison.OrdinalIgnoreCase))
        {
            output.PostContent.AppendHtml(
                "<link rel=\"stylesheet\" href=\"/css/contributor-frames.css\" />");
            return;
        }

        if (!string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var httpContext = ViewContext.HttpContext;
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        BadWolfQuiz.Game.Runtime.GameSessionSettings? settings = null;
        if (isAuthenticated && currentHost.Id is { } hostId)
        {
            settings = await settingsStore.LoadAsync(hostId, httpContext.RequestAborted);
        }

        var hostIsContributor = ViewContext.ViewData["ContributorHost"] is bool hostOverride
            ? hostOverride
            : isAuthenticated && ContributorRecognition.IsContributor(
                footerOptions.Value,
                settings?.HostName);
        var hostFrameEnabled = hostIsContributor &&
            settings?.HostAvatarFrameEnabled == true;
        var hostFrameId = ContributorAvatarFrameCatalog.Normalize(
            settings?.HostAvatarFrameId);
        var playerIsContributor =
            ViewContext.ViewData["ContributorPlayer"] is true;
        var playerFrameEnabled = playerIsContributor &&
            ViewContext.ViewData["ContributorPlayerFrameEnabled"] is true;
        var playerFrameId = ContributorAvatarFrameCatalog.Normalize(
            ViewContext.ViewData["ContributorPlayerFrameId"]?.ToString());

        output.Attributes.SetAttribute(
            "data-contributor-host",
            hostIsContributor ? "true" : "false");
        output.Attributes.SetAttribute(
            "data-contributor-host-frame-enabled",
            hostFrameEnabled ? "true" : "false");
        output.Attributes.SetAttribute(
            "data-contributor-host-frame-id",
            hostFrameId);
        output.Attributes.SetAttribute(
            "data-contributor-player",
            playerIsContributor ? "true" : "false");
        output.Attributes.SetAttribute(
            "data-contributor-player-frame-enabled",
            playerFrameEnabled ? "true" : "false");
        output.Attributes.SetAttribute(
            "data-contributor-player-frame-id",
            playerFrameId);
        output.Attributes.SetAttribute(
            "data-contributor-frame-save-failed",
            localizer["ContributorFrame_SaveFailed"].Value);

        var html = HtmlEncoder.Default;
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        if (hostIsContributor && string.Equals(
                page,
                "/Admin/Settings/Index",
                StringComparison.Ordinal))
        {
            output.PostContent.AppendHtml(BuildHostTemplate(
                html,
                settings?.HostAvatarFrameEnabled == true,
                hostFrameId));
        }

        if (playerIsContributor)
        {
            var requestToken = antiforgery
                .GetAndStoreTokens(httpContext)
                .RequestToken;
            if (!string.IsNullOrWhiteSpace(requestToken))
            {
                output.PostContent.AppendHtml(BuildPlayerTemplate(
                    html,
                    requestToken,
                    playerFrameEnabled,
                    playerFrameId));
            }
        }

        if (ShouldShowThankYou())
        {
            ContributorRecognition.MarkThankYouShown(
                httpContext.Response,
                httpContext.Request.IsHttps);
            output.PostContent.AppendHtml(BuildThankYouDialog(html));
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/contributor-frames.js\" defer></script>");
    }

    private bool ShouldShowThankYou()
    {
        var value = ViewContext.TempData[ContributorRecognition.ThankYouTempDataKey];
        return value is true ||
            string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildHostTemplate(
        HtmlEncoder html,
        bool enabled,
        string frameId)
    {
        var checkedAttribute = enabled ? " checked" : string.Empty;
        return $$"""
            <template id="contributor-host-frame-template">
                <div class="contributor-frame-settings" data-contributor-host-frame>
                    <strong>{{html.Encode(localizer["ContributorFrame_Title"].Value)}}</strong>
                    <label class="settings-checkbox">
                        <input type="checkbox"
                               name="Input.HostAvatarFrameEnabled"
                               value="true"{{checkedAttribute}} />
                        <input type="hidden"
                               name="Input.HostAvatarFrameEnabled"
                               value="false" />
                        <span>{{html.Encode(localizer["ContributorFrame_Enable"].Value)}}</span>
                    </label>
                    <label>
                        <span>{{html.Encode(localizer["ContributorFrame_Label"].Value)}}</span>
                        <select name="Input.HostAvatarFrameId">
                            {{BuildFrameOptions(html, frameId)}}
                        </select>
                    </label>
                    <small>{{html.Encode(localizer["ContributorFrame_Hint"].Value)}}</small>
                </div>
            </template>
            """;
    }

    private string BuildPlayerTemplate(
        HtmlEncoder html,
        string requestToken,
        bool enabled,
        string frameId)
    {
        var checkedAttribute = enabled ? " checked" : string.Empty;
        return $$"""
            <template id="contributor-player-frame-template">
                <div class="contributor-frame-settings" data-contributor-player-frame>
                    <strong>{{html.Encode(localizer["ContributorFrame_Title"].Value)}}</strong>
                    <label class="settings-checkbox">
                        <input type="checkbox"
                               data-contributor-frame-enabled{{checkedAttribute}} />
                        <span>{{html.Encode(localizer["ContributorFrame_Enable"].Value)}}</span>
                    </label>
                    <label>
                        <span>{{html.Encode(localizer["ContributorFrame_Label"].Value)}}</span>
                        <select data-contributor-frame-id>
                            {{BuildFrameOptions(html, frameId)}}
                        </select>
                    </label>
                    <small>{{html.Encode(localizer["ContributorFrame_Hint"].Value)}}</small>
                    <input type="hidden"
                           data-contributor-antiforgery
                           value="{{html.Encode(requestToken)}}" />
                    <small class="contributor-frame-status"
                           data-contributor-frame-status
                           aria-live="polite"></small>
                </div>
            </template>
            """;
    }

    private string BuildFrameOptions(HtmlEncoder html, string selectedFrameId)
    {
        return string.Concat(ContributorAvatarFrameCatalog.Frames.Select(frame =>
        {
            var selected = string.Equals(
                frame.Id,
                selectedFrameId,
                StringComparison.Ordinal)
                    ? " selected"
                    : string.Empty;
            return $"<option value=\"{html.Encode(frame.Id)}\"{selected}>" +
                $"{html.Encode(localizer[frame.ResourceKey].Value)}</option>";
        }));
    }

    private string BuildThankYouDialog(HtmlEncoder html) => $$"""
        <dialog class="app-dialog"
                data-contributor-thanks-dialog
                aria-labelledby="contributor-thanks-title">
            <div class="dialog-card">
                <div class="dialog-heading">
                    <h2 id="contributor-thanks-title">
                        {{html.Encode(localizer["ContributorThanks_Title"].Value)}}
                    </h2>
                    <button class="dialog-close"
                            type="button"
                            data-close-contributor-thanks
                            aria-label="{{html.Encode(localizer["ContributorThanks_Close"].Value)}}">×</button>
                </div>
                <p>{{html.Encode(localizer["ContributorThanks_Message"].Value)}}</p>
                <div class="form-actions dialog-actions">
                    <button class="button button-primary"
                            type="button"
                            data-close-contributor-thanks>
                        {{html.Encode(localizer["ContributorThanks_Close"].Value)}}
                    </button>
                </div>
            </div>
        </dialog>
        <script>
            (() => {
                const dialog = document.querySelector("[data-contributor-thanks-dialog]");
                if (!dialog) return;
                for (const button of dialog.querySelectorAll("[data-close-contributor-thanks]")) {
                    button.addEventListener("click", () => dialog.close());
                }
                dialog.addEventListener("click", event => {
                    if (event.target === dialog) dialog.close();
                });
                dialog.showModal();
            })();
        </script>
        """;
}
