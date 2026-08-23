using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using RuntimeGameSessionStatus = BadWolfQuiz.Game.Runtime.GameSessionStatus;

namespace BadWolfQuiz.Web.Services;

public sealed class DeferredGameMediaMiddleware(
    RequestDelegate next,
    DeferredGameMediaStore mediaStore,
    GameSessionRegistry sessionRegistry)
{
    public async Task InvokeAsync(HttpContext context, CurrentHost currentHost)
    {
        RegisterLobbyWarmup(context, currentHost);

        if (HttpMethods.IsGet(context.Request.Method) &&
            await TryServeDeferredMediaAsync(context, currentHost))
        {
            return;
        }

        await next(context);
    }

    private void RegisterLobbyWarmup(HttpContext context, CurrentHost currentHost)
    {
        if (!HttpMethods.IsGet(context.Request.Method) ||
            !context.Request.Path.StartsWithSegments(
                "/Admin/Games/Lobby",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(context.Request.Query["handler"]))
        {
            return;
        }

        var hostId = currentHost.Id;
        if (string.IsNullOrWhiteSpace(hostId) ||
            !TryGetGuidRouteValue(context, "id", out var gameId))
        {
            return;
        }

        var game = sessionRegistry.FindOwned(new GameSessionId(gameId), hostId);
        if (game is null || !DeferredGameMediaStore.HasDeferredMedia(game.Session.Quiz))
        {
            return;
        }

        context.Response.OnCompleted(() =>
        {
            if (context.Response.StatusCode is >= 200 and < 400)
            {
                mediaStore.WarmAfterLobby(game.Session.Id.Value, game.Session.Quiz);
            }

            return Task.CompletedTask;
        });
    }

    private async Task<bool> TryServeDeferredMediaAsync(
        HttpContext context,
        CurrentHost currentHost)
    {
        var handler = context.Request.Query["handler"].ToString();
        if (string.IsNullOrWhiteSpace(handler))
        {
            return false;
        }

        if (context.Request.Path.StartsWithSegments(
                "/Admin/Games/Lobby",
                StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeLobbyMediaAsync(context, currentHost, handler);
        }

        if (context.Request.Path.StartsWithSegments(
                "/Admin/Games/AnswerKey",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "ContentBlock", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeAnswerKeyMediaAsync(context, currentHost);
        }

        if (context.Request.Path.StartsWithSegments(
                "/Admin/Games/RoundIntro",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "ContentBlock", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeRoundIntroMediaAsync(context, currentHost);
        }

        if (context.Request.Path.StartsWithSegments(
                "/Admin/Games/RunningRoundIntro",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "ContentBlock", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeRunningRoundIntroMediaAsync(context, currentHost);
        }

        if (context.Request.Path.StartsWithSegments(
                "/Admin/Games/FinalQuestionTransition",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "ContentBlock", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeFinalDescriptionMediaAsync(context, currentHost);
        }

        if (context.Request.Path.StartsWithSegments(
                "/Player/Lobby",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "FinalContentBlock", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServePlayerFinalMediaAsync(context);
        }

        if (string.Equals(
                context.Request.Path.Value,
                "/api/all-player-question",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(handler, "OptionImage", StringComparison.OrdinalIgnoreCase))
        {
            return await TryServeAllPlayerOptionMediaAsync(context);
        }

        return false;
    }

    private async Task<bool> TryServeLobbyMediaAsync(
        HttpContext context,
        CurrentHost currentHost,
        string handler)
    {
        var game = FindOwnedGame(context, currentHost);
        if (game is null)
        {
            return false;
        }

        if (string.Equals(handler, "ContentBlock", StringComparison.OrdinalIgnoreCase) &&
            TryGetIntQuery(context, "sourceQuestionId", out var sourceQuestionId) &&
            TryGetIntQuery(context, "sourceContentBlockId", out var sourceContentBlockId))
        {
            var answer = TryGetBoolQuery(context, "answer", out var answerValue) &&
                answerValue;
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId);
            var block = (answer
                    ? question?.AnswerBlocks
                    : question?.QuestionBlocks)?
                .SingleOrDefault(item =>
                    item.SourceContentBlockId == sourceContentBlockId);
            return await TryWriteAsync(
                context,
                game,
                answer ? DeferredGameMediaRole.Answer : DeferredGameMediaRole.Question,
                block);
        }

        if (string.Equals(handler, "FinalContentBlock", StringComparison.OrdinalIgnoreCase) &&
            TryGetIntQuery(context, "sourceContentBlockId", out var finalBlockId))
        {
            var answer = TryGetBoolQuery(context, "answer", out var answerValue) &&
                answerValue;
            var blocks = answer
                ? game.Session.FinalQuestion?.Definition.AnswerBlocks
                : game.Session.FinalQuestion?.Definition.QuestionBlocks;
            var block = blocks?.SingleOrDefault(item =>
                item.SourceContentBlockId == finalBlockId);
            return await TryWriteAsync(
                context,
                game,
                answer
                    ? DeferredGameMediaRole.FinalAnswer
                    : DeferredGameMediaRole.FinalQuestion,
                block);
        }

        return false;
    }

    private async Task<bool> TryServeAnswerKeyMediaAsync(
        HttpContext context,
        CurrentHost currentHost)
    {
        var game = FindOwnedGame(context, currentHost);
        if (game is null ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var final = TryGetBoolQuery(context, "final", out var finalValue) && finalValue;
        ContentBlockSnapshot? block;
        DeferredGameMediaRole role;

        if (final)
        {
            block = game.Session.FinalQuestion?.Definition.AnswerBlocks
                .SingleOrDefault(item => item.SourceContentBlockId == blockId);
            role = DeferredGameMediaRole.FinalAnswer;
        }
        else
        {
            var question = game.Session.Board.Questions.FirstOrDefault(item =>
                item.Status is not RuntimeQuestionStatus.Available and
                    not RuntimeQuestionStatus.Resolved);
            block = question?.AnswerBlocks.SingleOrDefault(item =>
                item.SourceContentBlockId == blockId);
            role = DeferredGameMediaRole.Answer;
        }

        return await TryWriteAsync(context, game, role, block);
    }

    private async Task<bool> TryServeRoundIntroMediaAsync(
        HttpContext context,
        CurrentHost currentHost)
    {
        var game = FindOwnedGame(context, currentHost);
        if (game is null ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var round = game.Session.CurrentRound;
        if (TryGetIntQuery(context, "category", out var categoryIndex))
        {
            var categories = round.CategoryIntros
                .OrderBy(category => category.SortOrder)
                .ToArray();
            if (categoryIndex >= 0 && categoryIndex < categories.Length)
            {
                var categoryBlock = categories[categoryIndex].DescriptionBlocks
                    .SingleOrDefault(item => item.SourceContentBlockId == blockId);
                if (categoryBlock is not null)
                {
                    return await TryWriteAsync(
                        context,
                        game,
                        DeferredGameMediaRole.CategoryDescription,
                        categoryBlock);
                }
            }
        }

        var roundBlock = round.DescriptionBlocks.SingleOrDefault(item =>
            item.SourceContentBlockId == blockId);
        if (roundBlock is not null)
        {
            return await TryWriteAsync(
                context,
                game,
                DeferredGameMediaRole.RoundDescription,
                roundBlock);
        }

        var fallbackCategoryBlock = round.CategoryIntros
            .SelectMany(category => category.DescriptionBlocks)
            .FirstOrDefault(item => item.SourceContentBlockId == blockId);
        return await TryWriteAsync(
            context,
            game,
            DeferredGameMediaRole.CategoryDescription,
            fallbackCategoryBlock);
    }

    private async Task<bool> TryServeRunningRoundIntroMediaAsync(
        HttpContext context,
        CurrentHost currentHost)
    {
        var game = FindOwnedGame(context, currentHost);
        if (game is null ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var round = game.Session.CurrentRound;
        if (!TryGetIntQuery(context, "category", out var categoryIndex))
        {
            var roundBlock = round.DescriptionBlocks.SingleOrDefault(item =>
                item.SourceContentBlockId == blockId);
            return await TryWriteAsync(
                context,
                game,
                DeferredGameMediaRole.RoundDescription,
                roundBlock);
        }

        var returning = TryGetBoolQuery(context, "returning", out var returningValue) &&
            returningValue;
        var categories = round.CategoryIntros
            .Where(category => !returning || game.Session.Board.Questions.Any(question =>
                question.SourceRoundId == round.SourceRoundId &&
                question.SourceCategoryId == category.SourceCategoryId &&
                question.Status == RuntimeQuestionStatus.Available))
            .OrderBy(category => category.SortOrder)
            .ToArray();
        if (categoryIndex < 0 || categoryIndex >= categories.Length)
        {
            return false;
        }

        var block = categories[categoryIndex].DescriptionBlocks.SingleOrDefault(item =>
            item.SourceContentBlockId == blockId);
        return await TryWriteAsync(
            context,
            game,
            DeferredGameMediaRole.CategoryDescription,
            block);
    }

    private async Task<bool> TryServeFinalDescriptionMediaAsync(
        HttpContext context,
        CurrentHost currentHost)
    {
        var game = FindOwnedGame(context, currentHost);
        if (game?.Session.Quiz.FinalQuestion is null ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var block = game.Session.Quiz.FinalQuestion.DescriptionBlocks
            .SingleOrDefault(item => item.SourceContentBlockId == blockId);
        return await TryWriteAsync(
            context,
            game,
            DeferredGameMediaRole.FinalDescription,
            block);
    }

    private async Task<bool> TryServePlayerFinalMediaAsync(HttpContext context)
    {
        if (!TryGetStringRouteValue(context, "code", out var code) ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var game = sessionRegistry.Find(code);
        if (game is null ||
            game.Session.Status is not RuntimeGameSessionStatus.FinalAnswering and
                not RuntimeGameSessionStatus.FinalJudging and
                not RuntimeGameSessionStatus.Completed)
        {
            return false;
        }

        var block = game.Session.FinalQuestion?.Definition.QuestionBlocks
            .SingleOrDefault(item => item.SourceContentBlockId == blockId);
        return await TryWriteAsync(
            context,
            game,
            DeferredGameMediaRole.FinalQuestion,
            block);
    }

    private async Task<bool> TryServeAllPlayerOptionMediaAsync(HttpContext context)
    {
        var code = context.Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code) ||
            !TryGetIntQuery(context, "sourceQuestionId", out var sourceQuestionId) ||
            !TryGetIntQuery(context, "sourceContentBlockId", out var blockId))
        {
            return false;
        }

        var game = sessionRegistry.Find(code);
        if (game is null)
        {
            return false;
        }

        ContentBlockSnapshot? block;
        lock (game)
        {
            var question = game.Session.Board.Questions.SingleOrDefault(item =>
                item.SourceQuestionId == sourceQuestionId &&
                item.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice &&
                item.Status is RuntimeQuestionStatus.Selected or
                    RuntimeQuestionStatus.Active or
                    RuntimeQuestionStatus.ShowingAnswer);
            block = question?.AnswerBlocks.SingleOrDefault(item =>
                item.SourceContentBlockId == blockId &&
                item.Kind == ContentBlockKind.Image);
        }

        if (block is not null && DeferredGameMediaStore.IsDeferred(block.FileData))
        {
            context.Response.Headers["Cache-Control"] = "private, max-age=300";
        }

        return await TryWriteAsync(
            context,
            game,
            DeferredGameMediaRole.Answer,
            block);
    }

    private async Task<bool> TryWriteAsync(
        HttpContext context,
        GameSessionRegistration game,
        DeferredGameMediaRole role,
        ContentBlockSnapshot? block)
    {
        if (block is null || !DeferredGameMediaStore.IsDeferred(block.FileData))
        {
            return false;
        }

        var media = await mediaStore.ResolveAsync(
            game.Session.Id.Value,
            game.Session.Quiz,
            role,
            block,
            context.RequestAborted);
        if (media is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return true;
        }

        await WriteMediaResponseAsync(context, media);
        return true;
    }

    private static async Task WriteMediaResponseAsync(
        HttpContext context,
        DeferredGameMedia media)
    {
        var data = media.Data;
        context.Response.ContentType = media.ContentType;
        context.Response.Headers["Accept-Ranges"] = "bytes";

        if (TryParseRange(
                context.Request.Headers["Range"].ToString(),
                data.Length,
                out var start,
                out var end))
        {
            var count = end - start + 1;
            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.ContentLength = count;
            context.Response.Headers["Content-Range"] =
                $"bytes {start}-{end}/{data.Length}";
            await context.Response.Body.WriteAsync(
                data.AsMemory(start, count),
                context.RequestAborted);
            return;
        }

        context.Response.ContentLength = data.Length;
        await context.Response.Body.WriteAsync(
            data.AsMemory(),
            context.RequestAborted);
    }

    private static bool TryParseRange(
        string rangeHeader,
        int length,
        out int start,
        out int end)
    {
        start = 0;
        end = length - 1;
        if (length <= 0 ||
            string.IsNullOrWhiteSpace(rangeHeader) ||
            !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) ||
            rangeHeader.Contains(','))
        {
            return false;
        }

        var parts = rangeHeader[6..].Split('-', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[0]))
        {
            if (!int.TryParse(parts[1], out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            suffixLength = Math.Min(suffixLength, length);
            start = length - suffixLength;
            end = length - 1;
            return true;
        }

        if (!int.TryParse(parts[0], out start) || start < 0 || start >= length)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]))
        {
            end = length - 1;
            return true;
        }

        if (!int.TryParse(parts[1], out end) || end < start)
        {
            return false;
        }

        end = Math.Min(end, length - 1);
        return true;
    }

    private GameSessionRegistration? FindOwnedGame(
        HttpContext context,
        CurrentHost currentHost)
    {
        var hostId = currentHost.Id;
        return !string.IsNullOrWhiteSpace(hostId) &&
               TryGetGuidRouteValue(context, "id", out var id)
            ? sessionRegistry.FindOwned(new GameSessionId(id), hostId)
            : null;
    }

    private static bool TryGetGuidRouteValue(
        HttpContext context,
        string key,
        out Guid value) =>
        Guid.TryParse(context.Request.RouteValues[key]?.ToString(), out value);

    private static bool TryGetStringRouteValue(
        HttpContext context,
        string key,
        out string value)
    {
        value = context.Request.RouteValues[key]?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetIntQuery(
        HttpContext context,
        string key,
        out int value) =>
        int.TryParse(context.Request.Query[key].ToString(), out value);

    private static bool TryGetBoolQuery(
        HttpContext context,
        string key,
        out bool value) =>
        bool.TryParse(context.Request.Query[key].ToString(), out value);
}
