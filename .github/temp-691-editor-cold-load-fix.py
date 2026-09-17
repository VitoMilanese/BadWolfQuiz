from pathlib import Path


def replace(path: str, old: str, new: str, count: int = 1) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    actual = text.count(old)
    if actual != count:
        raise RuntimeError(f"{path}: expected {count} occurrence(s), found {actual}: {old[:120]!r}")
    file.write_text(text.replace(old, new, count), encoding="utf-8")


page = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml"
controller = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
guard = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options-guard.js"
tag_helper = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
on_demand_test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
stability_test = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"

# Server-render a deterministic initial on-demand editor state. Use both persisted
# representations so a stale/legacy client checkbox can never decide first paint.
replace(
    page,
    '            Blocks = Model.Input.AnswerBlocks\n        };\n}',
    '            Blocks = Model.Input.AnswerBlocks\n'
    '        };\n\n'
    '    var isOnDemandChoice =\n'
    '        Model.Input.PresentationType ==\n'
    '            BadWolfQuiz.Game.Definitions.QuestionPresentationType.AllPlayerMultipleChoice &&\n'
    '        (Model.Input.RevealAnswerOptionsOnDemand ||\n'
    '         Model.Input.AllPlayerMode ==\n'
    '            BadWolfQuiz.Web.Services.AllPlayerQuestionCompatibility.MultipleChoiceOnDemandMode);\n'
    '    Model.Input.RevealAnswerOptionsOnDemand = isOnDemandChoice;\n'
    '}'
)

replace(
    page,
    '</style>\n\n<form method="post" class="question-editor" enctype="multipart/form-data"\n'
    '      data-ajax-question-editor\n'
    '      data-save-error="@Localizer["Error_Unexpected"]">',
    '    .question-editor[data-on-demand-choice="true"] .answer-reward-modifier-setting {\n'
    '        display: none !important;\n'
    '    }\n'
    '</style>\n\n<form method="post" class="question-editor" enctype="multipart/form-data"\n'
    '      data-ajax-question-editor\n'
    '      data-on-demand-choice="@(isOnDemandChoice ? "true" : "false")"\n'
    '      data-save-error="@Localizer["Error_Unexpected"]">'
)

replace(
    page,
    '        <label class="checkbox-row all-player-choice-on-demand-setting"\n'
    '               data-all-player-choice-on-demand-setting\n'
    '               hidden>',
    '        <label class="checkbox-row all-player-choice-on-demand-setting"\n'
    '               data-all-player-choice-on-demand-setting\n'
    '               hidden="@(Model.Input.PresentationType == BadWolfQuiz.Game.Definitions.QuestionPresentationType.AllPlayerMultipleChoice ? null : "hidden")">'
)

replace(
    page,
    '        <div id="wager-mode-setting">',
    '        <div id="wager-mode-setting"\n'
    '             hidden="@((Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard && !isOnDemandChoice) ? "hidden" : null)">'
)

replace(
    page,
    '        <label class="checkbox-row answer-reward-modifier-setting"\n'
    '               hidden="@(Model.Input.RevealAnswerOptionsOnDemand ? "hidden" : null)">',
    '        <label class="checkbox-row answer-reward-modifier-setting"\n'
    '               hidden="@(isOnDemandChoice ? "hidden" : null)">'
)

replace(
    page,
    '        const wagerQuestionCheckbox =\n'
    '            document.getElementById("Input_IsSpecial");',
    '        const questionEditorForm =\n'
    '            document.querySelector("form[data-ajax-question-editor]");\n\n'
    '        const wagerQuestionCheckbox =\n'
    '            document.getElementById("Input_IsSpecial");'
)

replace(
    page,
    '            const isOnDemandChoice =\n'
    '                presentationTypeSelect?.value === "3" &&\n'
    '                onDemandChoiceCheckbox?.checked === true;',
    '            const isOnDemandChoice =\n'
    '                presentationTypeSelect?.value === "3" &&\n'
    '                (onDemandChoiceCheckbox?.checked === true ||\n'
    '                 questionEditorForm?.dataset.onDemandChoice === "true");'
)

replace(
    page,
    '            if (isOnDemandChoice && answerRewardModifierCheckbox) {\n'
    '                answerRewardModifierCheckbox.checked = false;\n'
    '            }',
    '            if (answerRewardModifierCheckbox) {\n'
    '                answerRewardModifierCheckbox.disabled = isOnDemandChoice;\n'
    '                if (isOnDemandChoice) {\n'
    '                    answerRewardModifierCheckbox.checked = false;\n'
    '                }\n'
    '            }'
)

replace(
    page,
    '        onDemandChoiceCheckbox?.addEventListener(\n'
    '            "change",\n'
    '            updateQuestionTypeUi);\n\n'
    '        document.addEventListener(\n'
    '            "badwolf:question-editor-on-demand-synced",\n'
    '            updateQuestionTypeUi);',
    '        const syncOnDemandDatasetFromCheckbox = () => {\n'
    '            if (!questionEditorForm) {\n'
    '                return;\n'
    '            }\n'
    '            const enabled =\n'
    '                presentationTypeSelect?.value === "3" &&\n'
    '                onDemandChoiceCheckbox?.checked === true;\n'
    '            questionEditorForm.dataset.onDemandChoice =\n'
    '                enabled ? "true" : "false";\n'
    '        };\n\n'
    '        onDemandChoiceCheckbox?.addEventListener(\n'
    '            "change",\n'
    '            () => {\n'
    '                syncOnDemandDatasetFromCheckbox();\n'
    '                updateQuestionTypeUi();\n'
    '            });\n\n'
    '        document.addEventListener(\n'
    '            "badwolf:question-editor-on-demand-synced",\n'
    '            () => {\n'
    '                syncOnDemandDatasetFromCheckbox();\n'
    '                updateQuestionTypeUi();\n'
    '            });'
)

replace(
    page,
    '        presentationTypeSelect?.addEventListener("change", async () => {\n'
    '            updateQuestionTypeUi();',
    '        presentationTypeSelect?.addEventListener("change", async () => {\n'
    '            syncOnDemandDatasetFromCheckbox();\n'
    '            updateQuestionTypeUi();'
)

# Keep the external controller as the authoritative dynamic synchronizer, but
# make the form itself expose the state to the page-level controls.
replace(
    controller,
    '        if (onDemandCheckbox instanceof HTMLInputElement &&\n'
    '            modeInput instanceof HTMLInputElement) {\n'
    '            onDemandCheckbox.checked =\n'
    '                modeInput.value === "multipleChoiceOnDemand";\n'
    '            document.dispatchEvent(new CustomEvent(\n'
    '                "badwolf:question-editor-on-demand-synced"));\n'
    '        }',
    '        if (onDemandCheckbox instanceof HTMLInputElement &&\n'
    '            modeInput instanceof HTMLInputElement) {\n'
    '            onDemandCheckbox.checked =\n'
    '                modeInput.value === "multipleChoiceOnDemand";\n'
    '            form.dataset.onDemandChoice =\n'
    '                onDemandCheckbox.checked ? "true" : "false";\n'
    '            document.dispatchEvent(new CustomEvent(\n'
    '                "badwolf:question-editor-on-demand-synced"));\n'
    '        }'
)

replace(
    controller,
    '            const onDemand = allPlayerChoice &&\n'
    '                onDemandCheckbox instanceof HTMLInputElement &&\n'
    '                onDemandCheckbox.checked;\n'
    '            const isAllPlayer = isText || (allPlayerChoice && !onDemand);',
    '            const onDemand = allPlayerChoice &&\n'
    '                onDemandCheckbox instanceof HTMLInputElement &&\n'
    '                onDemandCheckbox.checked;\n'
    '            form.dataset.onDemandChoice = onDemand ? "true" : "false";\n'
    '            const isAllPlayer = isText || (allPlayerChoice && !onDemand);'
)

replace(
    controller,
    '    if (document.readyState === "loading") {\n'
    '        document.addEventListener(\n'
    '            "DOMContentLoaded",\n'
    '            waitForEditorMount,\n'
    '            { once: true });\n'
    '    } else {\n'
    '        waitForEditorMount();\n'
    '    }',
    '    waitForEditorMount();'
)

# The asset tag helper now emits at the end of the form, after every editor
# control exists, so initialization no longer depends on network/cache timing.
replace(tag_helper, '        output.PreContent.AppendHtml(', '        output.PostContent.AppendHtml(')
replace(tag_helper, 'multiple-choice-answer-options-guard.js?v=382.10', 'multiple-choice-answer-options-guard.js?v=382.11')
replace(tag_helper, 'multiple-choice-answer-options.js?v=382.10', 'multiple-choice-answer-options.js?v=382.11')
replace(
    tag_helper,
    '            $"data-saved-question-type=\\"{(int)editor.Input.PresentationType}\\"></script>" +\n'
    '            "<script src=\\"/js/multiple-correct-answer-options.js?v=461.1\\"></script>" +\n'
    '            "<script>" +\n'
    '            "(()=>{" +\n'
    '            "const finish=()=>{" +\n'
    '            "window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver?.();" +',
    '            $"data-saved-question-type=\\"{(int)editor.Input.PresentationType}\\"></script>" +\n'
    '            "<script>window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver?.();</script>" +\n'
    '            "<script src=\\"/js/multiple-correct-answer-options.js?v=461.1\\"></script>" +\n'
    '            "<script>" +\n'
    '            "(()=>{" +\n'
    '            "const finish=()=>{" +'
)

# With scripts emitted after the form, install the temporary guard immediately;
# the following inline script restores native APIs right after controller setup.
replace(
    guard,
    '    if (document.readyState === "loading") {\n'
    '        // Capture-phase DOMContentLoaded runs before the controller\'s normal\n'
    '        // DOMContentLoaded listener. This keeps the monkey-patch scoped to the\n'
    '        // controller initialization instead of the whole page parse lifecycle.\n'
    '        nativeAddEventListener.call(\n'
    '            document,\n'
    '            "DOMContentLoaded",\n'
    '            installOverrides,\n'
    '            { capture: true, once: true });\n'
    '    } else {\n'
    '        installOverrides();\n'
    '    }',
    '    installOverrides();'
)

# Regression coverage for cold-cache/first-mount behavior and asset ordering.
replace(
    on_demand_test,
    '        Assert.Contains("badwolf:question-editor-on-demand-synced", editorScript, StringComparison.Ordinal);',
    '        Assert.Contains("badwolf:question-editor-on-demand-synced", editorScript, StringComparison.Ordinal);\n'
    '        Assert.Contains("data-on-demand-choice", editor, StringComparison.Ordinal);\n'
    '        Assert.Contains("Model.Input.AllPlayerMode ==", editor, StringComparison.Ordinal);\n'
    '        Assert.Contains("Model.Input.RevealAnswerOptionsOnDemand = isOnDemandChoice", editor, StringComparison.Ordinal);\n'
    '        Assert.Contains("questionEditorForm?.dataset.onDemandChoice === \\\"true\\\"", editor, StringComparison.Ordinal);\n'
    '        Assert.Contains("answerRewardModifierCheckbox.disabled = isOnDemandChoice", editor, StringComparison.Ordinal);\n'
    '        Assert.Contains("form.dataset.onDemandChoice = onDemand ? \\\"true\\\" : \\\"false\\\"", editorScript, StringComparison.Ordinal);\n'
    '        Assert.Contains("waitForEditorMount();", editorScript, StringComparison.Ordinal);'
)

replace(stability_test, 'Assert.Contains("capture: true, once: true", guard, StringComparison.Ordinal);', 'Assert.DoesNotContain("capture: true, once: true", guard, StringComparison.Ordinal);\n        Assert.Contains("    installOverrides();", guard, StringComparison.Ordinal);')
replace(stability_test, 'multiple-choice-answer-options-guard.js?v=382.10', 'multiple-choice-answer-options-guard.js?v=382.11')
replace(stability_test, 'multiple-choice-answer-options.js?v=382.10', 'multiple-choice-answer-options.js?v=382.11')
replace(
    stability_test,
    '        Assert.Contains(\n'
    '            "badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver",\n'
    '            tagHelper,\n'
    '            StringComparison.Ordinal);',
    '        Assert.Contains(\n'
    '            "badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver",\n'
    '            tagHelper,\n'
    '            StringComparison.Ordinal);\n'
    '        Assert.Contains("output.PostContent.AppendHtml", tagHelper, StringComparison.Ordinal);\n'
    '        Assert.DoesNotContain("output.PreContent.AppendHtml", tagHelper, StringComparison.Ordinal);'
)
