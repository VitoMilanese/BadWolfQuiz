using System.Collections.Concurrent;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

internal enum DeferredGameMediaRole
{
    Question,
    Answer,
    RoundDescription,
    CategoryDescription,
    FinalDescription,
    FinalQuestion,
    FinalAnswer
}

internal readonly record struct DeferredGameMediaKey(
    DeferredGameMediaRole Role,
    int ContentBlockId);

internal sealed record DeferredGameMedia(
    byte[] Data,
    string ContentType,
    string? FileName);

public sealed class DeferredGameMediaStore(
    IDbContextFactory<QuizDbContext> dbFactory,
    ILogger<DeferredGameMediaStore> logger)
{
    private static readonly byte[] DeferredMarker =
        [0x42, 0x57, 0x51, 0x44, 0x01];

    private readonly ConcurrentDictionary<Guid, Lazy<Task<DeferredGameMediaSet>>>
        loads = new();

    internal static byte[] CreateMarker() => (byte[])DeferredMarker.Clone();

    public static bool IsDeferred(byte[]? fileData) =>
        fileData is { Length: 5 } && fileData.AsSpan().SequenceEqual(DeferredMarker);

    internal static bool HasDeferredMedia(QuizSnapshot quiz) =>
        EnumerateBlocks(quiz).Any(item => IsDeferred(item.Block.FileData));

    internal void WarmAfterLobby(Guid gameId, QuizSnapshot quiz)
    {
        if (HasDeferredMedia(quiz))
        {
            _ = ObserveWarmAsync(gameId, quiz);
        }
    }

    internal async Task<DeferredGameMedia?> ResolveAsync(
        Guid gameId,
        QuizSnapshot quiz,
        DeferredGameMediaRole role,
        ContentBlockSnapshot block,
        CancellationToken cancellationToken)
    {
        if (!IsDeferred(block.FileData))
        {
            return block.FileData is { Length: > 0 } &&
                   !string.IsNullOrWhiteSpace(block.FileContentType)
                ? new DeferredGameMedia(
                    block.FileData,
                    block.FileContentType,
                    block.FileName)
                : null;
        }

        var media = await GetLoadTask(gameId, quiz).WaitAsync(cancellationToken);
        return media.Items.GetValueOrDefault(
            new DeferredGameMediaKey(role, block.SourceContentBlockId));
    }

    internal async Task<QuizSnapshot> MaterializeAsync(
        Guid gameId,
        QuizSnapshot quiz,
        CancellationToken cancellationToken = default)
    {
        if (!HasDeferredMedia(quiz))
        {
            return quiz;
        }

        var media = await GetLoadTask(gameId, quiz).WaitAsync(cancellationToken);

        ContentBlockSnapshot MaterializeBlock(
            ContentBlockSnapshot block,
            DeferredGameMediaRole role)
        {
            if (!IsDeferred(block.FileData))
            {
                return block;
            }

            if (!media.Items.TryGetValue(
                    new DeferredGameMediaKey(role, block.SourceContentBlockId),
                    out var item))
            {
                throw new InvalidDataException(
                    $"Deferred media block {role}/{block.SourceContentBlockId} is unavailable.");
            }

            return block with
            {
                FileData = item.Data,
                FileContentType = item.ContentType,
                FileName = item.FileName
            };
        }

        var rounds = quiz.Rounds.Select(round => new QuizRoundSnapshot(
            round.SourceRoundId,
            round.Title,
            round.SortOrder,
            round.Questions.Select(question => new QuizQuestionSnapshot(
                question.SourceQuestionId,
                question.SourceCategoryId,
                question.RowIndex,
                question.Points,
                question.IsSpecial,
                question.CategoryTitle,
                question.ExcludeFromRandomWagerSelection,
                question.QuestionBlocks.Select(block =>
                    MaterializeBlock(block, DeferredGameMediaRole.Question)),
                question.StoredAnswerBlocks.Select(block =>
                    MaterializeBlock(block, DeferredGameMediaRole.Answer)),
                question.PresentationType,
                question.BuzzerMode,
                question.BuzzDelaySeconds)),
            round.UseRandomWagerQuestions,
            round.RandomWagerQuestionCount,
            round.DescriptionBlocks.Select(block =>
                MaterializeBlock(block, DeferredGameMediaRole.RoundDescription)),
            round.CategoryIntros.Select(category => new QuizCategoryIntroSnapshot(
                category.SourceCategoryId,
                category.Title,
                category.SortOrder,
                category.DescriptionBlocks.Select(block =>
                    MaterializeBlock(
                        block,
                        DeferredGameMediaRole.CategoryDescription)))),
            useRandomAnonymousSharedWagerQuestions:
                round.UseRandomAnonymousSharedWagerQuestions,
            randomAnonymousSharedWagerQuestionCount:
                round.RandomAnonymousSharedWagerQuestionCount))
            .ToArray();

        var finalQuestion = quiz.FinalQuestion is null
            ? null
            : new FinalQuestionSnapshot(
                quiz.FinalQuestion.QuestionBlocks.Select(block =>
                    MaterializeBlock(block, DeferredGameMediaRole.FinalQuestion)),
                quiz.FinalQuestion.AnswerBlocks.Select(block =>
                    MaterializeBlock(block, DeferredGameMediaRole.FinalAnswer)),
                quiz.FinalQuestion.DescriptionBlocks.Select(block =>
                    MaterializeBlock(block, DeferredGameMediaRole.FinalDescription)));

        return new QuizSnapshot(
            quiz.SourceQuizId,
            quiz.Title,
            rounds,
            finalQuestion);
    }

    private Task<DeferredGameMediaSet> GetLoadTask(Guid gameId, QuizSnapshot quiz)
    {
        var lazy = loads.GetOrAdd(
            gameId,
            _ => new Lazy<Task<DeferredGameMediaSet>>(
                () => LoadAsync(gameId, quiz),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private async Task ObserveWarmAsync(Guid gameId, QuizSnapshot quiz)
    {
        try
        {
            await GetLoadTask(gameId, quiz);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Unable to warm deferred media for game {GameId}.",
                gameId);
        }
    }

    private async Task<DeferredGameMediaSet> LoadAsync(Guid gameId, QuizSnapshot quiz)
    {
        try
        {
            var expected = EnumerateBlocks(quiz)
                .Where(item => IsDeferred(item.Block.FileData))
                .GroupBy(item => item.Role)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.Block.SourceContentBlockId)
                        .Distinct()
                        .ToArray());

            if (expected.Count == 0)
            {
                return new DeferredGameMediaSet(
                    new Dictionary<DeferredGameMediaKey, DeferredGameMedia>());
            }

            await using var db = await dbFactory.CreateDbContextAsync();
            var result = new Dictionary<DeferredGameMediaKey, DeferredGameMedia>();
            var quizId = quiz.SourceQuizId;

            if (expected.TryGetValue(DeferredGameMediaRole.Question, out var ids))
            {
                await LoadRoleAsync(
                    db.QuestionContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) &&
                        block.Question.Category.Round.QuizId == quizId),
                    DeferredGameMediaRole.Question,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.Answer, out ids))
            {
                await LoadRoleAsync(
                    db.AnswerContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) &&
                        block.Question.Category.Round.QuizId == quizId),
                    DeferredGameMediaRole.Answer,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.RoundDescription, out ids))
            {
                await LoadRoleAsync(
                    db.RoundDescriptionContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) && block.Round.QuizId == quizId),
                    DeferredGameMediaRole.RoundDescription,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.CategoryDescription, out ids))
            {
                await LoadRoleAsync(
                    db.CategoryDescriptionContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) && block.Category.Round.QuizId == quizId),
                    DeferredGameMediaRole.CategoryDescription,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.FinalDescription, out ids))
            {
                await LoadRoleAsync(
                    db.FinalDescriptionContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) && block.QuizId == quizId),
                    DeferredGameMediaRole.FinalDescription,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.FinalQuestion, out ids))
            {
                await LoadRoleAsync(
                    db.FinalQuestionContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) && block.QuizId == quizId),
                    DeferredGameMediaRole.FinalQuestion,
                    result);
            }

            if (expected.TryGetValue(DeferredGameMediaRole.FinalAnswer, out ids))
            {
                await LoadRoleAsync(
                    db.FinalAnswerContentBlocks.IgnoreQueryFilters().Where(block =>
                        ids.Contains(block.Id) && block.QuizId == quizId),
                    DeferredGameMediaRole.FinalAnswer,
                    result);
            }

            return new DeferredGameMediaSet(result);
        }
        catch
        {
            loads.TryRemove(gameId, out _);
            throw;
        }
    }

    private static async Task LoadRoleAsync<TBlock>(
        IQueryable<TBlock> query,
        DeferredGameMediaRole role,
        IDictionary<DeferredGameMediaKey, DeferredGameMedia> result)
        where TBlock : ContentBlockBase
    {
        var rows = await query
            .AsNoTracking()
            .Select(block => new DeferredGameMediaRow
            {
                Id = block.Id,
                Data = block.FileData,
                ContentType = block.FileContentType,
                FileName = block.FileName
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            if (row.Data is not { Length: > 0 } ||
                string.IsNullOrWhiteSpace(row.ContentType))
            {
                continue;
            }

            var data = row.Data;
            if (string.Equals(
                    row.ContentType,
                    "image/gif",
                    StringComparison.OrdinalIgnoreCase))
            {
                data = MediaUploadProcessor.NormalizeAnimatedGifLoop(data);
            }

            result[new DeferredGameMediaKey(role, row.Id)] = new DeferredGameMedia(
                data,
                row.ContentType,
                row.FileName);
        }
    }

    private static IEnumerable<(DeferredGameMediaRole Role, ContentBlockSnapshot Block)>
        EnumerateBlocks(QuizSnapshot quiz)
    {
        foreach (var round in quiz.Rounds)
        {
            foreach (var block in round.DescriptionBlocks)
            {
                yield return (DeferredGameMediaRole.RoundDescription, block);
            }

            foreach (var category in round.CategoryIntros)
            {
                foreach (var block in category.DescriptionBlocks)
                {
                    yield return (DeferredGameMediaRole.CategoryDescription, block);
                }
            }

            foreach (var question in round.Questions)
            {
                foreach (var block in question.QuestionBlocks)
                {
                    yield return (DeferredGameMediaRole.Question, block);
                }

                foreach (var block in question.StoredAnswerBlocks)
                {
                    yield return (DeferredGameMediaRole.Answer, block);
                }
            }
        }

        if (quiz.FinalQuestion is null)
        {
            yield break;
        }

        foreach (var block in quiz.FinalQuestion.DescriptionBlocks)
        {
            yield return (DeferredGameMediaRole.FinalDescription, block);
        }

        foreach (var block in quiz.FinalQuestion.QuestionBlocks)
        {
            yield return (DeferredGameMediaRole.FinalQuestion, block);
        }

        foreach (var block in quiz.FinalQuestion.AnswerBlocks)
        {
            yield return (DeferredGameMediaRole.FinalAnswer, block);
        }
    }

    private sealed class DeferredGameMediaRow
    {
        public int Id { get; init; }
        public byte[]? Data { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public string? FileName { get; init; }
    }

    private sealed record DeferredGameMediaSet(
        IReadOnlyDictionary<DeferredGameMediaKey, DeferredGameMedia> Items);
}
