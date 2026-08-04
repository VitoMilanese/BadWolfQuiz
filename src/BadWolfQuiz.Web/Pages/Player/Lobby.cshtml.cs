using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Player;

public sealed class LobbyModel(
    GameSessionRegistry sessionRegistry,
    QuizRatingService quizRatingService) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public GamePlayer CurrentPlayer { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public string? AccessToken { get; private set; }
    public int? ExistingRating { get; private set; }
    public bool CanRateQuiz { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string code,
        Guid playerId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(code);

        if (game is null)
        {
            return NotFound();
        }

        var players = sessionRegistry.GetPlayers(game);
        var currentPlayer = players.FirstOrDefault(
            player => player.Id == new GamePlayerId(playerId));

        if (currentPlayer is null)
        {
            return NotFound();
        }

        Game = game;
        CurrentPlayer = currentPlayer;
        Players = players;
        AccessToken = accessToken;
        CanRateQuiz = QuizRatingService.IsRatingAvailable(game.Session);
        if (CanRateQuiz)
        {
            ExistingRating = await quizRatingService.GetPlayerRatingAsync(
                code,
                currentPlayer.Id,
                cancellationToken);
        }
        ViewData["GameThemeSettings"] = game.Session.Settings;
        return Page();
    }

    public async Task<IActionResult> OnPostRateQuizAsync(
        string code,
        Guid playerId,
        string? accessToken,
        int? score,
        CancellationToken cancellationToken)
    {
        var result = score.HasValue
            ? await quizRatingService.RateAsync(
                code,
                new GamePlayerId(playerId),
                score.Value,
                cancellationToken)
            : QuizRatingResult.InvalidScore;

        return new JsonResult(new
        {
            saved = result == QuizRatingResult.Saved
        })
        {
            StatusCode = result == QuizRatingResult.Saved ? 200 : 409
        };
    }

    public IActionResult OnGetFinalContentBlock(
        string code,
        Guid playerId,
        int sourceContentBlockId)
    {
        var game = sessionRegistry.Find(code);

        if (game is null ||
            game.Session.Status is not GameSessionStatus.FinalAnswering and
                not GameSessionStatus.FinalJudging and
                not GameSessionStatus.Completed)
        {
            return NotFound();
        }

        var block = game.Session.FinalQuestion?.Definition.QuestionBlocks
            .SingleOrDefault(item =>
                item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }
}
