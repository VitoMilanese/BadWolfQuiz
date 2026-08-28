using BadWolfQuiz.Game.Definitions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Globalization;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("select", Attributes = "asp-for")]
public sealed class AnonymousSharedWagerEditorTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")]
    public ModelExpression For { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!string.Equals(
                For.Name,
                "Input.PresentationType",
                StringComparison.Ordinal))
        {
            return;
        }

        var isShared = For.Model is QuestionPresentationType presentationType &&
            QuestionWagerModes.IsAnonymousShared(presentationType);
        var labels = GetLabels(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        var initialShared = isShared ? "true" : "false";
        var normalSelected = isShared ? string.Empty : " selected";
        var sharedSelected = isShared ? " selected" : string.Empty;

        // Value 6 remains an internal schema-compatible marker. It is hidden from
        // the question-type picker and is only selected immediately before save.
        output.PostContent.AppendHtml("<option value=\"6\" hidden></option>");
        output.PostElement.AppendHtml($$"""
            <div class="wager-mode-setting"
                 data-anonymous-shared-wager-editor
                 data-initial-shared="{{initialShared}}">
                <label for="anonymous-shared-wager-mode">{{labels.Mode}}</label>
                <select id="anonymous-shared-wager-mode"
                        data-anonymous-shared-wager-mode>
                    <option value="normal"{{normalSelected}}>{{labels.Normal}}</option>
                    <option value="shared"{{sharedSelected}}>{{labels.Shared}}</option>
                </select>
            </div>
            <script>
                (() => {
                    const root = document.currentScript?.previousElementSibling;
                    const presentationTypeSelect =
                        document.getElementById("Input_PresentationType");
                    const modeSelect =
                        root?.querySelector("[data-anonymous-shared-wager-mode]");
                    const form = presentationTypeSelect?.closest("form");

                    if (!root || !presentationTypeSelect || !modeSelect || !form) {
                        return;
                    }

                    if (root.dataset.initialShared === "true") {
                        presentationTypeSelect.value = "0";
                    }

                    const syncVisibility = () => {
                        const isCompatible = presentationTypeSelect.value === "0";
                        root.hidden = !isCompatible;
                        if (!isCompatible && modeSelect.value === "shared") {
                            modeSelect.value = "normal";
                        }
                    };

                    modeSelect.addEventListener("change", () => {
                        if (modeSelect.value === "shared" &&
                            presentationTypeSelect.value !== "0") {
                            presentationTypeSelect.value = "0";
                            presentationTypeSelect.dispatchEvent(
                                new Event("change", { bubbles: true }));
                        }
                        syncVisibility();
                    });

                    presentationTypeSelect.addEventListener(
                        "change",
                        syncVisibility);

                    form.addEventListener("submit", () => {
                        const visiblePresentationType =
                            presentationTypeSelect.value;
                        const useSharedWager =
                            modeSelect.value === "shared" &&
                            visiblePresentationType === "0";

                        if (!useSharedWager) {
                            return;
                        }

                        presentationTypeSelect.value = "6";
                        queueMicrotask(() => {
                            presentationTypeSelect.value =
                                visiblePresentationType;
                        });
                    });

                    syncVisibility();
                })();
            </script>
            """);
    }

    private static (string Mode, string Normal, string Shared) GetLabels(
        string language) => language switch
        {
            "uk" => (
                "Режим ставки",
                "Звичайна ставка",
                "Анонімна спільна ставка"),
            "ru" => (
                "Режим ставки",
                "Обычная ставка",
                "Анонимная общая ставка"),
            "it" => (
                "Modalità scommessa",
                "Scommessa normale",
                "Scommessa condivisa anonima"),
            _ => (
                "Wager mode",
                "Normal wager",
                "Anonymous shared wager")
        };
}
