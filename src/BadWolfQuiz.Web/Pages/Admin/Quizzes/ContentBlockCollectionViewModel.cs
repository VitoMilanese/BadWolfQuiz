namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class ContentBlockCollectionViewModel
{
    public required string CollectionId { get; init; }

    public required string Title { get; init; }

    public required string FieldPrefix { get; init; }

    public required List<ContentBlockInputModel> Blocks { get; init; }
}