using System.IO;
using System.Text.Json;

namespace BadWolfQuizLogDownloaderWpf;

internal static class UserPreferences
{
    private const string FileName = "preferences.json";

    private static string PreferencesDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BadWolfQuizLogDownloader");

    private static string PreferencesPath => Path.Combine(PreferencesDirectory, FileName);

    public static string? LoadTheme()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return null;
            }

            var preferences = JsonSerializer.Deserialize<Preferences>(
                File.ReadAllText(PreferencesPath));

            return preferences?.Theme;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveTheme(string theme)
    {
        try
        {
            Directory.CreateDirectory(PreferencesDirectory);

            var json = JsonSerializer.Serialize(
                new Preferences { Theme = theme },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(PreferencesPath, json);
        }
        catch
        {
            // Theme persistence should never prevent the application from running.
        }
    }

    private sealed class Preferences
    {
        public string Theme { get; set; } = "Light";
    }
}
