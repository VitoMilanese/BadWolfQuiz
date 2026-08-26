using System.Linq.Expressions;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

/// <summary>
/// Keeps the Question Editor's tracked question load from joining the two sibling
/// content-block collections into one potentially expensive SQL result set.
/// </summary>
internal static class QuestionEditorSplitQueryExtensions
{
    public static Task<QuizQuestion?> SingleOrDefaultAsync(
        this IIncludableQueryable<QuizQuestion, ICollection<AnswerContentBlock>> source,
        Expression<Func<QuizQuestion, bool>> predicate)
    {
        return EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
            source.AsSplitQuery(),
            predicate);
    }
}
