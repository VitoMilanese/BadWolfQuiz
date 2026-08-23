using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class GameSessionLauncher(
    QuizDbContext db,
    QuizSnapshotFactory snapshotFactory,
    GameSessionRegistry sessionRegistry,
    GameSettingsStore settingsStore,
    CurrentHost currentHost)
{
    private sealed class LaunchContentBlockData
    {
        public int Id { get; init; }
        public int ParentId { get; init; }
        public ContentBlockType BlockType { get; init; }
        public string? TextContent { get; init; }
        public string? TopCaption { get; init; }
        public string? BottomCaption { get; init; }
        public string? MediaPath { get; init; }
        public string? ExternalUrl { get; init; }
        public int SortOrder { get; init; }
        public bool AudioOnly { get; init; }
        public bool Autoplay { get; init; }
        public string? FileContentType { get; init; }
        public string? FileName { get; init; }
        public bool HasFileData { get; init; }
    }

    public async Task<GameSessionRegistration?> CreateAsync(
        int quizId,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsStore.LoadAsync(
            currentHost.RequiredId,
            cancellationToken);

        return await CreateAsync(
            quizId,
            settings,
            cancellationToken);
    }

    public async Task<GameSessionRegistration?> CreateAsync(
        int quizId,
        GameSessionSettings settings,
        CancellationToken cancellationToken = default)
    {
        // The launch path intentionally loads only quiz structure here. Content
        // block metadata is projected separately below so SQLite never has to
        // materialize large FileData BLOB values before the Lobby can render.
        var quiz = await db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Rounds)
                .ThenInclude(round => round.Rows)
            .Include(item => item.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
            .SingleOrDefaultAsync(
                item => item.Id == quizId &&
                    !item.IsArchived &&
                    item.MediaState == QuizMediaState.Active &&
                    (item.HostId == currentHost.RequiredId || item.IsPublic),
                cancellationToken);

        if (quiz is null)
        {
            return null;
        }

        await LoadContentBlockMetadataAsync(quiz, cancellationToken);

        // CreateFromDetachedQuiz converts the lightweight stored-file presence
        // markers into runtime deferred-media markers. Real bytes are loaded
        // after the Lobby response and are materialized before resume snapshots
        // are persisted.
        var snapshot = snapshotFactory.CreateFromDetachedQuiz(quiz);

        // Register the replacement lobby without a host first so creating it
        // does not evict the currently persisted unfinished game. The host is
        // assigned immediately after registration, before the launcher returns.
        var registration = sessionRegistry.Create(snapshot, settings);
        registration.AssignHost(currentHost.RequiredId);
        return registration;
    }

    private async Task LoadContentBlockMetadataAsync(
        Quiz quiz,
        CancellationToken cancellationToken)
    {
        var roundById = quiz.Rounds.ToDictionary(round => round.Id);
        var categoryById = quiz.Rounds
            .SelectMany(round => round.Categories)
            .ToDictionary(category => category.Id);
        var questionById = quiz.Rounds
            .SelectMany(round => round.Categories)
            .SelectMany(category => category.Questions)
            .ToDictionary(question => question.Id);

        var roundBlocks = await db.RoundDescriptionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.Round.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizRoundId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in roundBlocks)
        {
            if (roundById.TryGetValue(data.ParentId, out var round))
            {
                var block = CreateLightweightBlock<RoundDescriptionContentBlock>(data);
                block.QuizRoundId = data.ParentId;
                round.DescriptionBlocks.Add(block);
            }
        }

        var categoryBlocks = await db.CategoryDescriptionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.Category.Round.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizCategoryId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in categoryBlocks)
        {
            if (categoryById.TryGetValue(data.ParentId, out var category))
            {
                var block = CreateLightweightBlock<CategoryDescriptionContentBlock>(data);
                block.QuizCategoryId = data.ParentId;
                category.DescriptionBlocks.Add(block);
            }
        }

        var questionBlocks = await db.QuestionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.Question.Category.Round.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizQuestionId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in questionBlocks)
        {
            if (questionById.TryGetValue(data.ParentId, out var question))
            {
                var block = CreateLightweightBlock<QuestionContentBlock>(data);
                block.QuizQuestionId = data.ParentId;
                question.QuestionBlocks.Add(block);
            }
        }

        var answerBlocks = await db.AnswerContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.Question.Category.Round.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizQuestionId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in answerBlocks)
        {
            if (questionById.TryGetValue(data.ParentId, out var question))
            {
                var block = CreateLightweightBlock<AnswerContentBlock>(data);
                block.QuizQuestionId = data.ParentId;
                question.AnswerBlocks.Add(block);
            }
        }

        var finalDescriptionBlocks = await db.FinalDescriptionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in finalDescriptionBlocks)
        {
            var block = CreateLightweightBlock<FinalDescriptionContentBlock>(data);
            block.QuizId = quiz.Id;
            quiz.FinalDescriptionBlocks.Add(block);
        }

        var finalQuestionBlocks = await db.FinalQuestionContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in finalQuestionBlocks)
        {
            var block = CreateLightweightBlock<FinalQuestionContentBlock>(data);
            block.QuizId = quiz.Id;
            quiz.FinalQuestionBlocks.Add(block);
        }

        var finalAnswerBlocks = await db.FinalAnswerContentBlocks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(block => block.QuizId == quiz.Id)
            .Select(block => new LaunchContentBlockData
            {
                Id = block.Id,
                ParentId = block.QuizId,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                MediaPath = block.MediaPath,
                ExternalUrl = block.ExternalUrl,
                SortOrder = block.SortOrder,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                HasFileData = block.FileData != null
            })
            .ToListAsync(cancellationToken);
        foreach (var data in finalAnswerBlocks)
        {
            var block = CreateLightweightBlock<FinalAnswerContentBlock>(data);
            block.QuizId = quiz.Id;
            quiz.FinalAnswerBlocks.Add(block);
        }
    }

    private static TBlock CreateLightweightBlock<TBlock>(
        LaunchContentBlockData source)
        where TBlock : ContentBlockBase, new() => new()
    {
        Id = source.Id,
        BlockType = source.BlockType,
        TextContent = source.TextContent,
        TopCaption = source.TopCaption,
        BottomCaption = source.BottomCaption,
        MediaPath = source.MediaPath,
        ExternalUrl = source.ExternalUrl,
        SortOrder = source.SortOrder,
        AudioOnly = source.AudioOnly,
        Autoplay = source.Autoplay,
        FileContentType = source.FileContentType,
        FileName = source.FileName,
        // Presence only. The factory replaces this byte with an unmistakable
        // deferred-media marker; no stored media bytes are read in this query.
        FileData = source.HasFileData ? [0] : null
    };
}
