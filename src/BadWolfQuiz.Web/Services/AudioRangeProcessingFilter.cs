using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BadWolfQuiz.Web.Services;

public sealed class AudioRangeProcessingFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is FileResult fileResult &&
            fileResult.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            fileResult.EnableRangeProcessing = true;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
