namespace BadWolfQuiz.Web.Services;

public static class YouTubeEmbedUrlBuilder
{
    public static string? GetYouTubeEmbedUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        var videoId = GetVideoId(uri);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return value;
        }

        var embedUrl =
            $"https://www.youtube-nocookie.com/embed/{Uri.EscapeDataString(videoId)}?enablejsapi=1";
        var startSeconds = GetStartSeconds(uri);

        return startSeconds is > 0
            ? $"{embedUrl}&start={startSeconds.Value}"
            : embedUrl;
    }

    private static string? GetVideoId(Uri uri)
    {
        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
        }

        if (!uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.EndsWith("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var pathSegments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length >= 2 &&
            (string.Equals(pathSegments[0], "embed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(pathSegments[0], "shorts", StringComparison.OrdinalIgnoreCase)))
        {
            return pathSegments[1];
        }

        return GetUrlParameter(uri, "v");
    }

    private static int? GetStartSeconds(Uri uri)
    {
        var timestamp =
            GetUrlParameter(uri, "start") ??
            GetUrlParameter(uri, "t");
        return ParseTimestamp(timestamp);
    }

    private static string? GetUrlParameter(Uri uri, string name)
    {
        foreach (var source in new[]
        {
            uri.Query.TrimStart('?'),
            uri.Fragment.TrimStart('#')
        })
        {
            foreach (var item in source.Split(
                         '&',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split('=', 2);
                if (parts.Length != 2 ||
                    !string.Equals(
                        Decode(parts[0]),
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Decode(parts[1].Replace('+', ' '));
            }
        }

        return null;
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static int? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, out var seconds))
        {
            return seconds >= 0 ? seconds : null;
        }

        long totalSeconds = 0;
        long component = 0;
        var hasComponent = false;
        var hasUnit = false;

        foreach (var character in normalized)
        {
            if (character is >= '0' and <= '9')
            {
                component = component * 10 + (character - '0');
                if (component > int.MaxValue)
                {
                    return null;
                }

                hasComponent = true;
                continue;
            }

            if (!hasComponent || character is not ('h' or 'm' or 's'))
            {
                return null;
            }

            totalSeconds += character switch
            {
                'h' => component * 60 * 60,
                'm' => component * 60,
                _ => component
            };
            if (totalSeconds > int.MaxValue)
            {
                return null;
            }

            component = 0;
            hasComponent = false;
            hasUnit = true;
        }

        if (hasComponent)
        {
            if (!hasUnit)
            {
                return null;
            }

            totalSeconds += component;
        }

        return hasUnit && totalSeconds <= int.MaxValue
            ? (int)totalSeconds
            : null;
    }
}
