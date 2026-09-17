from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(relative_path: str, old: str, new: str) -> None:
    path = ROOT / relative_path
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"Expected exactly one match in {relative_path}, found {count}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


editor = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml"
editor_model = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs"
controller = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
tag_helper = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
on_demand_tests = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
stability_tests = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"

# Inline QuestionEditor logic: All-player multiple choice always keeps Button mode visible/enabled.
replace_once(
    editor,
    '''        function updateBuzzModeVisibility() {\n            const isWagerQuestion =\n                wagerQuestionCheckbox?.checked ?? false;\n            const isOnDemandChoice =\n                presentationTypeSelect?.value === "3" &&\n                (onDemandChoiceCheckbox?.checked === true ||\n                 questionEditorForm?.dataset.onDemandChoice === "true");\n            const hideBuzzMode = isWagerQuestion && !isOnDemandChoice;\n\n            if (buzzModeSetting) {\n                buzzModeSetting.hidden = hideBuzzMode;\n            }\n\n            if (buzzModeSelect) {\n                buzzModeSelect.disabled = hideBuzzMode;\n            }\n        }''',
    '''        function updateBuzzModeVisibility() {\n            const isWagerQuestion =\n                wagerQuestionCheckbox?.checked ?? false;\n            const isAllPlayerChoice =\n                presentationTypeSelect?.value === "3";\n            const hideBuzzMode = isWagerQuestion && !isAllPlayerChoice;\n\n            if (buzzModeSetting) {\n                buzzModeSetting.hidden = hideBuzzMode;\n            }\n\n            if (buzzModeSelect) {\n                buzzModeSelect.disabled = hideBuzzMode;\n                if (isAllPlayerChoice && !buzzModeSelect.value) {\n                    buzzModeSelect.value = "0";\n                }\n            }\n        }''')

# Multiple-choice controller: only all-player text hides Button mode. Choice type always shows it.
replace_once(controller, "        let previousAllPlayer = false;", "        let previousBuzzModeHidden = false;")
replace_once(
    controller,
    '''            const isAllPlayer = isText || (allPlayerChoice && !onDemand);''',
    '''            const hideBuzzModeForType = isText;''')
replace_once(
    controller,
    '''            if (isAllPlayer && !previousAllPlayer &&\n                buzzSelect instanceof HTMLSelectElement) {\n                standardBuzzMode = buzzSelect.value;\n            }\n            if (isAllPlayer) {\n                if (buzzSetting) {\n                    buzzSetting.hidden = true;\n                }\n                if (buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.disabled = false;\n                    buzzSelect.value = "5";\n                }\n            } else {\n                if (buzzSetting) {\n                    buzzSetting.hidden = false;\n                }\n                if (buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.disabled = false;\n                    if (previousAllPlayer) {\n                        buzzSelect.value = standardBuzzMode;\n                    }\n                }\n            }''',
    '''            if (hideBuzzModeForType && !previousBuzzModeHidden &&\n                buzzSelect instanceof HTMLSelectElement) {\n                standardBuzzMode = buzzSelect.value || "0";\n            }\n            if (hideBuzzModeForType) {\n                if (buzzSetting) {\n                    buzzSetting.hidden = true;\n                }\n                if (buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.disabled = false;\n                    buzzSelect.value = "5";\n                }\n            } else {\n                if (buzzSetting) {\n                    buzzSetting.hidden = false;\n                }\n                if (buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.disabled = false;\n                    if (allPlayerChoice && !buzzSelect.value) {\n                        buzzSelect.value = "0";\n                    } else if (previousBuzzModeHidden) {\n                        buzzSelect.value = standardBuzzMode || "0";\n                    }\n                }\n            }''')
replace_once(controller, "            previousAllPlayer = isAllPlayer;", "            previousBuzzModeHidden = hideBuzzModeForType;")

# Server model: a saved choice question always has a valid Button mode value.
replace_once(
    editor_model,
    '''            BuzzModeOverride = question.BuzzModeOverride,''',
    '''            BuzzModeOverride =\n                presentationType == QuestionPresentationType.AllPlayerMultipleChoice &&\n                !Enum.IsDefined(typeof(BuzzActivationMode), question.BuzzModeOverride)\n                    ? BuzzActivationMode.UseRoundDefault\n                    : question.BuzzModeOverride,''')

# Persist Button mode for All-player multiple choice instead of forcing Disabled.
replace_once(
    editor_model,
    '''        var isAllPlayer = Input.PresentationType is\n            QuestionPresentationType.AllPlayerText or\n            QuestionPresentationType.AllPlayerMultipleChoice;''',
    '''        var isAllPlayerMultipleChoice = Input.PresentationType is\n            QuestionPresentationType.AllPlayerMultipleChoice or\n            QuestionPresentationType.AllPlayerMultipleChoiceOnDemand;''')
replace_once(
    editor_model,
    '''        question.BuzzModeOverride = question.IsSpecial || isAllPlayer\n            ? BuzzActivationMode.Disabled\n            : Input.BuzzModeOverride;\n        question.BuzzDelaySeconds = question.IsSpecial || isAllPlayer\n            ? 0\n            : Math.Max(0, Input.BuzzDelaySeconds);''',
    '''        var disableBuzzMode =\n            Input.PresentationType == QuestionPresentationType.AllPlayerText ||\n            (question.IsSpecial && !isAllPlayerMultipleChoice);\n        question.BuzzModeOverride = disableBuzzMode\n            ? BuzzActivationMode.Disabled\n            : Input.BuzzModeOverride;\n        question.BuzzDelaySeconds = disableBuzzMode\n            ? 0\n            : Math.Max(0, Input.BuzzDelaySeconds);''')

# Cache-bust the corrected editor controller.
for asset in ("multiple-choice-answer-options-guard.js", "multiple-choice-answer-options.js"):
    replace_once(tag_helper, f"{asset}?v=382.12", f"{asset}?v=382.13")

# Regression expectations for Button mode visibility/value/persistence.
replace_once(
    on_demand_tests,
    '''        Assert.Contains("const hideBuzzMode = isWagerQuestion && !isOnDemandChoice", editor, StringComparison.Ordinal);''',
    '''        Assert.Contains("const isAllPlayerChoice =", editor, StringComparison.Ordinal);\n        Assert.Contains("const hideBuzzMode = isWagerQuestion && !isAllPlayerChoice", editor, StringComparison.Ordinal);\n        Assert.Contains("if (isAllPlayerChoice && !buzzModeSelect.value)", editor, StringComparison.Ordinal);''')
replace_once(
    on_demand_tests,
    '''        Assert.Contains("buzzSelect.disabled = false", editorScript, StringComparison.Ordinal);''',
    '''        Assert.Contains("buzzSelect.disabled = false", editorScript, StringComparison.Ordinal);\n        Assert.Contains("const hideBuzzModeForType = isText;", editorScript, StringComparison.Ordinal);\n        Assert.Contains("if (allPlayerChoice && !buzzSelect.value)", editorScript, StringComparison.Ordinal);\n        Assert.DoesNotContain("isText || (allPlayerChoice && !onDemand)", editorScript, StringComparison.Ordinal);\n        Assert.Contains("var isAllPlayerMultipleChoice = Input.PresentationType is", editorModel, StringComparison.Ordinal);\n        Assert.Contains("question.IsSpecial && !isAllPlayerMultipleChoice", editorModel, StringComparison.Ordinal);\n        Assert.Contains("Enum.IsDefined(typeof(BuzzActivationMode), question.BuzzModeOverride)", editorModel, StringComparison.Ordinal);''')

replace_once(stability_tests, "multiple-choice-answer-options-guard.js?v=382.12", "multiple-choice-answer-options-guard.js?v=382.13")
replace_once(stability_tests, "multiple-choice-answer-options.js?v=382.12", "multiple-choice-answer-options.js?v=382.13")

print("Applied all-player multiple-choice Button mode fix.")
