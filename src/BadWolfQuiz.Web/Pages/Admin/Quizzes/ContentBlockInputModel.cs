using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class ContentBlockInputModel
{
    public int? Id { get; set; }

    public int SortOrder { get; set; }

    public ContentBlockType BlockType { get; set; }

    public string? TextContent { get; set; }

    public string? TopCaption { get; set; }

    public string? BottomCaption { get; set; }

    public string? ExternalUrl { get; set; }

    public bool AudioOnly { get; set; }

    public IFormFile? UploadedFile { get; set; }

    public bool RemoveFile { get; set; }

    public string? FileContentType { get; set; }

    public string? FileName { get; set; }
    
    public bool IsAnswerBlock { get; set; }
}