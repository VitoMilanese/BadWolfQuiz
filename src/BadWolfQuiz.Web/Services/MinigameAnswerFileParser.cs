namespace BadWolfQuiz.Web.Services;

public static class MinigameAnswerFileParser
{
    public static MinigameAnswerFileParseResult Parse(string? content, int expectedCount)
    {
        if (content is null || expectedCount < 0)
        {
            return MinigameAnswerFileParseResult.Invalid(0, expectedCount);
        }

        var values = new List<bool?>();
        using var reader = new StringReader(content);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var value = line.Trim();
            if (value == "1")
            {
                values.Add(true);
            }
            else if (value == "0")
            {
                values.Add(false);
            }
            else if (value.Length == 0)
            {
                values.Add(null);
            }
            else
            {
                return MinigameAnswerFileParseResult.Invalid(lineNumber, expectedCount);
            }
        }

        return values.Count == expectedCount
            ? MinigameAnswerFileParseResult.Valid(values)
            : MinigameAnswerFileParseResult.Invalid(0, expectedCount, values.Count);
    }
}

public sealed record MinigameAnswerFileParseResult(
    bool Success,
    IReadOnlyList<bool?> Answers,
    int InvalidLineNumber,
    int ExpectedCount,
    int ActualCount)
{
    public static MinigameAnswerFileParseResult Valid(IReadOnlyList<bool?> answers) =>
        new(true, answers, 0, answers.Count, answers.Count);

    public static MinigameAnswerFileParseResult Invalid(
        int lineNumber,
        int expectedCount,
        int actualCount = -1) =>
        new(false, [], lineNumber, expectedCount, actualCount);
}
