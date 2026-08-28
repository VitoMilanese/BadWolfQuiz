using Microsoft.AspNetCore.Http;

namespace BadWolfQuiz.Web.Services;

public static class NotFoundPagePolicy
{
    public const string Path = "/NotFound";
    public const string RobotsDirective = "noindex, nofollow";

    public static void Apply(HttpResponse response)
    {
        response.StatusCode = StatusCodes.Status404NotFound;
        response.Headers["X-Robots-Tag"] = RobotsDirective;
    }
}
