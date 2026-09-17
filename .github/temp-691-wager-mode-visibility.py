from pathlib import Path


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one match in {path}, found {count}: {old!r}")
    p.write_text(text.replace(old, new), encoding="utf-8")


editor = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml"
replace_once(
    editor,
    "    Model.Input.RevealAnswerOptionsOnDemand = isOnDemandChoice;\n}",
    "    Model.Input.RevealAnswerOptionsOnDemand = isOnDemandChoice;\n"
    "    var showWagerMode =\n"
    "        Model.Input.PresentationType ==\n"
    "            BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ||\n"
    "        (Model.Input.PresentationType ==\n"
    "            BadWolfQuiz.Game.Definitions.QuestionPresentationType.AllPlayerMultipleChoice &&\n"
    "         !isOnDemandChoice);\n"
    "}"
)
replace_once(
    editor,
    "        <div id=\"wager-mode-setting\"\n"
    "             hidden=\"@(Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ? \"hidden\" : null)\">",
    "        <div id=\"wager-mode-setting\"\n"
    "             hidden=\"@(showWagerMode ? null : \"hidden\")\">"
)
replace_once(
    editor,
    "            const supportsWagerMode =\n"
    "                presentationTypeSelect?.value === \"0\";",
    "            const supportsWagerMode =\n"
    "                presentationTypeSelect?.value === \"0\" ||\n"
    "                (presentationTypeSelect?.value === \"3\" && !isOnDemandChoice);"
)

script = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
replace_once(
    script,
    "            const supportsWagerMode = type === \"0\";",
    "            const supportsWagerMode =\n"
    "                type === \"0\" || (allPlayerChoice && !onDemand);"
)

assets = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
p = Path(assets)
text = p.read_text(encoding="utf-8")
if text.count("382.13") != 2:
    raise SystemExit(f"Expected two 382.13 cache keys, found {text.count('382.13')}")
p.write_text(text.replace("382.13", "382.14"), encoding="utf-8")

test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
replace_once(
    test,
    "        Assert.Contains(\"const supportsWagerMode = type === \\\"0\\\";\", editorScript, StringComparison.Ordinal);",
    "        Assert.Contains(\"type === \\\"0\\\" || (allPlayerChoice && !onDemand)\", editorScript, StringComparison.Ordinal);"
)
replace_once(
    test,
    "        Assert.Contains(\"Model.Input.PresentationType != BadWolfQuiz.Game.Definitions.QuestionPresentationType.Standard ? \\\"hidden\\\" : null\", editor, StringComparison.Ordinal);",
    "        Assert.Contains(\"hidden=\\\"@(showWagerMode ? null : \\\"hidden\\\")\\\"\", editor, StringComparison.Ordinal);\n"
    "        Assert.Contains(\"presentationTypeSelect?.value === \\\"3\\\" && !isOnDemandChoice\", editor, StringComparison.Ordinal);"
)

stability_test = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"
p = Path(stability_test)
text = p.read_text(encoding="utf-8")
if text.count("382.13") != 2:
    raise SystemExit(f"Expected two 382.13 cache assertions, found {text.count('382.13')}")
p.write_text(text.replace("382.13", "382.14"), encoding="utf-8")

print("Applied wager mode visibility fix for normal all-player multiple-choice questions.")
