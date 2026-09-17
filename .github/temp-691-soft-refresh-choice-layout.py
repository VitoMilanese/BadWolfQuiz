from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(
            f"{path}: expected exactly one match, found {count}: {old[:160]!r}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


lobby = "src/BadWolfQuiz.Web/Pages/Admin/Games/Lobby.cshtml"
replace_once(
    lobby,
    '''            const sameMediaAutoplayState =
                currentMediaAutoplayState !== null &&
                currentMediaAutoplayState === nextMediaAutoplayState;

            const currentLeaderboardSignature =''',
    '''            const sameMediaAutoplayState =
                currentMediaAutoplayState !== null &&
                currentMediaAutoplayState === nextMediaAutoplayState;
            const sameAllPlayerChoiceLayout =
                (currentView.querySelector("[data-all-player-server-preview]") !== null) ===
                (nextView.querySelector("[data-all-player-server-preview]") !== null);

            const currentLeaderboardSignature =''')

replace_once(
    lobby,
    '''                const preservedLiveMedia = sameMediaAutoplayState &&
                    syncViewPreservingMediaPresentation(currentView, nextView);''',
    '''                const preservedLiveMedia = sameMediaAutoplayState &&
                    sameAllPlayerChoiceLayout &&
                    syncViewPreservingMediaPresentation(currentView, nextView);''')

test = "tests/BadWolfQuiz.Web.Tests/OnDemandAllPlayerChoiceRegressionTests.cs"
replace_once(
    test,
    '''        Assert.Contains("data-all-player-review-action", lobby, StringComparison.Ordinal);
        Assert.Contains("AllPlayer_ReviewAnswersNow", lobby, StringComparison.Ordinal);
        Assert.Contains("item.IsAllPlayerQuestion", registration, StringComparison.Ordinal);''',
    '''        Assert.Contains("data-all-player-review-action", lobby, StringComparison.Ordinal);
        Assert.Contains("AllPlayer_ReviewAnswersNow", lobby, StringComparison.Ordinal);
        Assert.Contains("const sameAllPlayerChoiceLayout =", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "currentView.querySelector(\\\"[data-all-player-server-preview]\\\")",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "nextView.querySelector(\\\"[data-all-player-server-preview]\\\")",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "sameMediaAutoplayState &&\\n                    sameAllPlayerChoiceLayout &&",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("item.IsAllPlayerQuestion", registration, StringComparison.Ordinal);''')
