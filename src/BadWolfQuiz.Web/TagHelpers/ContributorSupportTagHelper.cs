using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("head")]
[HtmlTargetElement("body")]
public sealed class ContributorSupportTagHelper(
    IAntiforgery antiforgery,
    GameSettingsStore settingsStore,
    CurrentHost currentHost,
    QuizDbContext db,
    IOptions<FooterOptions> footerOptions,
    IStringLocalizer<ContributorResource> localizer,
    IWebHostEnvironment environment) : TagHelper
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
            var frameInsetStyles = BuildFrameInsetStyles(
                ContributorAvatarFrameCatalog.GetFrames(environment));
            if (!string.IsNullOrEmpty(frameInsetStyles))
            {
                output.PostContent.AppendHtml(frameInsetStyles);
            }
            return;
        }

        if (!string.Equals(output.TagName, "body", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var httpContext = ViewContext.HttpContext;
        var isAuthenticated = httpContext.User.Identity?.IsAuthenticated == true;
        var authenticatedHostId = isAuthenticated ? currentHost.Id : null;
        BadWolfQuiz.Game.Runtime.GameSessionSettings? settings = null;
        string? hostDisplayName = null;
        if (authenticatedHostId is { } hostId)
        {
            settings = await settingsStore.LoadAsync(hostId, httpContext.RequestAborted);
            hostDisplayName = await db.Hosts
                .AsNoTracking()
                .Where(host => host.Id == hostId)
                .Select(host => host.DisplayName)
                .SingleOrDefaultAsync(httpContext.RequestAborted);
        }

        var frames = ContributorAvatarFrameCatalog.GetFrames(environment);
        var defaultFrameId = frames.FirstOrDefault()?.Id ?? string.Empty;
        var hostIsContributor = ViewContext.ViewData["ContributorHost"] is bool hostOverride
            ? hostOverride
            : isAuthenticated && ContributorRecognition.IsContributor(
                footerOptions.Value,
                hostDisplayName);
        var hostFrameEnabled = hostIsContributor &&
            settings?.HostAvatarFrameEnabled == true &&
            ContributorAvatarFrameCatalog.IsValid(
                environment,
                settings.HostAvatarFrameId);
        var hostFrameId = ContributorAvatarFrameCatalog.Normalize(
            environment,
            settings?.HostAvatarFrameId) ?? defaultFrameId;
        var playerIsContributor =
            ViewContext.ViewData["ContributorPlayer"] is true;
        var playerFrameIdValue =
            ViewContext.ViewData["ContributorPlayerFrameId"]?.ToString();
        var playerFrameEnabled = playerIsContributor &&
            ViewContext.ViewData["ContributorPlayerFrameEnabled"] is true &&
            ContributorAvatarFrameCatalog.IsValid(
                environment,
                playerFrameIdValue);
        var playerFrameId = ContributorAvatarFrameCatalog.Normalize(
            environment,
            playerFrameIdValue) ?? defaultFrameId;

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
            "data-contributor-frame-default-id",
            defaultFrameId);
        output.Attributes.SetAttribute(
            "data-contributor-frame-save-failed",
            localizer["ContributorFrame_SaveFailed"].Value);

        var html = HtmlEncoder.Default;
        var page = ViewContext.RouteData.Values["page"]?.ToString();
        var showHostFrameControls = frames.Count > 0 &&
            hostIsContributor &&
            string.Equals(
                page,
                "/Admin/Settings/Index",
                StringComparison.Ordinal);

        if (showHostFrameControls)
        {
            output.PostContent.AppendHtml(BuildHostTemplate(
                html,
                hostFrameEnabled,
                hostFrameId));
        }

        if (playerIsContributor && frames.Count > 0)
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

        if (showHostFrameControls || (playerIsContributor && frames.Count > 0))
        {
            output.PostContent.AppendHtml(BuildFramePicker(html, frames));
        }

        if (hostIsContributor &&
            authenticatedHostId is not null &&
            ContributorRecognition.ShouldShowThankYou(
                footerOptions.Value,
                hostDisplayName,
                authenticatedHostId,
                httpContext.Request))
        {
            ContributorRecognition.MarkThankYouShown(
                httpContext.Response,
                authenticatedHostId,
                httpContext.Request.IsHttps);
            output.PostContent.AppendHtml(BuildThankYouDialog(html));
        }

        output.PostContent.AppendHtml(
            "<script src=\"/js/contributor-frames.js\" defer></script>");
    }

    private string BuildHostTemplate(
        HtmlEncoder html,
        bool enabled,
        string frameId)
    {
        var checkedAttribute = enabled ? " checked" : string.Empty;
        var frameUrl = ContributorAvatarFrameCatalog.GetUrl(
            environment,
            frameId) ?? string.Empty;
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
                    <div class="contributor-frame-choice">
                        <span>{{html.Encode(localizer["ContributorFrame_Label"].Value)}}</span>
                        <div class="contributor-frame-choice-row">
                            <img class="contributor-frame-choice-preview"
                                 data-contributor-frame-preview
                                 src="{{html.Encode(frameUrl)}}"
                                 alt="" />
                            <button class="button button-secondary"
                                    type="button"
                                    data-open-contributor-frame-picker>
                                {{html.Encode(localizer["ContributorFrame_Label"].Value)}}
                            </button>
                        </div>
                        <input type="hidden"
                               name="Input.HostAvatarFrameId"
                               value="{{html.Encode(frameId)}}"
                               data-contributor-frame-id />
                    </div>
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
        var frameUrl = ContributorAvatarFrameCatalog.GetUrl(
            environment,
            frameId) ?? string.Empty;
        return $$"""
            <template id="contributor-player-frame-template">
                <div class="contributor-frame-settings" data-contributor-player-frame>
                    <strong>{{html.Encode(localizer["ContributorFrame_Title"].Value)}}</strong>
                    <label class="settings-checkbox">
                        <input type="checkbox"
                               data-contributor-frame-enabled{{checkedAttribute}} />
                        <span>{{html.Encode(localizer["ContributorFrame_Enable"].Value)}}</span>
                    </label>
                    <div class="contributor-frame-choice">
                        <span>{{html.Encode(localizer["ContributorFrame_Label"].Value)}}</span>
                        <div class="contributor-frame-choice-row">
                            <img class="contributor-frame-choice-preview"
                                 data-contributor-frame-preview
                                 src="{{html.Encode(frameUrl)}}"
                                 alt="" />
                            <button class="button button-secondary"
                                    type="button"
                                    data-open-contributor-frame-picker>
                                {{html.Encode(localizer["ContributorFrame_Label"].Value)}}
                            </button>
                        </div>
                        <input type="hidden"
                               value="{{html.Encode(frameId)}}"
                               data-contributor-frame-id />
                    </div>
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

    private string BuildFramePicker(
        HtmlEncoder html,
        IReadOnlyList<ContributorAvatarFrame> frames)
    {
        var options = string.Concat(frames.Select(frame =>
            $"<button type=\"button\" class=\"avatar-option contributor-frame-option\" " +
            $"data-contributor-frame-option=\"{html.Encode(frame.Id)}\" " +
            $"data-contributor-frame-url=\"{html.Encode(frame.Url)}\" " +
            $"aria-label=\"{html.Encode(localizer["ContributorFrame_Label"].Value)} {html.Encode(frame.Id)}\">" +
            $"<img src=\"{html.Encode(frame.Url)}\" alt=\"\" /></button>"));

        return $$"""
            <dialog class="avatar-picker contributor-frame-picker" data-contributor-frame-picker>
                <div class="avatar-picker-card">
                    <header class="dialog-heading">
                        <h2>{{html.Encode(localizer["ContributorFrame_Label"].Value)}}</h2>
                        <button class="dialog-close"
                                type="button"
                                data-close-contributor-frame-picker
                                aria-label="{{html.Encode(localizer["ContributorThanks_Close"].Value)}}">×</button>
                    </header>
                    <div class="avatar-option-grid contributor-frame-option-grid">
                        {{options}}
                    </div>
                </div>
            </dialog>
            """;
    }

    private static string BuildFrameInsetStyles(
        IReadOnlyList<ContributorAvatarFrame> frames)
    {
        if (frames.Count == 0)
        {
            return string.Empty;
        }

        var rules = string.Concat(frames.Select(frame =>
            $".contributor-frame-owner[data-avatar-frame=\"{frame.Id}\"] " +
            ".contributor-frame-avatar-source{" +
            $"--contributor-frame-avatar-inset:{frame.AvatarInsetPixels}px!important;" +
            "}"));
        return $"<style data-contributor-frame-insets>{rules}</style>";
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
