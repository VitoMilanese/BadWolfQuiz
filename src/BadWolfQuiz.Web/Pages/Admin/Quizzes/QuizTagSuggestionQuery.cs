using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

internal static class QuizTagSuggestionQuery
{
    public const int MaxResults = 8;
    private const int MaxCandidateResults =
        QuizMetadataEditorModel.MaximumTagCount + MaxResults;

    public static async Task<IReadOnlyList<string>> GetAsync(
        QuizDbContext db,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearchText = searchText?.Trim().ToUpperInvariant();
        if (normalizedSearchText?.Length > 100)
        {
            normalizedSearchText = normalizedSearchText[..100];
        }

        var quizTagQuery = db.QuizTags.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            quizTagQuery = quizTagQuery.Where(tag =>
                tag.NormalizedName.Contains(normalizedSearchText));
        }

        var quizSuggestions = await quizTagQuery
            .GroupBy(tag => tag.NormalizedName)
            .Select(group => new
            {
                NormalizedName = group.Key,
                Name = group.Min(tag => tag.Name),
                UsageCount = group.Count()
            })
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Name)
            .Take(MaxCandidateResults)
            .ToListAsync(cancellationToken);

        if (quizSuggestions.Count >= MaxCandidateResults)
        {
            return quizSuggestions.Select(item => item.Name!).ToList();
        }

        // Quiz-level tags are preferred. Question tags fill remaining candidate
        // slots so a new quiz can reuse the host's established tag vocabulary.
        // The browser filters already-selected tags and shows at most MaxResults.
        var questionTagQuery = db.QuizQuestionTags.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            questionTagQuery = questionTagQuery.Where(tag =>
                tag.NormalizedName.Contains(normalizedSearchText));
        }

        var questionSuggestions = await questionTagQuery
            .GroupBy(tag => tag.NormalizedName)
            .Select(group => new
            {
                NormalizedName = group.Key,
                Name = group.Min(tag => tag.Name),
                UsageCount = group.Count()
            })
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Name)
            .Take(MaxCandidateResults * 2)
            .ToListAsync(cancellationToken);

        var result = new List<string>(MaxCandidateResults);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in quizSuggestions)
        {
            if (seen.Add(item.NormalizedName))
            {
                result.Add(item.Name!);
            }
        }

        foreach (var item in questionSuggestions)
        {
            if (result.Count >= MaxCandidateResults)
            {
                break;
            }

            if (seen.Add(item.NormalizedName))
            {
                result.Add(item.Name!);
            }
        }

        return result;
    }
}
