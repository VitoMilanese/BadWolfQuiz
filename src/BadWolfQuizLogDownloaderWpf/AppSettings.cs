using System.Text.Json;
using System.IO;

namespace BadWolfQuizLogDownloaderWpf;

internal sealed class AppSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ServiceName { get; set; } = "badwolfquiz.service";
    public bool UseSudo { get; set; } = true;
    public string OutputDirectory { get; set; } = "Logs";

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "appsettings.json was not found next to the executable.",
                path);
        }

        return JsonSerializer.Deserialize<AppSettings>(
                   File.ReadAllText(path),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException("appsettings.json is invalid.");
    }
}
