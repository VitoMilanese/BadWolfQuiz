using System.Text.Encodings.Web;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("form", Attributes = "class")]
public sealed class QuizEditorUnfinishedGameGuardTagHelper(
    ActiveGameStore activeGameStore,
    ActiveGameAvailability activeGameAvailability,
    CurrentHost currentHost,
    IStringLocalizer<UnfinishedGameResource> unfinishedGameLocalizer,
    IStringLocalizer<SharedResource> localizer) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var classes = output.Attributes["class"]?.Value?.ToString() ?? string.Empty;
        var classNames = classes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!classNames.Contains("quiz-board-form", StringComparer.Ordinal))
        {
            return;
        }

        if (!int.TryParse(
                ViewContext.RouteData.Values["id"]?.ToString(),
                out var quizId))
        {
            return;
        }

        var unfinished = activeGameStore.Find(currentHost.RequiredId, quizId);
        if (unfinished is null || !activeGameAvailability.CanResume(unfinished))
        {
            return;
        }

        output.Attributes.SetAttribute(
            "data-editor-unfinished-game-guard",
            "true");
        output.PostContent.AppendHtml(
            "<input type=\"hidden\" name=\"replaceUnfinished\" value=\"false\" data-editor-replace-unfinished />");

        var title = HtmlEncoder.Default.Encode(
            unfinishedGameLocalizer["ReplaceDialogTitle"].Value);
        var text = HtmlEncoder.Default.Encode(
            unfinishedGameLocalizer["ReplaceDialogText"].Value);
        var cancel = HtmlEncoder.Default.Encode(
            localizer["Button_Cancel"].Value);
        var startNew = HtmlEncoder.Default.Encode(
            unfinishedGameLocalizer["StartNewGame"].Value);

        output.PostElement.AppendHtml($$"""
<dialog id="editorReplaceUnfinishedGameDialog" class="app-dialog">
    <div class="dialog-card dialog-card-danger">
        <div class="dialog-heading">
            <div>
                <h2>{{title}}</h2>
            </div>
            <button class="dialog-close"
                    type="button"
                    data-editor-close-replace-dialog
                    aria-label="{{cancel}}">×</button>
        </div>
        <p class="dialog-target" data-editor-replace-quiz-title></p>
        <p class="dialog-warning">{{text}}</p>
        <div class="form-actions dialog-actions">
            <button class="button button-secondary"
                    type="button"
                    data-editor-close-replace-dialog>{{cancel}}</button>
            <button class="button button-danger"
                    type="button"
                    data-editor-confirm-replace-game>{{startNew}}</button>
        </div>
    </div>
</dialog>
<script>
(() => {
    const form = document.querySelector(
        'form.quiz-board-form[data-editor-unfinished-game-guard="true"]');
    const playButton = form?.querySelector('button[name="play"][value="true"]');
    const replaceInput = form?.querySelector('[data-editor-replace-unfinished]');
    const dialog = document.getElementById('editorReplaceUnfinishedGameDialog');
    if (!(form instanceof HTMLFormElement) ||
        !(playButton instanceof HTMLButtonElement) ||
        !(replaceInput instanceof HTMLInputElement) ||
        !(dialog instanceof HTMLDialogElement)) {
        return;
    }

    const quizTitle = document.querySelector(
        '.quiz-editor-title-with-action > span')?.textContent?.trim() ?? '';
    const titleTarget = dialog.querySelector('[data-editor-replace-quiz-title]');
    if (titleTarget) {
        titleTarget.textContent = quizTitle;
    }

    playButton.type = 'button';
    playButton.addEventListener('click', () => {
        replaceInput.value = 'false';
        dialog.showModal();
    });

    dialog.querySelector('[data-editor-confirm-replace-game]')
        ?.addEventListener('click', () => {
            replaceInput.value = 'true';
            dialog.close();
            playButton.type = 'submit';
            form.requestSubmit(playButton);
        });

    dialog.querySelectorAll('[data-editor-close-replace-dialog]')
        .forEach(button => button.addEventListener('click', () => dialog.close()));

    dialog.addEventListener('click', event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });
})();
</script>
""");
    }
}
