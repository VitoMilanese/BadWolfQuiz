using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

internal static class QuestionTagSuggestionQuery
{
    public const int MaxResults = 8;

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

        var query = db.QuizQuestionTags
            .AsNoTracking()
            .Select(tag => new { tag.Name, tag.NormalizedName })
            .Concat(db.FinalQuestionTags
                .AsNoTracking()
                .Select(tag => new { tag.Name, tag.NormalizedName }));
        if (!string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            query = query.Where(tag =>
                tag.NormalizedName.Contains(normalizedSearchText));
        }

        return await query
            .GroupBy(tag => tag.NormalizedName)
            .Select(group => new
            {
                Name = group.Min(tag => tag.Name),
                UsageCount = group.Count()
            })
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Name)
            .Take(MaxResults)
            .Select(item => item.Name!)
            .ToListAsync(cancellationToken);
    }
}
