using System.Globalization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin;

[Authorize(Policy = "MasterHost")]
public sealed class WordRingsWordCatalogModel(IWebHostEnvironment environment) : PageModel
{
    private const int PageSize = 25;
    private const int MaximumSearchLength = 120;
    private static readonly CompareInfo SearchCompareInfo =
        CultureInfo.GetCultureInfo("uk-UA").CompareInfo;
    private static readonly StringComparer AlphabeticalComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true);

    public IActionResult OnGet(string? search, string? sort, int pageNumber = 1)
    {
        var query = (search ?? string.Empty).Trim();
        if (query.Length > MaximumSearchLength)
        {
            query = query[..MaximumSearchLength];
        }

        var store = WordRingsRuleStore.Get(environment);
        var count = store.GetWordCount();
        IEnumerable<WordRingsWordItem> words = count == 0
            ? Array.Empty<WordRingsWordItem>()
            : store.GetWordsPage(0, count);

        if (query.Length > 0)
        {
            words = words.Where(item =>
                SearchCompareInfo.IndexOf(
                    item.Word,
                    query,
                    CompareOptions.IgnoreCase) >= 0);
        }

        var sortMode = string.Equals(sort, "usage", StringComparison.OrdinalIgnoreCase)
            ? "usage"
            : "alphabetical";
        words = sortMode == "usage"
            ? words
                .OrderByDescending(item => item.TotalRuleCount)
                .ThenBy(item => item.Word, AlphabeticalComparer)
            : words.OrderBy(item => item.Word, AlphabeticalComparer);

        var filtered = words.ToArray();
        var totalPages = Math.Max(1, (filtered.Length + PageSize - 1) / PageSize);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var page = filtered
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToArray();

        return new JsonResult(new
        {
            success = true,
            search = query,
            sort = sortMode,
            pageNumber = currentPage,
            totalPages,
            totalCount = filtered.Length,
            items = page.Select(item => new
            {
                word = item.Word,
                totalRuleCount = item.TotalRuleCount,
                blueRuleCount = item.BlueRuleCount,
                yellowRuleCount = item.YellowRuleCount,
                redRuleCount = item.RedRuleCount
            })
        });
    }
}
