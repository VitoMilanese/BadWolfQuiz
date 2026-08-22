namespace BadWolfQuiz.Web.Tests;

public sealed class QuizRenameBusyRegressionTests
{
    [Fact]
    public void Rename_dialogs_save_without_following_the_expensive_editor_redirect()
    {
        var script = ReadBusyIndicatorScript();

        Assert.Contains("handler === \"renameround\" || handler === \"renamecategory\"", script);
        Assert.Contains("const submitEditorRename = async", script);
        Assert.Contains("headers: { \"X-Requested-With\": \"XMLHttpRequest\" }", script);
        Assert.Contains("result = await response.json();", script);
        Assert.Contains("!response.ok || !result?.success", script);
        Assert.Contains("applyEditorRename(handler, formData, savedTitle);", script);
        Assert.Contains("form.closest(\"dialog\")?.close();", script);
    }

    [Fact]
    public void Rename_dialogs_lock_all_controls_and_reject_duplicate_submissions()
    {
        var script = ReadBusyIndicatorScript();

        Assert.Contains("const lockEditorRenameDialogControls = form =>", script);
        Assert.Contains("button, input, select, textarea", script);
        Assert.Contains("control.disabled = true;", script);
        Assert.Contains("control.disabled = wasDisabled;", script);
        Assert.Contains("busy || form.dataset.busyLocked === \"true\"", script);
        Assert.Contains("form.dataset.busyLocked = \"true\";", script);
        Assert.Contains("show();", script);
        Assert.Contains("finally {\n            hide();\n        }", script);
    }

    [Fact]
    public void Successful_rename_updates_editor_state_without_a_page_reload()
    {
        var script = ReadBusyIndicatorScript();

        Assert.Contains(".round-tab-link", script);
        Assert.Contains("#delete-round-dialog .dialog-target", script);
        Assert.Contains(".category-title", script);
        Assert.Contains("button.dataset.categoryTitle = title;", script);
        Assert.Contains("button.dataset.questionTitle = questionTitle;", script);
        Assert.Contains("categoryRound.title = title;", script);
        Assert.Contains("question.category = title;", script);

        var renameStart = script.IndexOf(
  "const submitEditorRename = async",
  StringComparison.Ordinal);
        var ajaxSaveStart = script.IndexOf(
  "const isAjaxSave =",
  renameStart,
  StringComparison.Ordinal);
        var renameScript = script[renameStart..ajaxSaveStart];

        Assert.DoesNotContain("window.location.reload()", renameScript, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.assign", renameScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_ajax_endpoint_returns_json_while_native_editor_handlers_remain_as_fallback()
    {
        var ajaxPageModel = ReadWebFile(
  "Pages",
  "Admin",
  "Quizzes",
  "Rename.cshtml.cs");
        var editorPageModel = ReadWebFile(
  "Pages",
  "Admin",
  "Quizzes",
  "Editor.cshtml.cs");

        Assert.Contains("OnPostRoundAsync(", ajaxPageModel);
        Assert.Contains("OnPostCategoryAsync(", ajaxPageModel);
        Assert.Contains("return BadRequest(new", ajaxPageModel);
        Assert.Contains("success = false", ajaxPageModel);
        Assert.Contains("return new JsonResult(new", ajaxPageModel);
        Assert.Contains("success = true", ajaxPageModel);
        Assert.Contains("title = round.Title", ajaxPageModel);
        Assert.Contains("title = category.Title", ajaxPageModel);

        Assert.Contains("OnPostRenameRoundAsync()", editorPageModel);
        Assert.Contains("OnPostRenameCategoryAsync()", editorPageModel);
        Assert.Contains("return RedirectToPage(new", editorPageModel);
    }

    private static string ReadBusyIndicatorScript() => ReadWebFile(
        "wwwroot",
        "js",
        "busy-indicators.js");

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
  var candidate = Path.Combine(
      new[]
      {
          directory.FullName,
          "src",
          "BadWolfQuiz.Web"
      }.Concat(parts).ToArray());

  if (File.Exists(candidate))
  {
      return File.ReadAllText(candidate)
          .Replace("\r\n", "\n");
  }

  directory = directory.Parent;
        }

        throw new FileNotFoundException(
  $"Could not find BadWolfQuiz.Web file: {Path.Combine(parts)}.");
    }
}
