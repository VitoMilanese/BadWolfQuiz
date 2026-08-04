using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizPackageService(QuizDbContext db)
{
    public const string FileExtension = ".bwquiz";
    private const int FormatVersion = 1;
    private const long MaximumPackageBytes = 1024L * 1024 * 1024;
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public async Task<Stream?> ExportAsync(int quizId, CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.FinalQuestionBlocks)
            .Include(item => item.FinalAnswerBlocks)
            .Include(item => item.Rounds).ThenInclude(round => round.Rows)
            .Include(item => item.Rounds).ThenInclude(round => round.Categories)
                .ThenInclude(category => category.Questions)
                    .ThenInclude(question => question.QuestionBlocks)
            .Include(item => item.Rounds).ThenInclude(round => round.Categories)
                .ThenInclude(category => category.Questions)
                    .ThenInclude(question => question.AnswerBlocks)
            .SingleOrDefaultAsync(item => item.Id == quizId && !item.IsArchived, cancellationToken);

        if (quiz is null)
        {
            return null;
        }

        var output = new FileStream(
            Path.Combine(Path.GetTempPath(), $"badwolfquiz-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        try
        {
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var mediaIndex = 0;
                BlockData MapBlock(ContentBlockBase block)
                {
                    string? mediaEntry = null;
                    if (block.FileData is { Length: > 0 })
                    {
                        var extension = Path.GetExtension(block.FileName);
                        mediaEntry = $"media/{++mediaIndex:D6}{NormalizeExtension(extension)}";
                        var entry = archive.CreateEntry(mediaEntry, CompressionLevel.Optimal);
                        using var stream = entry.Open();
                        stream.Write(block.FileData);
                    }

                    return new BlockData(
                        block.BlockType, block.TextContent, block.TopCaption,
                        block.BottomCaption, block.MediaPath, block.ExternalUrl,
                        block.SortOrder, block.AudioOnly, mediaEntry,
                        block.FileContentType, block.FileName);
                }

                var manifest = new PackageData(
                FormatVersion,
                quiz.Title,
                quiz.Description,
                quiz.Rounds.OrderBy(round => round.SortOrder).Select(round => new RoundData(
                    round.Title,
                    round.SortOrder,
                    round.DefaultTimeLimitSeconds,
                    round.DefaultBuzzMode,
                    round.UseRandomWagerQuestions,
                    round.RandomWagerQuestionCount,
                    round.Rows.OrderBy(row => row.RowIndex)
                        .Select(row => new RowData(row.RowIndex, row.Points)).ToArray(),
                    round.Categories.OrderBy(category => category.SortOrder)
                        .Select(category => new CategoryData(
                            category.Title,
                            category.SortOrder,
                            category.Questions.OrderBy(question => question.RowIndex)
                                .Select(question => new QuestionData(
                                    question.RowIndex,
                                    question.TimeLimitSecondsOverride,
                                    question.BuzzModeOverride,
                                    question.BuzzDelaySeconds,
                                    question.IsSpecial,
                                    question.PresentationType,
                                    question.ExcludeFromRandomWagerSelection,
                                    question.QuestionBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray(),
                                    question.AnswerBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray()))
                                .ToArray()))
                        .ToArray()))
                    .ToArray(),
                quiz.FinalQuestionBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray(),
                quiz.FinalAnswerBlocks.OrderBy(block => block.SortOrder).Select(MapBlock).ToArray());

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
    }

    public async Task<Quiz> ImportAsync(
        Stream packageStream,
        long packageLength,
        string hostId,
        CancellationToken cancellationToken)
    {
        if (packageLength <= 0 || packageLength > MaximumPackageBytes)
        {
            throw new InvalidDataException("The quiz package size is invalid.");
        }

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
        {
            throw new InvalidDataException("The expanded quiz package is too large.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (!entries.TryAdd(NormalizeEntryName(entry.FullName), entry))
            {
                throw new InvalidDataException("The quiz package contains duplicate entries.");
            }
        }
        var manifestEntry = entries.GetValueOrDefault("manifest.json")
            ?? throw new InvalidDataException("The quiz package has no manifest.");
        if (manifestEntry.Length > 5 * 1024 * 1024)
        {
            throw new InvalidDataException("The quiz manifest is too large.");
        }

        PackageData manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<PackageData>(
                manifestStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The quiz manifest is invalid.");
        }

        Validate(manifest);
        var now = DateTime.UtcNow;
        var quiz = new Quiz
        {
            HostId = hostId,
            Title = await CreateImportedTitleAsync(manifest.Title, cancellationToken),
            Description = manifest.Description,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        async Task ApplyBlockAsync(ContentBlockBase target, BlockData source)
        {
            target.BlockType = source.BlockType;
            target.TextContent = source.TextContent;
            target.TopCaption = source.TopCaption;
            target.BottomCaption = source.BottomCaption;
            target.MediaPath = source.MediaPath;
            target.ExternalUrl = source.ExternalUrl;
            target.SortOrder = source.SortOrder;
            target.AudioOnly = source.AudioOnly;
            target.FileContentType = source.FileContentType;
            target.FileName = source.FileName;
            if (!string.IsNullOrWhiteSpace(source.MediaEntry))
            {
                var entryName = NormalizeEntryName(source.MediaEntry);
                var entry = entries.GetValueOrDefault(entryName)
                    ?? throw new InvalidDataException($"Missing media entry: {entryName}");
                await using var input = entry.Open();
                await using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, cancellationToken);
                target.FileData = buffer.ToArray();
            }
        }

        foreach (var sourceRound in manifest.Rounds.OrderBy(round => round.SortOrder))
        {
            var round = new QuizRound
            {
                Title = sourceRound.Title,
                SortOrder = sourceRound.SortOrder,
                DefaultTimeLimitSeconds = sourceRound.DefaultTimeLimitSeconds,
                DefaultBuzzMode = sourceRound.DefaultBuzzMode,
                UseRandomWagerQuestions = sourceRound.UseRandomWagerQuestions,
                RandomWagerQuestionCount = sourceRound.RandomWagerQuestionCount
            };
            foreach (var sourceRow in sourceRound.Rows)
            {
                round.Rows.Add(new QuizRoundRow { RowIndex = sourceRow.RowIndex, Points = sourceRow.Points });
            }
            foreach (var sourceCategory in sourceRound.Categories.OrderBy(category => category.SortOrder))
            {
                var category = new QuizCategory { Title = sourceCategory.Title, SortOrder = sourceCategory.SortOrder };
                foreach (var sourceQuestion in sourceCategory.Questions)
                {
                    var question = new QuizQuestion
                    {
                        RowIndex = sourceQuestion.RowIndex,
                        TimeLimitSecondsOverride = sourceQuestion.TimeLimitSecondsOverride,
                        BuzzModeOverride = sourceQuestion.BuzzModeOverride,
                        BuzzDelaySeconds = sourceQuestion.BuzzDelaySeconds,
                        IsSpecial = sourceQuestion.IsSpecial,
                        PresentationType = sourceQuestion.PresentationType,
                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,
                        UpdatedAtUtc = now
                    };
                    foreach (var sourceBlock in sourceQuestion.QuestionBlocks)
                    {
                        var block = new QuestionContentBlock();
                        await ApplyBlockAsync(block, sourceBlock);
                        question.QuestionBlocks.Add(block);
                    }
                    foreach (var sourceBlock in sourceQuestion.AnswerBlocks)
                    {
                        var block = new AnswerContentBlock();
                        await ApplyBlockAsync(block, sourceBlock);
                        question.AnswerBlocks.Add(block);
                    }
                    category.Questions.Add(question);
                }
                round.Categories.Add(category);
            }
            quiz.Rounds.Add(round);
        }

        foreach (var sourceBlock in manifest.FinalQuestionBlocks)
        {
            var block = new FinalQuestionContentBlock();
            await ApplyBlockAsync(block, sourceBlock);
            quiz.FinalQuestionBlocks.Add(block);
        }
        foreach (var sourceBlock in manifest.FinalAnswerBlocks)
        {
            var block = new FinalAnswerContentBlock();
            await ApplyBlockAsync(block, sourceBlock);
            quiz.FinalAnswerBlocks.Add(block);
        }

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(cancellationToken);
        return quiz;
    }

    private async Task<string> CreateImportedTitleAsync(string title, CancellationToken cancellationToken)
    {
        if (!await db.Quizzes.AnyAsync(item => item.Title == title && !item.IsArchived, cancellationToken))
        {
            return title;
        }

        var baseTitle = title.Length <= 149 ? $"{title} (imported)" : title[..149] + " (imported)";
        var candidate = baseTitle;
        for (var suffix = 2; await db.Quizzes.AnyAsync(item => item.Title == candidate && !item.IsArchived, cancellationToken); suffix++)
        {
            var marker = $" ({suffix})";
            candidate = baseTitle[..Math.Min(baseTitle.Length, 160 - marker.Length)] + marker;
        }
        return candidate;
    }

    private static void Validate(PackageData package)
    {
        if (package.FormatVersion != FormatVersion ||
            string.IsNullOrWhiteSpace(package.Title) || package.Title.Length > 160 ||
            package.Description?.Length > 1000 || package.Rounds is null ||
            package.FinalQuestionBlocks is null || package.FinalAnswerBlocks is null ||
            package.Rounds.Length is < 1 or > 100)
        {
            throw new InvalidDataException("The quiz manifest is not supported.");
        }
        if (package.Rounds.Any(round =>
            round is null || string.IsNullOrWhiteSpace(round.Title) || round.Title.Length > 100 ||
            round.Rows is null || round.Categories is null ||
            round.Rows.Length is < 1 or > 100 || round.Categories.Length > 100 ||
            round.Categories.Any(category => category is null ||
                string.IsNullOrWhiteSpace(category.Title) ||
                category.Title.Length > 100 || category.Questions is null ||
                category.Questions.Length > 100 || category.Questions.Any(question =>
                    question is null || question.QuestionBlocks is null || question.AnswerBlocks is null))))
        {
            throw new InvalidDataException("The quiz manifest contains invalid data.");
        }
        foreach (var round in package.Rounds)
        {
            if (round.RandomWagerQuestionCount < 0 ||
                round.Rows.Select(row => row.RowIndex).Distinct().Count() != round.Rows.Length ||
                round.Categories.Select(category => category.SortOrder).Distinct().Count() != round.Categories.Length)
            {
                throw new InvalidDataException("The quiz manifest contains invalid round data.");
            }
            foreach (var question in round.Categories.SelectMany(category => category.Questions))
            {
                if (!Enum.IsDefined(question.BuzzModeOverride) ||
                    !Enum.IsDefined(question.PresentationType) ||
                    question.PresentationType == QuestionPresentationType.FourClues &&
                        (question.IsSpecial || question.QuestionBlocks.Length != 4))
                {
                    throw new InvalidDataException("The quiz manifest contains invalid question data.");
                }
            }
        }
        foreach (var block in package.Rounds.SelectMany(round => round.Categories)
                     .SelectMany(category => category.Questions)
                     .SelectMany(question => question.QuestionBlocks.Concat(question.AnswerBlocks))
                     .Concat(package.FinalQuestionBlocks).Concat(package.FinalAnswerBlocks))
        {
            if (block is null || !Enum.IsDefined(block.BlockType) || block.TextContent?.Length > 100_000 ||
                block.TopCaption?.Length > 2_000 || block.BottomCaption?.Length > 2_000 ||
                block.MediaEntry is not null &&
                    !NormalizeEntryName(block.MediaEntry).StartsWith("media/", StringComparison.OrdinalIgnoreCase) ||
                block.MediaEntry is not null && block.BlockType == ContentBlockType.Image &&
                    !(block.FileContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false) ||
                block.MediaEntry is not null && block.BlockType == ContentBlockType.Audio &&
                    !(block.FileContentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                throw new InvalidDataException("The quiz manifest contains an invalid content block.");
            }
        }
    }

    private static string NormalizeEntryName(string name)
    {
        var normalized = name.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(part => part is ".." or "."))
        {
            throw new InvalidDataException("The quiz package contains an unsafe path.");
        }
        return normalized;
    }

    private static string NormalizeExtension(string? extension) =>
        !string.IsNullOrEmpty(extension) && extension.Length <= 12 &&
        extension.All(character => char.IsLetterOrDigit(character) || character == '.')
            ? extension.ToLowerInvariant()
            : ".bin";

    private sealed record PackageData(
        int FormatVersion, string Title, string? Description, RoundData[] Rounds,
        BlockData[] FinalQuestionBlocks, BlockData[] FinalAnswerBlocks);
    private sealed record RoundData(
        string Title, int SortOrder, int DefaultTimeLimitSeconds,
        BuzzActivationMode DefaultBuzzMode, bool UseRandomWagerQuestions,
        int RandomWagerQuestionCount, RowData[] Rows, CategoryData[] Categories);
    private sealed record RowData(int RowIndex, int Points);
    private sealed record CategoryData(string Title, int SortOrder, QuestionData[] Questions);
    private sealed record QuestionData(
        int RowIndex, int? TimeLimitSecondsOverride, BuzzActivationMode BuzzModeOverride,
        int BuzzDelaySeconds, bool IsSpecial, QuestionPresentationType PresentationType,
        bool ExcludeFromRandomWagerSelection, BlockData[] QuestionBlocks, BlockData[] AnswerBlocks);
    private sealed record BlockData(
        ContentBlockType BlockType, string? TextContent, string? TopCaption,
        string? BottomCaption, string? MediaPath, string? ExternalUrl,
        int SortOrder, bool AudioOnly, string? MediaEntry,
        string? FileContentType, string? FileName);
}
