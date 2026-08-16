namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockAutoplayRegressionTests
{
    [Fact]
    public void Editors_store_autoplay_per_audio_video_or_youtube_block()
    {
        var root = FindRepositoryRoot();
        var input = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "ContentBlockInputModel.cs");
        var audioCard = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Shared", "_AudioBlockCard.cshtml");
        var videoCard = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "Shared", "_VideoBlockCard.cshtml");
        var questionEditor = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs");
        var finalEditor = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Quizzes", "FinalQuestionEditor.cshtml.cs");

        Assert.Contains("public bool Autoplay { get; set; }", input, StringComparison.Ordinal);
        Assert.Contains("class=\"checkbox-row content-block-autoplay-option\"", audioCard, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Autoplay\"", audioCard, StringComparison.Ordinal);
        Assert.Contains("Label_MediaAutoplay", audioCard, StringComparison.Ordinal);
        Assert.Contains("Hint_MediaAutoplay", audioCard, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.Video", videoCard, StringComparison.Ordinal);
        Assert.Contains("ContentBlockType.YouTube", videoCard, StringComparison.Ordinal);
        Assert.Contains("class=\"checkbox-row content-block-autoplay-option\"", videoCard, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"Autoplay\"", videoCard, StringComparison.Ordinal);
        Assert.Contains("Label_MediaAutoplay", videoCard, StringComparison.Ordinal);
        Assert.Contains("Hint_MediaAutoplay", videoCard, StringComparison.Ordinal);
        Assert.Contains("Autoplay = x.Autoplay", questionEditor, StringComparison.Ordinal);
        Assert.Contains("Autoplay = block.Autoplay", finalEditor, StringComparison.Ordinal);
        Assert.Contains("inputBlock.BlockType is ContentBlockType.Audio or ContentBlockType.Video or ContentBlockType.YouTube", questionEditor, StringComparison.Ordinal);
        Assert.Contains("inputBlock.BlockType is ContentBlockType.Audio or ContentBlockType.Video or ContentBlockType.YouTube", finalEditor, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshots_and_packages_preserve_autoplay()
    {
        var root = FindRepositoryRoot();
        var snapshot = Read(root, "src", "BadWolfQuiz.Game", "Definitions", "QuizSnapshot.cs");
        var factory = Read(root, "src", "BadWolfQuiz.Web", "Services", "QuizSnapshotFactory.cs");
        var package = Read(root, "src", "BadWolfQuiz.Web", "Services", "QuizPackageService.cs");

        Assert.Contains("bool Autoplay = false", snapshot, StringComparison.Ordinal);
        Assert.Contains("block.Autoplay", factory, StringComparison.Ordinal);
        Assert.Contains("target.Autoplay = source.Autoplay", package, StringComparison.Ordinal);
        Assert.Contains("bool Autoplay = false", package, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_native_media_autoplays_and_uses_normal_media_lifecycle()
    {
        var root = FindRepositoryRoot();
        var preview = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "_GameContentPreview.cshtml");
        var lobby = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");
        var lobbyModel = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");
        var autoplayLifecycle = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "media-autoplay.js");
        var mediaLifecycle = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "discord-media-mute.js");
        var youtubeLifecycle = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "youtube-auto-expand.js");

        Assert.Equal(2, Count(preview, "data-autoplay-media"));
        Assert.Contains("data-youtube-autoplay", preview, StringComparison.Ordinal);
        Assert.Equal(4, Count(lobby, "data-autoplay-media"));
        Assert.Equal(2, Count(lobby, "data-youtube-autoplay"));
        Assert.Equal(
            3,
            Count(
                lobby,
                "ResolveContentBlockAutoplay(block, isShowingAnswer)"));
        Assert.Equal(
            3,
            Count(
                lobby,
                "ResolveContentBlockAutoplay(block, final: true)"));
        Assert.DoesNotContain(
            "data-autoplay-media=\"@block.Autoplay.ToString().ToLowerInvariant()\"",
            lobby,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-youtube-autoplay=\"@block.Autoplay.ToString().ToLowerInvariant()\"",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("LoadContentBlockAutoplayOverridesAsync", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("values.TryGetValue(block.SourceContentBlockId", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("? autoplay", lobbyModel, StringComparison.Ordinal);
        Assert.Contains(": block.Autoplay;", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("block.Question.Category.Round.QuizId == quizId", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("block.QuizId == quizId", lobbyModel, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "autoplay=\"@(Model.ResolveContentBlockAutoplay",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("activateMediaAutoplay(mediaAutoplayRequest, currentView);", lobby, StringComparison.Ordinal);
        Assert.Contains("data-media-autoplay-state", lobby, StringComparison.Ordinal);
        Assert.Contains("syncViewPreservingMediaPresentation", lobby, StringComparison.Ordinal);
        Assert.Contains("transition,", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("stop: () => stopActivePlayback(null)", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("stopActivePlayback(root);", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("resetAutoplayAttempts(root);", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("stopAllExcept(exceptRoot)", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("media.setAttribute(\"autoplay\", \"\");", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("media.play()", autoplayLifecycle, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfYouTubeAutoExpand?.autoplay?.(root);", autoplayLifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("tryAutoplayNativeMedia", mediaLifecycle, StringComparison.Ordinal);
        Assert.Contains("media.pause();", mediaLifecycle, StringComparison.Ordinal);
        Assert.Contains("media.currentTime = 0", mediaLifecycle, StringComparison.Ordinal);
        Assert.Contains(
            "root instanceof Node && root.isConnected",
            mediaLifecycle,
            StringComparison.Ordinal);
        Assert.Contains("const autoplayMediaTree = rootNode =>", youtubeLifecycle, StringComparison.Ordinal);
        Assert.Contains("launchPlaceholder(placeholder, true);", youtubeLifecycle, StringComparison.Ordinal);
        Assert.Contains("autoplay: autoplayMediaTree", youtubeLifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("queueMicrotask(() => launchPlaceholder(placeholder));", youtubeLifecycle, StringComparison.Ordinal);
        Assert.Contains("event.target.playVideo();", youtubeLifecycle, StringComparison.Ordinal);
    }

    [Fact]
    public void Four_clue_autoplay_targets_only_the_newly_revealed_clue()
    {
        var root = FindRepositoryRoot();
        var lobby = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml");
        var autoplayLifecycle = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "media-autoplay.js");
        var youtubeLifecycle = Read(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "youtube-auto-expand.js");

        Assert.DoesNotContain(
            "autoplay=\"@(Model.ResolveContentBlockAutoplay",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "media.closest(\".question-clue-hidden\")",
            autoplayLifecycle,
            StringComparison.Ordinal);
        Assert.Contains(
            "placeholder.closest(\".question-clue-hidden\")",
            youtubeLifecycle,
            StringComparison.Ordinal);

        var revealStart = lobby.IndexOf(
            "const applyClueReveal = (update, preserveRevealControl = false) =>",
            StringComparison.Ordinal);
        var revealEnd = lobby.IndexOf(
            "const createOptimisticClueReveal = () =>",
            revealStart,
            StringComparison.Ordinal);

        Assert.True(revealStart >= 0);
        Assert.True(revealEnd > revealStart);

        var reveal = lobby[revealStart..revealEnd];
        Assert.Contains(
            "index >= nextRevealedClueCount",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "questionClues.dataset.revealedClueCount =",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfMediaAutoplay?.transition(clue)",
            reveal,
            StringComparison.Ordinal);
        Assert.Contains(
            "presentation.dataset.mediaAutoplayState =",
            reveal,
            StringComparison.Ordinal);

        var signalStart = lobby.IndexOf(
            "connection.on(\"QuestionClueRevealed\"",
            StringComparison.Ordinal);
        var signalEnd = lobby.IndexOf("});", signalStart, StringComparison.Ordinal);
        Assert.True(signalStart >= 0);
        Assert.True(signalEnd > signalStart);
        var signalHandler = lobby[signalStart..(signalEnd + 3)];
        Assert.Contains("applyClueReveal(update);", signalHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("requestHostGameplayRefresh", signalHandler, StringComparison.Ordinal);

        Assert.Contains(
            "const result = await submitGameControl(",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "applyClueReveal(result);",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "flushPendingHostChangesOnComplete = true",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "flushPendingHostChanges();",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "const createOptimisticClueReveal = () =>",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "const completeClueRevealRequest = async (form, submitter) =>",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.addEventListener(\"click\", event =>",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "[data-reveal-clue-form] button[type='submit']",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "applyClueReveal(optimisticUpdate, true);",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "void completeClueRevealRequest(form, button);",
            lobby,
            StringComparison.Ordinal);
        Assert.True(
            lobby.IndexOf("applyClueReveal(optimisticUpdate, true);", StringComparison.Ordinal) <
            lobby.IndexOf("void completeClueRevealRequest(form, button);", StringComparison.Ordinal));
        Assert.Equal(
            2,
            Count(lobby, "window.BadWolfMediaAutoplay?.transition(clue)"));
        Assert.Contains(
            "stopAllExcept: stopPlaybackOutside",
            youtubeLifecycle,
            StringComparison.Ordinal);
        Assert.Contains(
            "resetAutoplay: resetAutoplayTree",
            youtubeLifecycle,
            StringComparison.Ordinal);
        Assert.Contains(
            "copyLiveTimerState(currentView, nextView);",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "getMediaAutoplayState(currentView) !==",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "getMediaAutoplayState(nextView)",
            lobby,
            StringComparison.Ordinal);

        var lobbyModel = Read(root, "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games", "Lobby.cshtml.cs");
        Assert.Contains("revealedClueCount = question.RevealedClueCount", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("canRevealClue = question.CanRevealClue", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("sourceQuestionId,", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("revealedClueCount,", lobbyModel, StringComparison.Ordinal);
        Assert.Contains("canRevealClue", lobbyModel, StringComparison.Ordinal);

        Assert.Contains("data-revealed-clue-count", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "data-media-autoplay-state=\"@mediaAutoplayState\"",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "$\"answer:{Model.CurrentQuestion.SourceQuestionId}\"",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            ".four-clue-grid[data-question-clues][data-revealed-clue-count]",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "nextRevealedClueCount > currentRevealedClueCount",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("kind: \"clues\"", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "startIndex: currentRevealedClueCount",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "endIndex: nextRevealedClueCount",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains("kind: \"presentation\"", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "syncViewPreservingMediaPresentation(currentView, nextView)",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "replaceSiblingsPreserving",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "activateMediaAutoplay(mediaAutoplayRequest, currentView);",
            lobby,
            StringComparison.Ordinal);
        Assert.DoesNotContain("schedulePendingMediaAutoplay", lobby, StringComparison.Ordinal);
        Assert.DoesNotContain("mediaAutoplaySettleMilliseconds", lobby, StringComparison.Ordinal);
        Assert.Contains(
            "rootNode instanceof Node && rootNode.isConnected",
            youtubeLifecycle,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "window.BadWolfMediaAutoplay?.activate(currentView);",
            lobby,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Autoplay_database_migration_covers_all_content_block_tables()
    {
        var root = FindRepositoryRoot();
        var migration = Directory.GetFiles(
                Path.Combine(root, "src", "BadWolfQuiz.Web", "Migrations"),
                "*_AddContentBlockAutoplay.cs")
            .Single();
        var source = File.ReadAllText(migration);

        foreach (var table in new[]
        {
            "QuestionContentBlocks",
            "AnswerContentBlocks",
            "FinalQuestionContentBlocks",
            "FinalAnswerContentBlocks",
            "RoundDescriptionContentBlocks",
            "CategoryDescriptionContentBlocks"
        })
        {
            Assert.Contains($"table: \"{table}\"", source, StringComparison.Ordinal);
        }
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
