using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;

namespace BadWolfQuiz.Web.Tests;

public sealed class CategoryDescriptionBlockClonerTests
{
    [Fact]
    public void CopyDescriptions_clones_all_content_and_file_data_independently()
    {
        var sourceFileData = new byte[] { 10, 20, 30 };
        var sourceCategory = new QuizCategory
        {
            Title = "History",
            SortOrder = 2
        };
        sourceCategory.DescriptionBlocks.Add(new CategoryDescriptionContentBlock
        {
            BlockType = ContentBlockType.Text,
            TextContent = "Second block",
            TopCaption = "Top text",
            BottomCaption = "Bottom text",
            MediaPath = "media/text",
            ExternalUrl = "https://example.com/text",
            SortOrder = 2,
            AudioOnly = true,
            Autoplay = true
        });
        sourceCategory.DescriptionBlocks.Add(new CategoryDescriptionContentBlock
        {
            BlockType = ContentBlockType.Image,
            TextContent = "First block",
            TopCaption = "Top image",
            BottomCaption = "Bottom image",
            MediaPath = "media/image",
            ExternalUrl = "https://example.com/image",
            SortOrder = 1,
            FileData = sourceFileData,
            FileContentType = "image/png",
            FileName = "category.png"
        });

        var targetCategory = new QuizCategory
        {
            Title = sourceCategory.Title,
            SortOrder = sourceCategory.SortOrder
        };

        CategoryDescriptionBlockCloner.CopyDescriptions(
            sourceCategory,
            targetCategory);

        var copiedBlocks = targetCategory.DescriptionBlocks.ToList();
        Assert.Equal(2, copiedBlocks.Count);
        Assert.Equal(new[] { 1, 2 }, copiedBlocks.Select(x => x.SortOrder));

        var copiedImage = copiedBlocks[0];
        var sourceImage = sourceCategory.DescriptionBlocks
            .Single(x => x.SortOrder == 1);

        Assert.NotSame(sourceImage, copiedImage);
        Assert.Equal(sourceImage.BlockType, copiedImage.BlockType);
        Assert.Equal(sourceImage.TextContent, copiedImage.TextContent);
        Assert.Equal(sourceImage.TopCaption, copiedImage.TopCaption);
        Assert.Equal(sourceImage.BottomCaption, copiedImage.BottomCaption);
        Assert.Equal(sourceImage.MediaPath, copiedImage.MediaPath);
        Assert.Equal(sourceImage.ExternalUrl, copiedImage.ExternalUrl);
        Assert.Equal(sourceImage.AudioOnly, copiedImage.AudioOnly);
        Assert.Equal(sourceImage.Autoplay, copiedImage.Autoplay);
        Assert.Equal(sourceImage.FileContentType, copiedImage.FileContentType);
        Assert.Equal(sourceImage.FileName, copiedImage.FileName);
        Assert.Equal(sourceImage.FileData, copiedImage.FileData);
        Assert.NotSame(sourceImage.FileData, copiedImage.FileData);

        copiedImage.TextContent = "Changed copy";
        copiedImage.FileData![0] = 99;

        Assert.Equal("First block", sourceImage.TextContent);
        Assert.Equal(10, sourceImage.FileData![0]);
    }

    [Fact]
    public void CopyDescriptions_keeps_empty_descriptions_empty()
    {
        var sourceCategory = new QuizCategory();
        var targetCategory = new QuizCategory();

        CategoryDescriptionBlockCloner.CopyDescriptions(
            sourceCategory,
            targetCategory);

        Assert.Empty(targetCategory.DescriptionBlocks);
    }
}
