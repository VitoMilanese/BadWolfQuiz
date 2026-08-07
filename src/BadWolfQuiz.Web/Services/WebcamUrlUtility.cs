namespace BadWolfQuiz.Web.Services;

public static class WebcamUrlUtility
{
    public static string MuteAudio(string value)
    {
        var uri = new UriBuilder(value);
        uri.Query = AddMuteParameters(uri.Query);
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            uri.Fragment = AddMuteParameters(uri.Fragment);
        }
        return uri.Uri.AbsoluteUri;
    }

    private static string AddMuteParameters(string value)
    {
        var parameters = value.TrimStart('?', '#')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(parameter =>
            {
                var separator = parameter.IndexOf('=');
                var name = separator < 0 ? parameter : parameter[..separator];
                return !string.Equals(name, "deafen", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "noaudio", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        parameters.Add("deafen=1");
        parameters.Add("noaudio");
        return string.Join('&', parameters);
    }
}
