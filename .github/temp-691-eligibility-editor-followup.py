from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace(path: str, old: str, new: str, count: int = 1) -> None:
    target = ROOT / path
    text = target.read_text(encoding="utf-8")
    actual = text.count(old)
    if actual != count:
        raise SystemExit(f"{path}: expected {count} matches, found {actual}: {old[:100]!r}")
    target.write_text(text.replace(old, new, count), encoding="utf-8")


# Only players whose buzzer answer was actually judged incorrect are excluded
# from the later all-player choice phase. An unresolved current claimant remains
# eligible after the host switches to answer options.
replace(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    """        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;\n""",
    """        AllowAnswerRewardModifiers =\n            !RevealAnswerOptionsOnDemand && allowAnswerRewardModifiers;\n""",
)
replace(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    """        _allPlayerChoiceExcludedPlayerIds.Clear();\n        _allPlayerChoiceExcludedPlayerIds.AddRange(\n            _answerAttempts\n                .Select(attempt => attempt.PlayerId)\n                .Distinct());\n        if (AnsweringPlayerId is { } answeringPlayerId &&\n            !_allPlayerChoiceExcludedPlayerIds.Contains(answeringPlayerId))\n        {\n            _allPlayerChoiceExcludedPlayerIds.Add(answeringPlayerId);\n        }\n\n        AreAllPlayerChoiceOptionsRevealed = true;\n""",
    """        _allPlayerChoiceExcludedPlayerIds.Clear();\n        _allPlayerChoiceExcludedPlayerIds.AddRange(\n            _answerAttempts\n                .Where(attempt => !attempt.IsCorrect)\n                .Select(attempt => attempt.PlayerId)\n                .Distinct());\n\n        AreAllPlayerChoiceOptionsRevealed = true;\n""",
)

# The authored/runtime snapshot also treats x2 / 1/2 as incompatible with the
# on-demand hybrid mode, including old persisted rows that still have the flag.
replace(
    "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs",
    """        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;\n""",
    """        AllowAnswerRewardModifiers =\n            presentationType !=\n                QuestionPresentationType.AllPlayerMultipleChoiceOnDemand &&\n            allowAnswerRewardModifiers;\n""",
)

# Normalize incompatible saved editor state on GET and POST, not only in JS.
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    """        var wagerMode = QuestionWagerModes.GetMode(storedPresentationType);\n        var presentationType =\n            AllPlayerQuestionCompatibility.GetContentPresentationType(\n                storedPresentationType);\n\n        Input = new InputModel\n""",
    """        var wagerMode = QuestionWagerModes.GetMode(storedPresentationType);\n        var presentationType =\n            AllPlayerQuestionCompatibility.GetContentPresentationType(\n                storedPresentationType);\n        var isAllPlayerMultipleChoiceOnDemand =\n            storedPresentationType ==\n                QuestionPresentationType.AllPlayerMultipleChoiceOnDemand;\n\n        Input = new InputModel\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    """            IsSpecial = question.IsSpecial,\n            WagerMode = wagerMode,\n""",
    """            IsSpecial =\n                !isAllPlayerMultipleChoiceOnDemand && question.IsSpecial,\n            WagerMode = isAllPlayerMultipleChoiceOnDemand\n                ? QuestionWagerMode.Normal\n                : wagerMode,\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    """            RevealAnswerOptionsOnDemand = storedPresentationType ==\n                QuestionPresentationType.AllPlayerMultipleChoiceOnDemand,\n            ExcludeFromRandomWagerSelection =\n                question.ExcludeFromRandomWagerSelection,\n            AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers,\n""",
    """            RevealAnswerOptionsOnDemand = isAllPlayerMultipleChoiceOnDemand,\n            ExcludeFromRandomWagerSelection =\n                question.ExcludeFromRandomWagerSelection,\n            AllowAnswerRewardModifiers =\n                !isAllPlayerMultipleChoiceOnDemand &&\n                question.AllowAnswerRewardModifiers,\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    """        if (isAllPlayerMultipleChoiceOnDemand)\n        {\n            Input.IsSpecial = false;\n            Input.ExcludeFromRandomWagerSelection = true;\n        }\n""",
    """        if (isAllPlayerMultipleChoiceOnDemand)\n        {\n            Input.IsSpecial = false;\n            Input.WagerMode = QuestionWagerMode.Normal;\n            Input.ExcludeFromRandomWagerSelection = true;\n            Input.AllowAnswerRewardModifiers = false;\n        }\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    """        question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers;\n""",
    """        question.AllowAnswerRewardModifiers =\n            !isAllPlayerMultipleChoiceOnDemand &&\n            Input.AllowAnswerRewardModifiers;\n""",
)

# Keep the lower editor controls synchronized with the persisted on-demand flag.
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml",
    """        const wagerModeSelect =\n            document.getElementById(\"Input_WagerMode\");\n\n        function updateQuestionTypeUi() {\n            const isFourClues = presentationTypeSelect?.value === \"1\";\n""",
    """        const wagerModeSelect =\n            document.getElementById(\"Input_WagerMode\");\n\n        const onDemandChoiceCheckbox =\n            document.querySelector(\"[data-all-player-choice-on-demand]\");\n\n        const answerRewardModifierSetting =\n            document.querySelector(\".answer-reward-modifier-setting\");\n\n        const answerRewardModifierCheckbox =\n            document.getElementById(\"Input_AllowAnswerRewardModifiers\");\n\n        function updateQuestionTypeUi() {\n            const isFourClues = presentationTypeSelect?.value === \"1\";\n            const isOnDemandChoice =\n                presentationTypeSelect?.value === \"3\" &&\n                onDemandChoiceCheckbox?.checked === true;\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml",
    """            if (!supportsWagerMode && wagerModeSelect) {\n                wagerModeSelect.value = \"0\";\n            }\n\n            updateBuzzModeVisibility();\n""",
    """            if (wagerModeSelect) {\n                wagerModeSelect.disabled = false;\n                if (!supportsWagerMode) {\n                    wagerModeSelect.value = \"0\";\n                }\n            }\n\n            if (answerRewardModifierSetting) {\n                answerRewardModifierSetting.hidden = isOnDemandChoice;\n            }\n            if (isOnDemandChoice && answerRewardModifierCheckbox) {\n                answerRewardModifierCheckbox.checked = false;\n            }\n\n            updateBuzzModeVisibility();\n""",
)
replace(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml",
    """        wagerQuestionCheckbox?.addEventListener(\n            \"change\",\n            updateBuzzModeVisibility);\n\n        presentationTypeSelect?.addEventListener(\"change\", async () => {\n""",
    """        wagerQuestionCheckbox?.addEventListener(\n            \"change\",\n            updateBuzzModeVisibility);\n\n        onDemandChoiceCheckbox?.addEventListener(\n            \"change\",\n            updateQuestionTypeUi);\n\n        document.addEventListener(\n            \"badwolf:question-editor-on-demand-synced\",\n            updateQuestionTypeUi);\n\n        presentationTypeSelect?.addEventListener(\"change\", async () => {\n""",
)

# If stale persisted IsSpecial state disabled the buzzer-mode select during the
# page's first pass, explicitly re-enable it when on-demand mode is active. Also
# notify the page-level settings controller after restoring the saved checkbox.
replace(
    "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js",
    """        if (onDemandCheckbox instanceof HTMLInputElement &&\n            modeInput instanceof HTMLInputElement) {\n            onDemandCheckbox.checked =\n                modeInput.value === \"multipleChoiceOnDemand\";\n        }\n        const saveStatus = document.querySelector(\"[data-question-save-status]\");\n""",
    """        if (onDemandCheckbox instanceof HTMLInputElement &&\n            modeInput instanceof HTMLInputElement) {\n            onDemandCheckbox.checked =\n                modeInput.value === \"multipleChoiceOnDemand\";\n            document.dispatchEvent(new CustomEvent(\n                \"badwolf:question-editor-on-demand-synced\"));\n        }\n        const saveStatus = document.querySelector(\"[data-question-save-status]\");\n""",
)
replace(
    "src/BadWolfQuiz.Web/wwwroot/js/multiple-choice-answer-options.js",
    """            } else {\n                if (buzzSetting) {\n                    buzzSetting.hidden = false;\n                }\n                if (previousAllPlayer &&\n                    buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.value = standardBuzzMode;\n                }\n            }\n""",
    """            } else {\n                if (buzzSetting) {\n                    buzzSetting.hidden = false;\n                }\n                if (buzzSelect instanceof HTMLSelectElement) {\n                    buzzSelect.disabled = false;\n                    if (previousAllPlayer) {\n                        buzzSelect.value = standardBuzzMode;\n                    }\n                }\n            }\n""",
)

# Update game-level regression expectations.
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    """    public void Current_buzzer_claim_does_not_block_reveal_and_claimant_is_excluded()\n""",
    """    public void Current_buzzer_claim_does_not_block_reveal_and_claimant_remains_eligible()\n""",
)
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    """        Assert.Contains(rose.Id, question.AllPlayerChoiceExcludedPlayerIds);\n        Assert.Null(question.AnsweringPlayerId);\n""",
    """        Assert.DoesNotContain(rose.Id, question.AllPlayerChoiceExcludedPlayerIds);\n        Assert.Null(question.AnsweringPlayerId);\n""",
    count=1,
)
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    """        session.ClaimQuestionBuzzer(100, rose.Id);\n        session.RevealAllPlayerChoiceOptions(100);\n\n        var restored = GameSession.Restore(\n""",
    """        session.ClaimQuestionBuzzer(100, rose.Id);\n        session.JudgeQuestionAnswer(100, rose.Id, false);\n        session.RevealAllPlayerChoiceOptions(100);\n\n        var restored = GameSession.Restore(\n""",
)
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    """    private static QuizQuestionSnapshot CreateQuestion(bool isSpecial = false) => new(\n""",
    """    private static QuizQuestionSnapshot CreateQuestion(\n        bool isSpecial = false,\n        bool allowAnswerRewardModifiers = false) => new(\n""",
)
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    """        answerBlocks: [TextBlock(10, \"A\"), TextBlock(11, \"B\")],\n        presentationType: QuestionPresentationType.AllPlayerMultipleChoiceOnDemand);\n""",
    """        answerBlocks: [TextBlock(10, \"A\"), TextBlock(11, \"B\")],\n        presentationType: QuestionPresentationType.AllPlayerMultipleChoiceOnDemand,\n        allowAnswerRewardModifiers: allowAnswerRewardModifiers);\n""",
)
insert_after = """    [Fact]\n    public void Snapshot_forces_on_demand_choice_out_of_every_wager_mode()\n    {\n        var question = CreateQuestion(isSpecial: true);\n\n        Assert.Equal(\n            QuestionPresentationType.AllPlayerMultipleChoiceOnDemand,\n            question.PresentationType);\n        Assert.False(question.IsSpecial);\n        Assert.True(question.ExcludeFromRandomWagerSelection);\n        Assert.False(question.IsEligibleForRandomWagerSelection);\n        Assert.False(question.IsEligibleForRandomAnonymousSharedWagerSelection);\n    }\n"""
replacement = insert_after + """\n    [Fact]\n    public void Snapshot_forces_reward_modifiers_off_for_on_demand_choice()\n    {\n        var question = CreateQuestion(allowAnswerRewardModifiers: true);\n\n        Assert.False(question.AllowAnswerRewardModifiers);\n    }\n"""
replace(
    "tests/BadWolfQuiz.Game.Tests/OnDemandAllPlayerMultipleChoiceTests.cs",
    insert_after,
    replacement,
)

# Extend source-level web regressions for the editor/runtime contract.
replace(
    "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs",
    """        Assert.Contains(\"specialCheckbox.checked = false\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"excludeCheckbox.checked = true\", editorScript, StringComparison.Ordinal);\n""",
    """        Assert.Contains(\"specialCheckbox.checked = false\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"excludeCheckbox.checked = true\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"buzzSelect.disabled = false\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"badwolf:question-editor-on-demand-synced\", editorScript, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierSetting.hidden = isOnDemandChoice\", editor, StringComparison.Ordinal);\n        Assert.Contains(\"answerRewardModifierCheckbox.checked = false\", editor, StringComparison.Ordinal);\n        Assert.Contains(\"Input.AllowAnswerRewardModifiers = false\", editorModel, StringComparison.Ordinal);\n        Assert.Contains(\"!isAllPlayerMultipleChoiceOnDemand &&\", editorModel, StringComparison.Ordinal);\n""",
)
replace(
    "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs",
    """        Assert.Contains(\"AllPlayerChoiceExcludedPlayerIds\", runtime, StringComparison.Ordinal);\n        Assert.DoesNotContain(\"AllPlayerChoiceRevealLocked\", runtime, StringComparison.Ordinal);\n""",
    """        Assert.Contains(\"AllPlayerChoiceExcludedPlayerIds\", runtime, StringComparison.Ordinal);\n        Assert.Contains(\".Where(attempt => !attempt.IsCorrect)\", runtime, StringComparison.Ordinal);\n        Assert.DoesNotContain(\"_allPlayerChoiceExcludedPlayerIds.Add(answeringPlayerId)\", runtime, StringComparison.Ordinal);\n        Assert.DoesNotContain(\"AllPlayerChoiceRevealLocked\", runtime, StringComparison.Ordinal);\n""",
)

# The existing reward-button regression used exact one-line assignments. Keep it
# checking persistence, but account for the intentional on-demand guard.
replace(
    "tests/BadWolfQuiz.Web.Tests/AnswerRewardButtonsRegressionTests.cs",
    """        Assert.Contains(\"AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers\", model);\n        Assert.Contains(\"question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers\", model);\n""",
    """        Assert.Contains(\n            \"AllowAnswerRewardModifiers =\\n                !isAllPlayerMultipleChoiceOnDemand &&\\n                question.AllowAnswerRewardModifiers\",\n            model);\n        Assert.Contains(\n            \"question.AllowAnswerRewardModifiers =\\n            !isAllPlayerMultipleChoiceOnDemand &&\\n            Input.AllowAnswerRewardModifiers\",\n            model);\n""",
)
