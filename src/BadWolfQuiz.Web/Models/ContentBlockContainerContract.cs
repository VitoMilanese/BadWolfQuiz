using System.Globalization;

namespace BadWolfQuiz.Web.Models;

public static class ContentBlockContainerContract
{
    public const string RuntimeMarkerPrefix = "__badwolf_container:";
    public const string RuntimeMarkerSuffix = "__";

    public static int ParseChildCount(string? value)
    {
        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var childCount) && childCount > 0
                ? childCount
                : 0;
    }

    public static string StoreChildCount(int childCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(childCount);
        return childCount.ToString(CultureInfo.InvariantCulture);
    }

    public static string CreateRuntimeMarker(string? storedChildCount)
    {
        return $"{RuntimeMarkerPrefix}{ParseChildCount(storedChildCount)}{RuntimeMarkerSuffix}";
    }

    public static bool TryParseRuntimeMarker(
        string? value,
        out int childCount)
    {
        childCount = 0;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(RuntimeMarkerPrefix, StringComparison.Ordinal) ||
            !value.EndsWith(RuntimeMarkerSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var countText = value[
            RuntimeMarkerPrefix.Length..
            ^RuntimeMarkerSuffix.Length];
        childCount = ParseChildCount(countText);
        return true;
    }
}
