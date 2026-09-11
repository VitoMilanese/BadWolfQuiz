from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def write(path, text):
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(path, old, new):
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one occurrence, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new, 1))


def replace_exact(path, old, new, expected):
    text = read(path)
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} occurrences, found {count}: {old[:120]!r}")
    write(path, text.replace(old, new))


# Persisted question model. A bool defaults to false for new/legacy questions.
replace_once(
    "src/BadWolfQuiz.Web/Models/QuizModels.cs",
    "    public bool ExcludeFromRandomWagerSelection { get; set; }\n\n    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;",
    "    public bool ExcludeFromRandomWagerSelection { get; set; }\n\n    public bool AllowAnswerRewardModifiers { get; set; }\n\n    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;")

# Immutable quiz snapshot, including backwards-compatible default false.
replace_once(
    "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs",
    "        QuestionPresentationType presentationType = QuestionPresentationType.Standard,\n        QuestionBuzzerMode buzzerMode = QuestionBuzzerMode.UseGameSetting,\n        int buzzDelaySeconds = 0)",
    "        QuestionPresentationType presentationType = QuestionPresentationType.Standard,\n        QuestionBuzzerMode buzzerMode = QuestionBuzzerMode.UseGameSetting,\n        int buzzDelaySeconds = 0,\n        bool allowAnswerRewardModifiers = false)")
replace_once(
    "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs",
    "        ExcludeFromRandomWagerSelection =\n            presentationType == QuestionPresentationType.HostMultipleChoice ||\n            excludeFromRandomWagerSelection;\n        CategoryTitle = string.IsNullOrWhiteSpace(categoryTitle)",
    "        ExcludeFromRandomWagerSelection =\n            presentationType == QuestionPresentationType.HostMultipleChoice ||\n            excludeFromRandomWagerSelection;\n        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;\n        CategoryTitle = string.IsNullOrWhiteSpace(categoryTitle)")
replace_once(
    "src/BadWolfQuiz.Game/Definitions/QuizSnapshot.cs",
    "    public bool ExcludeFromRandomWagerSelection { get; }\n\n    public bool IsEligibleForRandomWagerSelection =>",
    "    public bool ExcludeFromRandomWagerSelection { get; }\n\n    public bool AllowAnswerRewardModifiers { get; }\n\n    public bool IsEligibleForRandomWagerSelection =>")

# Runtime question receives the authored permission and enforces it server-side.
replace_once(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    "                presentationType,\n                question.QuestionBlocks,\n                question.AnswerBlocks);",
    "                presentationType,\n                question.QuestionBlocks,\n                question.AnswerBlocks,\n                question.AllowAnswerRewardModifiers);")
replace_once(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    "        QuestionPresentationType presentationType,\n        IReadOnlyList<ContentBlockSnapshot> questionBlocks,\n        IReadOnlyList<ContentBlockSnapshot> answerBlocks)",
    "        QuestionPresentationType presentationType,\n        IReadOnlyList<ContentBlockSnapshot> questionBlocks,\n        IReadOnlyList<ContentBlockSnapshot> answerBlocks,\n        bool allowAnswerRewardModifiers = false)")
replace_once(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    "        IsSpecial = isSpecial;\n        PresentationType = presentationType;\n        RevealedClueCount =",
    "        IsSpecial = isSpecial;\n        PresentationType = presentationType;\n        AllowAnswerRewardModifiers = allowAnswerRewardModifiers;\n        RevealedClueCount =")
replace_once(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    "    public QuestionPresentationType PresentationType { get; }\n\n    public bool IsAllPlayerQuestion =>",
    "    public QuestionPresentationType PresentationType { get; }\n\n    public bool AllowAnswerRewardModifiers { get; }\n\n    public bool IsAllPlayerQuestion =>")
replace_once(
    "src/BadWolfQuiz.Game/Runtime/GameBoard.cs",
    "        if (IsSpecial &&\n            !IsAllPlayerQuestion &&\n            Wager?.PlayerId != playerId)\n        {\n            throw new GameRuleViolationException(\n                \"Only the wager player can answer a wager question.\");\n        }\n\n        var value = IsSpecial",
    "        if (IsSpecial &&\n            !IsAllPlayerQuestion &&\n            Wager?.PlayerId != playerId)\n        {\n            throw new GameRuleViolationException(\n                \"Only the wager player can answer a wager question.\");\n        }\n\n        if (isCorrect &&\n            rewardModifier != AnswerRewardModifier.Normal &&\n            !AllowAnswerRewardModifiers)\n        {\n            throw new GameRuleViolationException(\n                \"Answer reward modifiers are not enabled for this question.\");\n        }\n\n        var value = IsSpecial")

# Stored quiz -> runtime snapshot.
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizSnapshotFactory.cs",
    "                        presentationType,\n                        buzzerMode,\n                        Math.Max(0, question.BuzzDelaySeconds)))",
    "                        presentationType,\n                        buzzerMode,\n                        Math.Max(0, question.BuzzDelaySeconds),\n                        question.AllowAnswerRewardModifiers)))")

# Deferred-media materialization must preserve the authored flag.
replace_once(
    "src/BadWolfQuiz.Web/Services/DeferredGameMedia.cs",
    "                question.PresentationType,\n                question.BuzzerMode,\n                question.BuzzDelaySeconds)),",
    "                question.PresentationType,\n                question.BuzzerMode,\n                question.BuzzDelaySeconds,\n                question.AllowAnswerRewardModifiers)),")

# Active-game JSON snapshots preserve the flag while old JSON defaults to false.
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizSnapshotJsonConverter.cs",
    "        ContentBlockSnapshot[] QuestionBlocks,\n        ContentBlockSnapshot[] AnswerBlocks,\n        QuestionPresentationType PresentationType = QuestionPresentationType.Standard)",
    "        ContentBlockSnapshot[] QuestionBlocks,\n        ContentBlockSnapshot[] AnswerBlocks,\n        QuestionPresentationType PresentationType = QuestionPresentationType.Standard,\n        bool AllowAnswerRewardModifiers = false)")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizSnapshotJsonConverter.cs",
    "            QuestionBlocks,\n            AnswerBlocks,\n            PresentationType);",
    "            QuestionBlocks,\n            AnswerBlocks,\n            PresentationType,\n            allowAnswerRewardModifiers: AllowAnswerRewardModifiers);")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizSnapshotJsonConverter.cs",
    "                snapshot.QuestionBlocks.ToArray(),\n                snapshot.StoredAnswerBlocks.ToArray(),\n                snapshot.PresentationType);",
    "                snapshot.QuestionBlocks.ToArray(),\n                snapshot.StoredAnswerBlocks.ToArray(),\n                snapshot.PresentationType,\n                snapshot.AllowAnswerRewardModifiers);")

# .bwquiz export/import remains format-v1 compatible via an optional trailing field.
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizPackageService.cs",
    "                                    question.AnswerBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray(),\n                                    question.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToArray()))",
    "                                    question.AnswerBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray(),\n                                    question.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToArray(),\n                                    question.AllowAnswerRewardModifiers))")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizPackageService.cs",
    "                        PresentationType = sourceQuestion.PresentationType,\n                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,\n                        UpdatedAtUtc = now",
    "                        PresentationType = sourceQuestion.PresentationType,\n                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,\n                        AllowAnswerRewardModifiers = sourceQuestion.AllowAnswerRewardModifiers,\n                        UpdatedAtUtc = now")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizPackageService.cs",
    "        bool ExcludeFromRandomWagerSelection, BlockData[] QuestionBlocks, BlockData[] AnswerBlocks,\n        string[]? Tags = null);",
    "        bool ExcludeFromRandomWagerSelection, BlockData[] QuestionBlocks, BlockData[] AnswerBlocks,\n        string[]? Tags = null, bool AllowAnswerRewardModifiers = false);")

# Quiz clone and question copy flows preserve the setting. A flagged question is not blank.
replace_once(
    "src/BadWolfQuiz.Web/Services/QuizCloneOperations.cs",
    "                        PresentationType = sourceQuestion.PresentationType,\n                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,\n                        UpdatedAtUtc = now",
    "                        PresentationType = sourceQuestion.PresentationType,\n                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,\n                        AllowAnswerRewardModifiers = sourceQuestion.AllowAnswerRewardModifiers,\n                        UpdatedAtUtc = now")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuestionCopyOperations.cs",
    "                    !question.ExcludeFromRandomWagerSelection &&\n                    !question.Tags.Any() &&",
    "                    !question.ExcludeFromRandomWagerSelection &&\n                    !question.AllowAnswerRewardModifiers &&\n                    !question.Tags.Any() &&")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuestionCopyOperations.cs",
    "        !question.ExcludeFromRandomWagerSelection &&\n        question.Tags.Count == 0 &&",
    "        !question.ExcludeFromRandomWagerSelection &&\n        !question.AllowAnswerRewardModifiers &&\n        question.Tags.Count == 0 &&")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuestionCopyOperations.cs",
    "        target.PresentationType = source.PresentationType;\n        target.ExcludeFromRandomWagerSelection = source.ExcludeFromRandomWagerSelection;\n        target.UpdatedAtUtc = now;",
    "        target.PresentationType = source.PresentationType;\n        target.ExcludeFromRandomWagerSelection = source.ExcludeFromRandomWagerSelection;\n        target.AllowAnswerRewardModifiers = source.AllowAnswerRewardModifiers;\n        target.UpdatedAtUtc = now;")
replace_once(
    "src/BadWolfQuiz.Web/Services/QuestionCopyOperations.cs",
    "            ExcludeFromRandomWagerSelection =\n                source.ExcludeFromRandomWagerSelection,\n            UpdatedAtUtc = now",
    "            ExcludeFromRandomWagerSelection =\n                source.ExcludeFromRandomWagerSelection,\n            AllowAnswerRewardModifiers = source.AllowAnswerRewardModifiers,\n            UpdatedAtUtc = now")

# Do not reinterpret a newly-authored standard image question as a legacy all-player question.
replace_once(
    "src/BadWolfQuiz.Web/Services/AllPlayerQuestionCompatibility.cs",
    "            question.IsSpecial ||\n            !question.ExcludeFromRandomWagerSelection ||",
    "            question.IsSpecial ||\n            question.AllowAnswerRewardModifiers ||\n            !question.ExcludeFromRandomWagerSelection ||")

# Question Editor read/write/input model.
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    "            ExcludeFromRandomWagerSelection =\n                question.ExcludeFromRandomWagerSelection,\n            BuzzModeOverride = question.BuzzModeOverride,",
    "            ExcludeFromRandomWagerSelection =\n                question.ExcludeFromRandomWagerSelection,\n            AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers,\n            BuzzModeOverride = question.BuzzModeOverride,")
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    "        question.ExcludeFromRandomWagerSelection =\n            isHostMultipleChoice || Input.ExcludeFromRandomWagerSelection;\n        question.BuzzModeOverride =",
    "        question.ExcludeFromRandomWagerSelection =\n            isHostMultipleChoice || Input.ExcludeFromRandomWagerSelection;\n        question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers;\n        question.BuzzModeOverride =")
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml.cs",
    "        [Display(Name = \"Label_ExcludeFromRandomWagerSelection\")]\n        public bool ExcludeFromRandomWagerSelection { get; set; }\n\n        [Display(Name = \"Label_BuzzMode\")]",
    "        [Display(Name = \"Label_ExcludeFromRandomWagerSelection\")]\n        public bool ExcludeFromRandomWagerSelection { get; set; }\n\n        [Display(Name = \"Label_AllowAnswerRewardModifiers\")]\n        public bool AllowAnswerRewardModifiers { get; set; }\n\n        [Display(Name = \"Label_BuzzMode\")]" )

replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Quizzes/QuestionEditor.cshtml",
    "        <label class=\"checkbox-row wager-question-setting\">\n            <input asp-for=\"Input.ExcludeFromRandomWagerSelection\" />\n            <span>\n                <strong>@Localizer[\"Label_ExcludeFromRandomWagerSelection\"]</strong>\n                <small>@Localizer[\"Hint_ExcludeFromRandomWagerSelection\"]</small>\n            </span>\n        </label>\n\n        <div id=\"wager-mode-setting\">",
    "        <label class=\"checkbox-row wager-question-setting\">\n            <input asp-for=\"Input.ExcludeFromRandomWagerSelection\" />\n            <span>\n                <strong>@Localizer[\"Label_ExcludeFromRandomWagerSelection\"]</strong>\n                <small>@Localizer[\"Hint_ExcludeFromRandomWagerSelection\"]</small>\n            </span>\n        </label>\n\n        <label class=\"checkbox-row answer-reward-modifier-setting\">\n            <input asp-for=\"Input.AllowAnswerRewardModifiers\" />\n            <span>\n                <strong>@Localizer[\"Label_AllowAnswerRewardModifiers\"]</strong>\n                <small>@Localizer[\"Hint_AllowAnswerRewardModifiers\"]</small>\n            </span>\n        </label>\n\n        <div id=\"wager-mode-setting\">")

# Localized editor labels. Russian resources intentionally follow the project's Ukraine-only convention.
resource_values = {
    "src/BadWolfQuiz.Web/Resources/Localization/SharedResource.resx": (
        "Allow x2 and 1/2 answer scoring",
        "The host can mark a correct answer for double or half of the normal reward."),
    "src/BadWolfQuiz.Web/Resources/Localization/SharedResource.uk.resx": (
        "Дозволити оцінювання відповіді x2 та 1/2",
        "Ведучий зможе зарахувати правильну відповідь із подвійною або половинною нагородою."),
    "src/BadWolfQuiz.Web/Resources/Localization/SharedResource.it.resx": (
        "Consenti punteggio x2 e 1/2 per la risposta",
        "Il presentatore può segnare una risposta corretta con il doppio o la metà della ricompensa normale."),
    "src/BadWolfQuiz.Web/Resources/Localization/SharedResource.ru.resx": (
        "Україна",
        "Україна"),
}
for path, (label, hint) in resource_values.items():
    text = read(path)
    marker = '  <data name="Label_Created" xml:space="preserve">'
    if text.count(marker) != 1:
        raise RuntimeError(f"{path}: Label_Created marker missing or duplicated")
    addition = (
        '  <data name="Label_AllowAnswerRewardModifiers" xml:space="preserve">\n'
        f'    <value>{label}</value>\n'
        '  </data>\n'
        '  <data name="Hint_AllowAnswerRewardModifiers" xml:space="preserve">\n'
        f'    <value>{hint}</value>\n'
        '  </data>\n')
    write(path, text.replace(marker, addition + marker, 1))

# Registry passes the explicit modifier into the authoritative GameSession method.
replace_once(
    "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs",
    "    public QuestionAnswerAttempt? JudgeQuestionAnswer(\n        string publicCode,\n        int sourceQuestionId,\n        GamePlayerId playerId,\n        bool isCorrect)",
    "    public QuestionAnswerAttempt? JudgeQuestionAnswer(\n        string publicCode,\n        int sourceQuestionId,\n        GamePlayerId playerId,\n        bool isCorrect,\n        AnswerRewardModifier rewardModifier = AnswerRewardModifier.Normal)")
replace_once(
    "src/BadWolfQuiz.Web/Services/GameSessionRegistry.cs",
    "            var attempt = game.Session.JudgeQuestionAnswer(\n                sourceQuestionId,\n                playerId,\n                isCorrect);",
    "            var attempt = game.Session.JudgeQuestionAnswer(\n                sourceQuestionId,\n                playerId,\n                isCorrect,\n                rewardModifier);")

# Generic host judging UI. Dedicated all-player/final/multiple-choice flows are untouched.
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml",
    "                            <button class=\"button judgment-correct-button\"\n                                    name=\"isCorrect\"\n                                    type=\"submit\"\n                                    value=\"true\">\n                                @Localizer[\"Message_Correct\"]\n                            </button>\n                            <button class=\"button judgment-incorrect-button\"\n                                    name=\"isCorrect\"\n                                    type=\"submit\"\n                                    value=\"false\">\n                                @Localizer[\"Message_Wrong\"]\n                            </button>",
    "                            <button class=\"button judgment-correct-button\"\n                                    name=\"judgment\"\n                                    type=\"submit\"\n                                    value=\"correct\">\n                                @Localizer[\"Message_Correct\"]\n                            </button>\n                            @if (Model.CurrentQuestion.AllowAnswerRewardModifiers)\n                            {\n                                <button class=\"button button-secondary judgment-reward-button\"\n                                        name=\"judgment\"\n                                        type=\"submit\"\n                                        value=\"double\">x2</button>\n                                <button class=\"button button-secondary judgment-reward-button\"\n                                        name=\"judgment\"\n                                        type=\"submit\"\n                                        value=\"half\">1/2</button>\n                            }\n                            <button class=\"button judgment-incorrect-button\"\n                                    name=\"judgment\"\n                                    type=\"submit\"\n                                    value=\"incorrect\">\n                                @Localizer[\"Message_Wrong\"]\n                            </button>")
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml",
    "                    if (judgingButton?.name === \"isCorrect\") {\n                        playAnswerFeedbackSound(judgingButton.value === \"true\");\n                    }",
    "                    if (judgingButton?.name === \"isCorrect\") {\n                        playAnswerFeedbackSound(judgingButton.value === \"true\");\n                    } else if (judgingButton?.name === \"judgment\") {\n                        playAnswerFeedbackSound(judgingButton.value !== \"incorrect\");\n                    }")

# Standard host handler resolves the four UI actions. isCorrect remains as a stale-page fallback.
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml.cs",
    "        int sourceQuestionId,\n        Guid playerId,\n        bool isCorrect,\n        CancellationToken cancellationToken)",
    "        int sourceQuestionId,\n        Guid playerId,\n        string? judgment,\n        bool? isCorrect,\n        CancellationToken cancellationToken)")
replace_once(
    "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml.cs",
    "        try\n        {\n            var attempt = sessionRegistry.JudgeQuestionAnswer(\n                game.PublicCode,\n                sourceQuestionId,\n                new GamePlayerId(playerId),\n                isCorrect);\n\n            if (attempt is not null)\n            {\n                var player = game.Session.Players.Single(\n                    item => item.Id == attempt.PlayerId);\n\n                sessionRegistry.SetAnswerResultOverlay(\n                    game,\n                    player,\n                    attempt,\n                    isCorrect ? \"correct\" : \"incorrect\");\n            }\n        }",
    "        try\n        {\n            var resolvedJudgment = judgment switch\n            {\n                \"correct\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Normal),\n                \"double\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Double),\n                \"half\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Half),\n                \"incorrect\" => (IsCorrect: false, RewardModifier: AnswerRewardModifier.Normal),\n                null when isCorrect.HasValue =>\n                    (IsCorrect: isCorrect.Value, RewardModifier: AnswerRewardModifier.Normal),\n                _ => throw new GameRuleViolationException(\"Unknown answer judgment.\")\n            };\n\n            var attempt = sessionRegistry.JudgeQuestionAnswer(\n                game.PublicCode,\n                sourceQuestionId,\n                new GamePlayerId(playerId),\n                resolvedJudgment.IsCorrect,\n                resolvedJudgment.RewardModifier);\n\n            if (attempt is not null)\n            {\n                var player = game.Session.Players.Single(\n                    item => item.Id == attempt.PlayerId);\n\n                sessionRegistry.SetAnswerResultOverlay(\n                    game,\n                    player,\n                    attempt,\n                    resolvedJudgment.IsCorrect ? \"correct\" : \"incorrect\");\n            }\n        }")

# Domain-level functional regression coverage.
write(
    "tests/BadWolfQuiz.Game.Tests/AnswerRewardModifierTests.cs",
    '''using BadWolfQuiz.Game.Definitions;\nusing BadWolfQuiz.Game.Runtime;\n\nnamespace BadWolfQuiz.Game.Tests;\n\npublic sealed class AnswerRewardModifierTests\n{\n    [Theory]\n    [InlineData(100, AnswerRewardModifier.Double, 200)]\n    [InlineData(100, AnswerRewardModifier.Half, 50)]\n    [InlineData(101, AnswerRewardModifier.Half, 51)]\n    [InlineData(1, AnswerRewardModifier.Half, 1)]\n    public void Enabled_correct_answer_modifier_is_explicit_and_survives_recovery(\n        int points,\n        AnswerRewardModifier modifier,\n        int expectedScore)\n    {\n        var session = CreateSession(points, allowAnswerRewardModifiers: true);\n        var player = session.AddPlayer("Wolf");\n        StartAnswering(session, player);\n\n        var attempt = session.JudgeQuestionAnswer(100, player.Id, true, modifier);\n        var restored = GameSession.Restore(\n            session.Quiz,\n            session.Settings,\n            session.CaptureState());\n        var restoredQuestion = restored.Board.Questions.Single();\n        var restoredAttempt = restoredQuestion.AnswerAttempts.Single();\n\n        Assert.Equal(expectedScore, attempt.ScoreDelta);\n        Assert.Equal(expectedScore, player.Score);\n        Assert.Equal(points, session.Board.Questions.Single().Points);\n        Assert.Equal(modifier, attempt.RewardModifier);\n        Assert.Equal(modifier, restoredAttempt.RewardModifier);\n        Assert.True(restoredQuestion.AllowAnswerRewardModifiers);\n    }\n\n    [Fact]\n    public void Disabled_question_rejects_non_normal_correct_reward_modifier_without_score_change()\n    {\n        var session = CreateSession(100, allowAnswerRewardModifiers: false);\n        var player = session.AddPlayer("Wolf");\n        StartAnswering(session, player);\n\n        Assert.Throws<GameRuleViolationException>(() =>\n            session.JudgeQuestionAnswer(\n                100,\n                player.Id,\n                true,\n                AnswerRewardModifier.Double));\n\n        Assert.Equal(0, player.Score);\n        Assert.Empty(session.Board.Questions.Single().AnswerAttempts);\n    }\n\n    [Fact]\n    public void Incorrect_answer_ignores_reward_modifier_and_keeps_normal_penalty()\n    {\n        var session = CreateSession(101, allowAnswerRewardModifiers: true);\n        var player = session.AddPlayer("Wolf");\n        StartAnswering(session, player);\n\n        var attempt = session.JudgeQuestionAnswer(\n            100,\n            player.Id,\n            false,\n            AnswerRewardModifier.Double);\n\n        Assert.Equal(-101, attempt.ScoreDelta);\n        Assert.Equal(-101, player.Score);\n        Assert.Equal(AnswerRewardModifier.Normal, attempt.RewardModifier);\n    }\n\n    [Fact]\n    public void Modified_reward_cannot_be_applied_twice_to_the_same_answer()\n    {\n        var session = CreateSession(100, allowAnswerRewardModifiers: true);\n        var player = session.AddPlayer("Wolf");\n        StartAnswering(session, player);\n\n        session.JudgeQuestionAnswer(\n            100,\n            player.Id,\n            true,\n            AnswerRewardModifier.Double);\n\n        Assert.Throws<GameRuleViolationException>(() =>\n            session.JudgeQuestionAnswer(\n                100,\n                player.Id,\n                true,\n                AnswerRewardModifier.Double));\n        Assert.Equal(200, player.Score);\n        Assert.Single(session.Board.Questions.Single().AnswerAttempts);\n    }\n\n    private static GameSession CreateSession(\n        int points,\n        bool allowAnswerRewardModifiers)\n    {\n        var clue = new ContentBlockSnapshot(\n            1, ContentBlockKind.Text, "Question", null, null, null,\n            null, null, null, null, 0, false);\n        var answer = new ContentBlockSnapshot(\n            2, ContentBlockKind.Text, "Answer", null, null, null,\n            null, null, null, null, 0, false);\n        var question = new QuizQuestionSnapshot(\n            100, 10, 0, points, false, "General", false,\n            [clue], [answer], QuestionPresentationType.Standard,\n            allowAnswerRewardModifiers: allowAnswerRewardModifiers);\n        var quiz = new QuizSnapshot(\n            1,\n            "Reward modifiers",\n            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);\n        return GameSession.Create(quiz);\n    }\n\n    private static void StartAnswering(GameSession session, GamePlayer player)\n    {\n        session.Start();\n        session.SelectQuestion(100);\n        session.ActivateQuestionBuzzer(100);\n        session.ClaimQuestionBuzzer(100, player.Id);\n    }\n}\n''')

# Existing real persistence-flow tests now assert the new authored setting too.
replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuizPackageServiceTests.cs",
    "            BuzzModeOverride = BuzzActivationMode.UseRoundDefault,\n            PresentationType = QuestionPresentationType.Standard",
    "            BuzzModeOverride = BuzzActivationMode.UseRoundDefault,\n            PresentationType = QuestionPresentationType.Standard,\n            AllowAnswerRewardModifiers = true")
replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuizPackageServiceTests.cs",
    "            Assert.Equal(\"Answer\", Assert.Single(importedQuestion.AnswerBlocks).TextContent);\n            Assert.Equal(\n",
    "            Assert.Equal(\"Answer\", Assert.Single(importedQuestion.AnswerBlocks).TextContent);\n            Assert.True(importedQuestion.AllowAnswerRewardModifiers);\n            Assert.Equal(\n")

replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuizCloneOperationsTests.cs",
    "        Assert.True(cloneQuestion.ExcludeFromRandomWagerSelection);\n\n        var cloneQuestionBlock",
    "        Assert.True(cloneQuestion.ExcludeFromRandomWagerSelection);\n        Assert.True(cloneQuestion.AllowAnswerRewardModifiers);\n\n        var cloneQuestionBlock")
replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuizCloneOperationsTests.cs",
    "            PresentationType = QuestionPresentationType.Standard,\n            ExcludeFromRandomWagerSelection = true",
    "            PresentationType = QuestionPresentationType.Standard,\n            ExcludeFromRandomWagerSelection = true,\n            AllowAnswerRewardModifiers = true")

replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuestionCopyOperationsTests.cs",
    "        Assert.True(copied.ExcludeFromRandomWagerSelection);\n\n        var copiedBlock",
    "        Assert.True(copied.ExcludeFromRandomWagerSelection);\n        Assert.True(copied.AllowAnswerRewardModifiers);\n\n        var copiedBlock")
replace_once(
    "tests/BadWolfQuiz.Web.Tests/QuestionCopyOperationsTests.cs",
    "            IsSpecial = true,\n            ExcludeFromRandomWagerSelection = true",
    "            IsSpecial = true,\n            ExcludeFromRandomWagerSelection = true,\n            AllowAnswerRewardModifiers = true")

# Source-level UI/wiring checks catch accidental removal of the editor gate or button order.
write(
    "tests/BadWolfQuiz.Web.Tests/AnswerRewardButtonsRegressionTests.cs",
    '''namespace BadWolfQuiz.Web.Tests;\n\npublic sealed class AnswerRewardButtonsRegressionTests\n{\n    [Fact]\n    public void Question_editor_exposes_opt_in_setting_and_persists_it()\n    {\n        var root = FindRepositoryRoot();\n        var view = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml"));\n        var model = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs"));\n        var entity = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Models", "QuizModels.cs"));\n\n        Assert.Contains("asp-for=\\\"Input.AllowAnswerRewardModifiers\\\"", view);\n        Assert.Contains("Label_AllowAnswerRewardModifiers", view);\n        Assert.Contains("Hint_AllowAnswerRewardModifiers", view);\n        Assert.Contains("public bool AllowAnswerRewardModifiers { get; set; }", entity);\n        Assert.Contains("AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers", model);\n        Assert.Contains("question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers", model);\n    }\n\n    [Fact]\n    public void Host_generic_judging_orders_correct_double_half_incorrect_and_maps_modifiers()\n    {\n        var root = FindRepositoryRoot();\n        var view = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml"));\n        var model = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs"));\n        var registry = File.ReadAllText(Path.Combine(\n            root, "src", "BadWolfQuiz.Web", "Services", "GameSessionRegistry.cs"));\n\n        var formStart = view.IndexOf("<form class=\\\"question-judge-actions\\\"", StringComparison.Ordinal);\n        Assert.True(formStart >= 0);\n        var formEnd = view.IndexOf("</form>", formStart, StringComparison.Ordinal);\n        Assert.True(formEnd > formStart);\n        var form = view[formStart..formEnd];\n\n        Assert.Contains("Model.CurrentQuestion.AllowAnswerRewardModifiers", form);\n        var correct = form.IndexOf("value=\\\"correct\\\"", StringComparison.Ordinal);\n        var doubleReward = form.IndexOf("value=\\\"double\\\"", StringComparison.Ordinal);\n        var halfReward = form.IndexOf("value=\\\"half\\\"", StringComparison.Ordinal);\n        var incorrect = form.IndexOf("value=\\\"incorrect\\\"", StringComparison.Ordinal);\n        Assert.True(correct >= 0 && doubleReward > correct && halfReward > doubleReward && incorrect > halfReward);\n        Assert.Contains(">x2</button>", form);\n        Assert.Contains(">1/2</button>", form);\n\n        Assert.Contains("\\\"double\\\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Double)", model);\n        Assert.Contains("\\\"half\\\" => (IsCorrect: true, RewardModifier: AnswerRewardModifier.Half)", model);\n        Assert.Contains("resolvedJudgment.RewardModifier", model);\n        Assert.Contains("AnswerRewardModifier rewardModifier = AnswerRewardModifier.Normal", registry);\n        Assert.Contains("rewardModifier);", registry);\n        Assert.Contains("judgingButton?.name === \\\"judgment\\\"", view);\n        Assert.Contains("judgingButton.value !== \\\"incorrect\\\"", view);\n    }\n\n    [Fact]\n    public void Authored_reward_setting_flows_through_snapshot_copy_clone_and_package_paths()\n    {\n        var root = FindRepositoryRoot();\n        string Read(params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));\n\n        Assert.Contains("question.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "QuizSnapshotFactory.cs"));\n        Assert.Contains("snapshot.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "QuizSnapshotJsonConverter.cs"));\n        Assert.Contains("question.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "DeferredGameMedia.cs"));\n        Assert.Contains("sourceQuestion.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "QuizPackageService.cs"));\n        Assert.Contains("sourceQuestion.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "QuizCloneOperations.cs"));\n        Assert.Contains("source.AllowAnswerRewardModifiers",\n            Read("src", "BadWolfQuiz.Web", "Services", "QuestionCopyOperations.cs"));\n    }\n\n    private static string FindRepositoryRoot()\n    {\n        var directory = new DirectoryInfo(AppContext.BaseDirectory);\n        while (directory is not null)\n        {\n            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&\n                Directory.Exists(Path.Combine(directory.FullName, "tests")))\n            {\n                return directory.FullName;\n            }\n            directory = directory.Parent;\n        }\n        throw new DirectoryNotFoundException("Repository root was not found.");\n    }\n}\n''')

print("Issue #601 source changes applied.")
