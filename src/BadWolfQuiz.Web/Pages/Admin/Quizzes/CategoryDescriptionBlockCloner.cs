using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

internal static class CategoryDescriptionBlockCloner
{
    internal static void CopyDescriptions(
        QuizCategory sourceCategory,
        QuizCategory targetCategory)
    {
        ArgumentNullException.ThrowIfNull(sourceCategory);
        ArgumentNullException.ThrowIfNull(targetCategory);

        foreach (var sourceBlock in sourceCategory.DescriptionBlocks
            .OrderBy(x => x.SortOrder))
        {
            targetCategory.DescriptionBlocks.Add(Clone(sourceBlock));
        }
    }

    private static CategoryDescriptionContentBlock Clone(
        CategoryDescriptionContentBlock sourceBlock)
    {
        return new CategoryDescriptionContentBlock
        {
            BlockType = sourceBlock.BlockType,
            TextContent = sourceBlock.TextContent,
            TopCaption = sourceBlock.TopCaption,
            BottomCaption = sourceBlock.BottomCaption,
            MediaPath = sourceBlock.MediaPath,
            ExternalUrl = sourceBlock.ExternalUrl,
            SortOrder = sourceBlock.SortOrder,
            AudioOnly = sourceBlock.AudioOnly,
            Autoplay = sourceBlock.Autoplay,
            FileData = sourceBlock.FileData?.ToArray(),
            FileContentType = sourceBlock.FileContentType,
            FileName = sourceBlock.FileName
        };
    }
}
