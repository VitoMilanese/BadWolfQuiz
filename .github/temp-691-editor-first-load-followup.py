from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace(path: str, old: str, new: str, count: int = 1) -> None:
    target = ROOT / path
    text = target.read_text(encoding="utf-8")
    actual = text.count(old)
    if actual != count:
        raise SystemExit(
            f"{path}: expected {count} matches, found {actual}: {old[:120]!r}")
    target.write_text(text.replace(old, new, count), encoding="utf-8")


# The editor assets can be mounted after DOMContentLoaded. Restore the temporary
# guard in both full-page and already-loaded document paths, and bust the browser
# cache for the corrected controller.
tag_helper = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
replace(tag_helper, "382.9", "382.10", count=2)
replace(
    tag_helper,
    """            \"<script>\" +\n            \"document.addEventListener('DOMContentLoaded',()=>{\" +\n            \"window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver?.();\" +\n            \"window.setTimeout(()=>{\" +\n            \"const s=document.querySelector('[data-question-save-status]');\" +\n            \"if(!s)return;\" +\n            \"const p={h:s.hidden,t:s.textContent,c:s.className,d:s.style.display};\" +\n            \"s.style.display='none';s.classList.add('alert-success');\" +\n            \"s.classList.remove('alert-error');s.textContent='editor-state-synchronized';\" +\n            \"s.hidden=false;window.setTimeout(()=>{\" +\n            \"s.hidden=p.h;s.textContent=p.t;s.className=p.c;s.style.display=p.d;\" +\n            \"},0);\" +\n            \"},0);\" +\n            \"},{once:true});\" +\n            \"</script>\");\n""",
    """            \"<script>\" +\n            \"(()=>{\" +\n            \"const finish=()=>{\" +\n            \"window.badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver?.();\" +\n            \"window.setTimeout(()=>{\" +\n            \"const s=document.querySelector('[data-question-save-status]');\" +\n            \"if(!s)return;\" +\n            \"const p={h:s.hidden,t:s.textContent,c:s.className,d:s.style.display};\" +\n            \"s.style.display='none';s.classList.add('alert-success');\" +\n            \"s.classList.remove('alert-error');s.textContent='editor-state-synchronized';\" +\n            \"s.hidden=false;window.setTimeout(()=>{\" +\n            \"s.hidden=p.h;s.textContent=p.t;s.className=p.c;s.style.display=p.d;\" +\n            \"},0);\" +\n            \"},0);\" +\n            \"};\" +\n            \"if(document.readyState==='loading'){\" +\n            \"document.addEventListener('DOMContentLoaded',finish,{once:true});\" +\n            \"}else{window.setTimeout(finish,0);}\" +\n            \"})();\" +\n            \"</script>\");\n""",
)

controller = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
replace(
    controller,
    """    // This controller owns only the Question Editor integration. The existing\n    // all-player and host-selected scripts still own their gameplay clients.\n    form.dataset.allPlayerEditorInitialized = \"true\";\n    window.badWolfHostMultipleChoiceInitialized = true;\n\n    if (window.badWolfMultipleChoiceAnswerOptionsEditorLoaded) {\n        return;\n    }\n    window.badWolfMultipleChoiceAnswerOptionsEditorLoaded = true;\n""",
    """    // This controller owns only the Question Editor integration. The existing\n    // all-player and host-selected scripts still own their gameplay clients.\n    // Scope initialization to the concrete editor form so a later editor mount\n    // in the same browser window is initialized without requiring a hard refresh.\n    if (form.dataset.multipleChoiceAnswerOptionsController) {\n        return;\n    }\n    form.dataset.multipleChoiceAnswerOptionsController = \"pending\";\n    form.dataset.allPlayerEditorInitialized = \"true\";\n    window.badWolfHostMultipleChoiceInitialized = true;\n""",
)
replace(
    controller,
    """    const style = document.createElement(\"style\");\n    style.id = \"multiple-choice-answer-options-editor-styles\";\n""",
    """    if (!document.getElementById(\"multiple-choice-answer-options-editor-styles\")) {\n        const style = document.createElement(\"style\");\n        style.id = \"multiple-choice-answer-options-editor-styles\";\n""",
)
replace(
    controller,
    """    document.head.appendChild(style);\n\n    const isChoiceType = value => value === \"3\" || value === \"4\";\n""",
    """        document.head.appendChild(style);\n    }\n\n    const isChoiceType = value => value === \"3\" || value === \"4\";\n""",
)
replace(
    controller,
    """        const select = document.getElementById(\"Input_PresentationType\");\n        const modeInput = document.getElementById(\"Input_AllPlayerMode\");\n        const questionSection = document.getElementById(\"question-blocks\");\n        const answerSection = document.getElementById(\"answer-blocks\");\n""",
    """        const select = form.querySelector(\"#Input_PresentationType\");\n        const modeInput = form.querySelector(\"#Input_AllPlayerMode\");\n        const questionSection = form.querySelector(\"#question-blocks\");\n        const answerSection = form.querySelector(\"#answer-blocks\");\n""",
)
replace(
    controller,
    """        const buzzSetting = document.getElementById(\"buzz-mode-setting\");\n        const buzzSelect = document.getElementById(\"Input_BuzzModeOverride\");\n        const specialCheckbox = document.getElementById(\"Input_IsSpecial\");\n        const excludeCheckbox = document.getElementById(\n            \"Input_ExcludeFromRandomWagerSelection\");\n        const onDemandSetting = document.querySelector(\n            \"[data-all-player-choice-on-demand-setting]\");\n        const onDemandCheckbox = document.querySelector(\n            \"[data-all-player-choice-on-demand]\");\n""",
    """        const buzzSetting = form.querySelector(\"#buzz-mode-setting\");\n        const buzzSelect = form.querySelector(\"#Input_BuzzModeOverride\");\n        const specialCheckbox = form.querySelector(\"#Input_IsSpecial\");\n        const excludeCheckbox = form.querySelector(\n            \"#Input_ExcludeFromRandomWagerSelection\");\n        const wagerModeSetting = form.querySelector(\"#wager-mode-setting\");\n        const wagerModeSelect = form.querySelector(\"#Input_WagerMode\");\n        const answerRewardModifierSetting = form.querySelector(\n            \".answer-reward-modifier-setting\");\n        const answerRewardModifierCheckbox = form.querySelector(\n            \"#Input_AllowAnswerRewardModifiers\");\n        const onDemandSetting = form.querySelector(\n            \"[data-all-player-choice-on-demand-setting]\");\n        const onDemandCheckbox = form.querySelector(\n            \"[data-all-player-choice-on-demand]\");\n""",
)
replace(
    controller,
    """        const saveStatus = document.querySelector(\"[data-question-save-status]\");\n""",
    """        const saveStatus = form.querySelector(\"[data-question-save-status]\");\n""",
)
replace(
    controller,
    """            if ((hostChoice || onDemand) &&\n                excludeCheckbox instanceof HTMLInputElement) {\n                excludeCheckbox.checked = true;\n            }\n\n            if (answerHeading instanceof HTMLHeadingElement) {\n""",
    """            if ((hostChoice || onDemand) &&\n                excludeCheckbox instanceof HTMLInputElement) {\n                excludeCheckbox.checked = true;\n            }\n\n            const supportsWagerMode = type === \"0\" || onDemand;\n            if (wagerModeSetting instanceof HTMLElement) {\n                wagerModeSetting.hidden = !supportsWagerMode;\n            }\n            if (wagerModeSelect instanceof HTMLSelectElement) {\n                wagerModeSelect.disabled = false;\n                if (!supportsWagerMode) {\n                    wagerModeSelect.value = \"0\";\n                }\n            }\n\n            if (answerRewardModifierSetting instanceof HTMLElement) {\n                answerRewardModifierSetting.hidden = onDemand;\n            }\n            if (onDemand &&\n                answerRewardModifierCheckbox instanceof HTMLInputElement) {\n                answerRewardModifierCheckbox.checked = false;\n            }\n\n            if (answerHeading instanceof HTMLHeadingElement) {\n""",
)
replace(
    controller,
    """    if (document.readyState === \"loading\") {\n        document.addEventListener(\"DOMContentLoaded\", initialize, { once: true });\n    } else {\n        initialize();\n    }\n})();\n""",
    """    const start = () => {\n        if (form.dataset.multipleChoiceAnswerOptionsController === \"initialized\") {\n            return true;\n        }\n\n        const select = form.querySelector(\"#Input_PresentationType\");\n        const answerSection = form.querySelector(\"#answer-blocks\");\n        if (!(select instanceof HTMLSelectElement) ||\n            !(answerSection instanceof HTMLElement)) {\n            return false;\n        }\n\n        form.dataset.multipleChoiceAnswerOptionsController = \"initialized\";\n        initialize();\n        return true;\n    };\n\n    const waitForEditorMount = () => {\n        if (start()) {\n            return;\n        }\n\n        const mountObserver = new MutationObserver(() => {\n            if (start()) {\n                mountObserver.disconnect();\n            }\n        });\n        mountObserver.observe(form, { childList: true, subtree: true });\n        window.setTimeout(() => {\n            if (start()) {\n                mountObserver.disconnect();\n            }\n        }, 0);\n    };\n\n    if (document.readyState === \"loading\") {\n        document.addEventListener(\n            \"DOMContentLoaded\",\n            waitForEditorMount,\n            { once: true });\n    } else {\n        waitForEditorMount();\n    }\n})();\n""",
)

# Keep the inline page controller consistent with the external controller and
# render the x2/1/2 row hidden from the first paint of an already-saved on-demand
# question.
editor = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml"
replace(
    editor,
    """        <label class=\"checkbox-row answer-reward-modifier-setting\">\n""",
    """        <label class=\"checkbox-row answer-reward-modifier-setting\"\n               hidden=\"@(Model.Input.RevealAnswerOptionsOnDemand ? \"hidden\" : null)\">\n""",
)
replace(
    editor,
    """            const supportsWagerMode =\n                presentationTypeSelect?.value === \"0\";\n""",
    """            const supportsWagerMode =\n                presentationTypeSelect?.value === \"0\" ||\n                isOnDemandChoice;\n""",
)

# Regression coverage: the robust external controller must own first-load state,
# and the guard cleanup must not depend on a future DOMContentLoaded event.
on_demand_test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
replace(
    on_demand_test,
    """        Assert.Contains(\"badwolf:question-editor-on-demand-synced\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierSetting.hidden = isOnDemandChoice\", editor, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierCheckbox.checked = false\", editor, StringComparison.Ordinal);\n""",
    """        Assert.Contains(\"badwolf:question-editor-on-demand-synced\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"form.dataset.multipleChoiceAnswerOptionsController\", editorScript, StringComparison.Ordinal);\n        Assert.DoesNotContain(\"window.badWolfMultipleChoiceAnswerOptionsEditorLoaded\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"const supportsWagerMode = type === \\\"0\\\" || onDemand\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"wagerModeSetting.hidden = !supportsWagerMode\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierSetting.hidden = onDemand\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierCheckbox.checked = false\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"hidden=\\\"@(Model.Input.RevealAnswerOptionsOnDemand ? \\\"hidden\\\" : null)\\\"\", editor, StringComparison.Ordinal);\n        Assert.Contains(\"isOnDemandChoice;\", editor, StringComparison.Ordinal);\n""",
)

stability_test = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"
replace(stability_test, "382.9", "382.10", count=2)
replace(
    stability_test,
    """        Assert.Contains(\n            \"badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver\",\n            tagHelper,\n            StringComparison.Ordinal);\n""",
    """        Assert.Contains(\n            \"badWolfMultipleChoiceAnswerOptionsRestoreMutationObserver\",\n            tagHelper,\n            StringComparison.Ordinal);\n        Assert.Contains(\"document.readyState==='loading'\", tagHelper, StringComparison.Ordinal);\n        Assert.Contains(\"window.setTimeout(finish,0)\", tagHelper, StringComparison.Ordinal);\n""",
)
