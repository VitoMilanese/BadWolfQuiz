from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"{path}: expected one match, found {count}: {old[:160]!r}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


script = "src/BadWolfQuiz.Web/wwwroot/js/all-player-question.js"

replace_once(
    script,
    '''        const renderHostChoices = (board, state) => {
            removeHostChoices(board);
            if (state.phase !== "answering" ||
                state.mode !== "multipleChoice") {
                return;
            }

            const presentation = board.querySelector(".question-presentation");
            if (!presentation ||
                presentation.querySelector("[data-all-player-server-preview]")) {
                return;
            }

            const preview = document.createElement("section");
            preview.className = "all-player-host-choice-preview";
            preview.dataset.allPlayerClientPreview = "true";
            preview.tabIndex = 0;''',
    '''        const getHostChoicesRenderKey = state => JSON.stringify({
            sourceQuestionId: state.sourceQuestionId,
            phase: state.phase,
            mode: state.mode,
            options: (state.options ?? []).map(option => [
                option.id,
                option.kind,
                option.text ?? "",
                option.imageUrl ?? ""
            ])
        });

        const renderHostChoices = (board, state) => {
            if (state.phase !== "answering" ||
                state.mode !== "multipleChoice") {
                removeHostChoices(board);
                return;
            }

            const presentation = board.querySelector(".question-presentation");
            if (!presentation) {
                removeHostChoices(board);
                return;
            }

            if (presentation.querySelector("[data-all-player-server-preview]")) {
                removeHostChoices(board);
                return;
            }

            const renderKey = getHostChoicesRenderKey(state);
            const existingPreview = presentation.querySelector(
                "[data-all-player-client-preview]");
            if (existingPreview instanceof HTMLElement &&
                existingPreview.dataset.renderKey === renderKey) {
                return;
            }

            removeHostChoices(board);
            const preview = document.createElement("section");
            preview.className = "all-player-host-choice-preview";
            preview.dataset.allPlayerClientPreview = "true";
            preview.dataset.renderKey = renderKey;
            preview.tabIndex = 0;''')

replace_once(
    script,
    '''            if (state.phase === "answering" &&
                requestRefresh(state, ".question-presentation")) {
                return;
            }''',
    '''            const answeringSelector = state.mode === "multipleChoice"
                ? ".question-presentation [data-all-player-server-preview]"
                : ".question-presentation";
            if (state.phase === "answering" &&
                requestRefresh(state, answeringSelector)) {
                return;
            }''')

test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
replace_once(
    test,
    '''        Assert.Contains("isChoiceExcluded", playerScript, StringComparison.Ordinal);
        Assert.Contains("buzzerAlreadyUsed", playerScript, StringComparison.Ordinal);
        Assert.Contains("item.IsAllPlayerQuestion", registration, StringComparison.Ordinal);''',
    '''        Assert.Contains("isChoiceExcluded", playerScript, StringComparison.Ordinal);
        Assert.Contains("buzzerAlreadyUsed", playerScript, StringComparison.Ordinal);
        Assert.Contains("const answeringSelector = state.mode === \\"multipleChoice\\"", playerScript, StringComparison.Ordinal);
        Assert.Contains(".question-presentation [data-all-player-server-preview]", playerScript, StringComparison.Ordinal);
        Assert.Contains("getHostChoicesRenderKey", playerScript, StringComparison.Ordinal);
        Assert.Contains("preview.dataset.renderKey = renderKey", playerScript, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "const renderHostChoices = (board, state) => {\\n            removeHostChoices(board);",
            playerScript,
            StringComparison.Ordinal);
        Assert.Contains("data-all-player-review-action", lobby, StringComparison.Ordinal);
        Assert.Contains("AllPlayer_ReviewAnswersNow", lobby, StringComparison.Ordinal);
        Assert.Contains("item.IsAllPlayerQuestion", registration, StringComparison.Ordinal);''')
