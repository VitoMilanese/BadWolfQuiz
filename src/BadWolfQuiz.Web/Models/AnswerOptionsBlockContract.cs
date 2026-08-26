using System.Globalization;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Web.Models;

public static class AnswerOptionsBlockContract
{
    public static int ParseOptionCount(string? storedValue)
    {
        return int.TryParse(
                storedValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var optionCount) &&
            optionCount >= 0
                ? optionCount
                : 0;
    }

    public static string StoreOptionCount(int optionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);
        return optionCount.ToString(CultureInfo.InvariantCulture);
    }

    public static string CreateRuntimeMarker(string? storedValue) =>
        MultipleChoiceAnswerContract.CreateRuntimeMarker(
            ParseOptionCount(storedValue));
}
