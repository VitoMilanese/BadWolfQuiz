namespace BadWolfQuiz.Web.Services;

public sealed class AvatarCatalog(IWebHostEnvironment environment)
{
    private readonly string _root = Path.Combine(
        environment.ContentRootPath,
        "Resources",
        "Avatars");

    public bool IsValid(string? avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId))
        {
            return false;
        }

        var parts = avatarId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            (parts[0] != "F" && parts[0] != "M") ||
            Path.GetFileName(parts[1]) != parts[1] ||
            !string.Equals(Path.GetExtension(parts[1]), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return File.Exists(Path.Combine(_root, parts[0], parts[1]));
    }
}
