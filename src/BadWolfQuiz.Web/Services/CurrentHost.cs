using System.Security.Claims;

namespace BadWolfQuiz.Web.Services;

public sealed class CurrentHost(IHttpContextAccessor httpContextAccessor)
{
    public string? Id => httpContextAccessor.HttpContext?.User
        .FindFirstValue(ClaimTypes.NameIdentifier);

    public string RequiredId => Id ?? throw new InvalidOperationException(
        "An authenticated host is required.");
}
