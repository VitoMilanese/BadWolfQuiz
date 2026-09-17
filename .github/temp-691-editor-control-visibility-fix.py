from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(relative_path: str, old: str, new: str) -> None:
    path = ROOT / relative_path
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one match in {relative_path}, found {count}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


editor = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml"
controller = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
tag_helper = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
on_demand_tests = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
stability_tests = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"

replace_once(
    editor,
    '''        <div id="wager-mode-setting"\n             hidden="@((Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard && !isOnDemandChoice) ? "hidden" : null)">''',
    '''        <div id="wager-mode-setting"\n             hidden="@(Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ? "hidden" : null)">''')

replace_once(
    editor,
    '''            const supportsWagerMode =\n                presentationTypeSelect?.value === "0" ||\n                isOnDemandChoice;''',
    '''            const supportsWagerMode =\n                presentationTypeSelect?.value === "0";''')

replace_once(
    editor,
    '''        function updateBuzzModeVisibility() {\n            const isWagerQuestion =\n                wagerQuestionCheckbox?.checked ?? false;\n\n            if (buzzModeSetting) {\n                buzzModeSetting.hidden = isWagerQuestion;\n            }\n\n            if (buzzModeSelect) {\n                buzzModeSelect.disabled = isWagerQuestion;\n            }\n        }''',
    '''        function updateBuzzModeVisibility() {\n            const isWagerQuestion =\n                wagerQuestionCheckbox?.checked ?? false;\n            const isOnDemandChoice =\n                presentationTypeSelect?.value === "3" &&\n                (onDemandChoiceCheckbox?.checked === true ||\n                 questionEditorForm?.dataset.onDemandChoice === "true");\n            const hideBuzzMode = isWagerQuestion && !isOnDemandChoice;\n\n            if (buzzModeSetting) {\n                buzzModeSetting.hidden = hideBuzzMode;\n            }\n\n            if (buzzModeSelect) {\n                buzzModeSelect.disabled = hideBuzzMode;\n            }\n        }''')

replace_once(
    controller,
    '''            const supportsWagerMode = type === "0" || onDemand;''',
    '''            const supportsWagerMode = type === "0";''')

replace_once(tag_helper, "382.11", "382.12")
replace_once(tag_helper, "382.11", "382.12")

replace_once(
    on_demand_tests,
    '''        Assert.Contains("const supportsWagerMode = type === \\"0\\" || onDemand", editorScript, StringComparison.Ordinal);''',
    '''        Assert.Contains("const supportsWagerMode = type === \\"0\\";", editorScript, StringComparison.Ordinal);\n        Assert.DoesNotContain("type === \\"0\\" || onDemand", editorScript, StringComparison.Ordinal);\n        Assert.Contains("const hideBuzzMode = isWagerQuestion && !isOnDemandChoice", editor, StringComparison.Ordinal);\n        Assert.Contains("Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ? \\"hidden\\" : null", editor, StringComparison.Ordinal);''')

replace_once(stability_tests, "multiple-choice-answer-options-guard.js?v=382.11", "multiple-choice-answer-options-guard.js?v=382.12")
replace_once(stability_tests, "multiple-choice-answer-options.js?v=382.11", "multiple-choice-answer-options.js?v=382.12")

print("Applied editor control visibility fix.")
