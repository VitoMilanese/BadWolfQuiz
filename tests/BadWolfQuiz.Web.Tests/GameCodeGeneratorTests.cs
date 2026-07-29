using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameCodeGeneratorTests
{
    [Fact]
    public void Create_returns_six_unambiguous_uppercase_characters()
    {
        var generator = new GameCodeGenerator();

        var code = generator.Create();

        Assert.Equal(GameCodeGenerator.CodeLength, code.Length);
        Assert.All(
            code,
            character => Assert.Contains(
                character,
                "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"));
    }
}
