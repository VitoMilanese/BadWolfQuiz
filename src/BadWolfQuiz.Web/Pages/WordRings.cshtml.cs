using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsModel(IWebHostEnvironment environment) : PageModel
{
    public WordRingsPuzzle Puzzle { get; private set; } = null!;

    public void OnGet()
    {
        Puzzle = WordRingsRuleStore.Get(environment).CreatePuzzle();
    }
}
