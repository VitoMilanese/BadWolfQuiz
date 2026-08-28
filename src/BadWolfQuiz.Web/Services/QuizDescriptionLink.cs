using System.Security.Cryptography;
using System.Text;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public static class QuizDescriptionLink
{
    private const string TokenPurpose = "BadWolfQuiz:QuizDescription:v1";
    private const int TokenByteLength = 16;

    public static string CreateToken(int quizId, string? hostId, DateTime createdAtUtc)
    {
        var material = string.Join(
            '\n',
            TokenPurpose,
            quizId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hostId?.Trim() ?? string.Empty,
            createdAtUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash.AsSpan(0, TokenByteLength)).ToLowerInvariant();
    }

    public static bool IsValidToken(
        int quizId,
        string? hostId,
        DateTime createdAtUtc,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenByteLength * 2)
        {
            return false;
        }

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(token);
        }
        catch (FormatException)
        {
            return false;
        }

        if (actual.Length != TokenByteLength)
        {
            return false;
        }

        var expected = Convert.FromHexString(CreateToken(quizId, hostId, createdAtUtc));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static string BuildPath(int quizId, string token) =>
        $"/quiz-description/{quizId}/{token}";

    public static string BuildPreviewPath(int quizId, string token) =>
        $"{BuildPath(quizId, token)}/preview.png";

    public static async Task<QuizDescriptionData?> LoadAsync(
        QuizDbContext db,
        int quizId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        var candidate = await db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(quiz => quiz.Id == quizId && !quiz.IsArchived)
            .Select(quiz => new QuizDescriptionCandidate(
                quiz.Id,
                quiz.HostId,
                quiz.CreatedAtUtc,
                quiz.Title,
                quiz.Description,
                quiz.Ratings.Average(rating => (double?)rating.Score),
                quiz.Ratings.Count))
            .SingleOrDefaultAsync(cancellationToken);

        if (candidate is null ||
            !IsValidToken(
                candidate.Id,
                candidate.HostId,
                candidate.CreatedAtUtc,
                token))
        {
            return null;
        }

        var canonicalToken = CreateToken(
            candidate.Id,
            candidate.HostId,
            candidate.CreatedAtUtc);
        return new QuizDescriptionData(
            candidate.Id,
            candidate.Title,
            candidate.Description,
            candidate.AverageRating,
            candidate.RatingCount,
            canonicalToken);
    }

    private sealed record QuizDescriptionCandidate(
        int Id,
        string? HostId,
        DateTime CreatedAtUtc,
        string Title,
        string? Description,
        double? AverageRating,
        int RatingCount);
}

public sealed record QuizDescriptionData(
    int Id,
    string Title,
    string? Description,
    double? AverageRating,
    int RatingCount,
    string Token)
{
    public string Path => QuizDescriptionLink.BuildPath(Id, Token);
    public string PreviewPath => QuizDescriptionLink.BuildPreviewPath(Id, Token);
}
