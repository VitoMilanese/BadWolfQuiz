using BadWolfQuiz.Web.Pages.Admin.Quizzes;

namespace BadWolfQuiz.Web.Tests;

public sealed class ContentBlockAutoplayDefaultTests
{
    [Fact]
    public void New_content_block_defaults_autoplay_on()
    {
        var input = new ContentBlockInputModel();

        Assert.True(input.Autoplay);
    }
}
