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
    "    .question-editor[data-on-demand-choice=\"true\"] .answer-reward-modifier-setting {\n"
    "        display: none !important;\n"
    "    }\n",
    ""
)
replace_once(
    editor,
    "        <label class=\"checkbox-row answer-reward-modifier-setting\"\n"
    "               hidden=\"@(isOnDemandChoice ? \"hidden\" : null)\">",
    "        <label class=\"checkbox-row answer-reward-modifier-setting\">"
)
replace_once(
    editor,
    "            if (answerRewardModifierSetting) {\n"
    "                answerRewardModifierSetting.hidden = isOnDemandChoice;\n"
    "            }\n"
    "            if (answerRewardModifierCheckbox) {\n"
    "                answerRewardModifierCheckbox.disabled = isOnDemandChoice;\n"
    "                if (isOnDemandChoice) {\n"
    "                    answerRewardModifierCheckbox.checked = false;\n"
    "                }\n"
    "            }\n",
    "            if (answerRewardModifierSetting) {\n"
    "                answerRewardModifierSetting.hidden = false;\n"
    "            }\n"
    "            if (answerRewardModifierCheckbox) {\n"
    "                answerRewardModifierCheckbox.disabled = false;\n"
    "            }\n"
)

script = "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js"
replace_once(
    script,
    "            if (answerRewardModifierSetting instanceof HTMLElement) {\n"
    "                answerRewardModifierSetting.hidden = onDemand;\n"
    "            }\n"
    "            if (onDemand &&\n"
    "                answerRewardModifierCheckbox instanceof HTMLInputElement) {\n"
    "                answerRewardModifierCheckbox.checked = false;\n"
    "            }\n",
    "            if (answerRewardModifierSetting instanceof HTMLElement) {\n"
    "                answerRewardModifierSetting.hidden = false;\n"
    "            }\n"
    "            if (answerRewardModifierCheckbox instanceof HTMLInputElement) {\n"
    "                answerRewardModifierCheckbox.disabled = false;\n"
    "            }\n"
)

model = "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs"
replace_once(
    model,
    "            AllowAnswerRewardModifiers =\n"
    "                !isAllPlayerMultipleChoiceOnDemand &&\n"
    "                question.AllowAnswerRewardModifiers,",
    "            AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers,"
)
replace_once(
    model,
    "            Input.ExcludeFromRandomWagerSelection = true;\n"
    "            Input.AllowAnswerRewardModifiers = false;\n",
    "            Input.ExcludeFromRandomWagerSelection = true;\n"
)
replace_once(
    model,
    "        question.AllowAnswerRewardModifiers =\n"
    "            !isAllPlayerMultipleChoiceOnDemand &&\n"
    "            Input.AllowAnswerRewardModifiers;",
    "        question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers;"
)

snapshot = "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs"
replace_once(
    snapshot,
    "        AllowAnswerRewardModifiers =\n"
    "            presentationType !=\n"
    "                QuestionPresentationType.AllPlayerMultipleChoiceOnDemand &&\n"
    "            allowAnswerRewardModifiers;",
    "        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;"
)

runtime = "src/BadWolfQuiz.Game/Runtime/GameBoard.cs"
replace_once(
    runtime,
    "        AllowAnswerRewardModifiers =\n"
    "            !RevealAnswerOptionsOnDemand && allowAnswerRewardModifiers;",
    "        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;"
)

assets = "src/BadWolfQuiz.Web/TagHelpers/MultipleChoiceAnswerOptionsAssetsTagHelper.cs"
p = Path(assets)
text = p.read_text(encoding="utf-8")
if text.count("382.14") != 2:
    raise SystemExit(f"Expected two 382.14 cache keys, found {text.count('382.14')}")
p.write_text(text.replace("382.14", "382.15"), encoding="utf-8")

stability = "tests/BadWolfQuiz.Web.Tests/MultipleChoiceEditorStabilityRegressionTests.cs"
p = Path(stability)
text = p.read_text(encoding="utf-8")
if text.count("382.14") != 2:
    raise SystemExit(f"Expected two 382.14 cache assertions, found {text.count('382.14')}")
p.write_text(text.replace("382.14", "382.15"), encoding="utf-8")

game_test = "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs"
replace_once(
    game_test,
    "    public void Snapshot_forces_reward_modifiers_off_for_on_demand_choice()\n"
    "    {\n"
    "        var question = CreateQuestion(allowAnswerRewardModifiers: true);\n\n"
    "        Assert.False(question.AllowAnswerRewardModifiers);\n"
    "    }",
    "    public void Snapshot_preserves_reward_modifiers_for_on_demand_choice()\n"
    "    {\n"
    "        var question = CreateQuestion(allowAnswerRewardModifiers: true);\n\n"
    "        Assert.True(question.AllowAnswerRewardModifiers);\n"
    "    }"
)

reward_test = "tests/BadWolfQuiz.Web.Tests/AnswerRewardButtonsRegressionTests.cs"
replace_once(
    reward_test,
    "        Assert.Contains(\n"
    "            \"AllowAnswerRewardModifiers =\\n                !isAllPlayerMultipleChoiceOnDemand &&\\n                question.AllowAnswerRewardModifiers\",\n"
    "            model);\n"
    "        Assert.Contains(\n"
    "            \"question.AllowAnswerRewardModifiers =\\n            !isAllPlayerMultipleChoiceOnDemand &&\\n            Input.AllowAnswerRewardModifiers\",\n"
    "            model);",
    "        Assert.Contains(\"AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers\", model);\n"
    "        Assert.Contains(\"question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers\", model);"
)

ondemand_test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
p = Path(ondemand_test)
text = p.read_text(encoding="utf-8")
replacements = {
    "        Assert.Contains(\"answerRewardModifierCheckbox.disabled = isOnDemandChoice\", editor, StringComparison.Ordinal);\n":
        "        Assert.Contains(\"answerRewardModifierCheckbox.disabled = false\", editor, StringComparison.Ordinal);\n",
    "        Assert.Contains(\"answerRewardModifierSetting.hidden = onDemand\", editorScript, StringComparison.Ordinal);\n":
        "        Assert.Contains(\"answerRewardModifierSetting.hidden = false\", editorScript, StringComparison.Ordinal);\n",
    "        Assert.Contains(\"answerRewardModifierCheckbox.checked = false\", editorScript, StringComparison.Ordinal);\n":
        "        Assert.DoesNotContain(\"answerRewardModifierCheckbox.checked = false\", editorScript, StringComparison.Ordinal);\n",
    "        Assert.Contains(\"hidden=\\\"@(isOnDemandChoice ? \\\"hidden\\\" : null)\\\"\", editor, StringComparison.Ordinal);\n":
        "        Assert.DoesNotContain(\"hidden=\\\"@(isOnDemandChoice ? \\\"hidden\\\" : null)\\\"\", editor, StringComparison.Ordinal);\n",
    "        Assert.Contains(\"Input.AllowAnswerRewardModifiers = false\", editorModel, StringComparison.Ordinal);\n":
        "        Assert.DoesNotContain(\"Input.AllowAnswerRewardModifiers = false\", editorModel, StringComparison.Ordinal);\n",
    "        Assert.Contains(\"!isAllPlayerMultipleChoiceOnDemand &&\", editorModel, StringComparison.Ordinal);\n":
        "        Assert.Contains(\"AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers\", editorModel, StringComparison.Ordinal);\n"
        "        Assert.Contains(\"question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers\", editorModel, StringComparison.Ordinal);\n"
}
for old, new in replacements.items():
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one on-demand test match, found {count}: {old!r}")
    text = text.replace(old, new)
p.write_text(text, encoding="utf-8")

print("Enabled reward modifier setting for on-demand all-player multiple-choice questions.")
