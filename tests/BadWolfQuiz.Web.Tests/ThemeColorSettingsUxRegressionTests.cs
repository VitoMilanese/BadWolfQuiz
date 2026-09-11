namespace BadWolfQuiz.Web.Tests;

public sealed class ThemeColorSettingsUxRegressionTests
{
    [Fact]
    public void Running_game_category_color_preview_does_not_revert_while_settings_save_closes_dialog()
    {
        var fields = ReadWebFile("Pages", "Admin", "Games", "_GameSettingsFields.cshtml");

        Assert.Contains("let categoryColorsSavePending = false;", fields, StringComparison.Ordinal);
        Assert.Contains("settingsForm?.addEventListener(\"submit\"", fields, StringComparison.Ordinal);
        Assert.Contains("commitCategoryColorsPreview", fields, StringComparison.Ordinal);
        Assert.Contains("grid.dataset.categoryColorsEnabled =", fields, StringComparison.Ordinal);
        Assert.Contains("if (categoryColorsSavePending)", fields, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_theme_colors_use_custom_picker_with_palette_first_and_ctrl_s_saves_settings()
    {
        var settings = ReadWebFile("Pages", "Admin", "Settings", "Index.cshtml");
        var script = ReadWebFile("wwwroot", "js", "host-settings-theme-color-picker.js");
        var styles = ReadWebFile("wwwroot", "css", "host-settings-theme-color-picker.css");

        Assert.DoesNotContain("type=\"color\" data-theme-variable=", settings, StringComparison.Ordinal);
        Assert.Equal(8, CountOccurrences(settings, "type=\"hidden\" data-theme-variable="));
        Assert.Contains("host-settings-theme-color-picker.css", settings, StringComparison.Ordinal);
        Assert.Contains("host-settings-theme-color-picker.js", settings, StringComparison.Ordinal);
        Assert.Contains("host-theme-color-picker-spectrum", script, StringComparison.Ordinal);
        Assert.Contains("field.append(input, trigger, labelText);", script, StringComparison.Ordinal);
        Assert.Contains("event.ctrlKey || event.metaKey", script, StringComparison.Ordinal);
        Assert.Contains("form.requestSubmit();", script, StringComparison.Ordinal);
        Assert.Contains("min-height: 76px;", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: max-content minmax(0, 1fr);", styles, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }
        return count;
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate)
                    .Replace("\r\n", "\n", StringComparison.Ordinal);
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(Path.Combine(parts));
    }
}
