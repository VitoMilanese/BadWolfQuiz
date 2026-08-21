using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.TagHelpers;

[HtmlTargetElement("form", Attributes = "data-ajax-question-editor")]
public sealed class QuestionEditorContentBlockRepairTagHelper(
    QuizDbContext db) : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(
        TagHelperContext context,
        TagHelperOutput output)
    {
        if (ViewContext.ViewData.Model is not QuestionEditorModel model ||
            model.Input.Id <= 0 ||
            !HasInvalidBlockType(model.Input))
        {
            return;
        }

        var cancellationToken = ViewContext.HttpContext.RequestAborted;
        var question = await db.QuizQuestions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.QuestionBlocks)
            .Include(item => item.AnswerBlocks)
            .SingleOrDefaultAsync(
                item => item.Id == model.Input.Id,
                cancellationToken);
        if (question is null)
        {
            return;
        }

        var result = await QuestionContentBlockRepairOperations
            .RepairEmptyInvalidBlocksAsync(
                db,
                question,
                cancellationToken);
        if (!result.Changed)
        {
            return;
        }

        ReplaceInputBlocks(
            model.Input.QuestionBlocks,
            question.QuestionBlocks,
            isAnswerBlock: false);
        ReplaceInputBlocks(
            model.Input.AnswerBlocks,
            question.AnswerBlocks,
            isAnswerBlock: true);

        // The form body may already have been rendered or cached by another form tag helper.
        // Re-execute it after repairing the model so the current response cannot retain
        // stale BlockType=0 inputs that would fail the next save.
        var repairedContent = await output.GetChildContentAsync(useCachedResult: false);
        output.Content.SetHtmlContent(repairedContent);
    }

    private static bool HasInvalidBlockType(QuestionEditorModel.InputModel input) =>
        input.QuestionBlocks.Any(block => !Enum.IsDefined(block.BlockType)) ||
        input.AnswerBlocks.Any(block => !Enum.IsDefined(block.BlockType));

    private static void ReplaceInputBlocks<TBlock>(
        List<ContentBlockInputModel> target,
        IEnumerable<TBlock> source,
        bool isAnswerBlock)
        where TBlock : ContentBlockBase
    {
        target.Clear();
        target.AddRange(source
            .OrderBy(block => block.SortOrder)
            .Select(block => new ContentBlockInputModel
            {
                Id = block.Id,
                SortOrder = block.SortOrder,
                BlockType = block.BlockType,
                TextContent = block.TextContent,
                TopCaption = block.TopCaption,
                BottomCaption = block.BottomCaption,
                ExternalUrl = block.ExternalUrl,
                AudioOnly = block.AudioOnly,
                Autoplay = block.Autoplay,
                FileContentType = block.FileContentType,
                FileName = block.FileName,
                IsAnswerBlock = isAnswerBlock
            }));
    }
}
