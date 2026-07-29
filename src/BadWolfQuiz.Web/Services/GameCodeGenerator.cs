using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

public sealed class GameCodeGenerator : IGameCodeGenerator
{
    public const int CodeLength = 6;

    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Create()
    {
        return string.Create(
            CodeLength,
            Alphabet,
            static (characters, alphabet) =>
            {
                for (var index = 0; index < characters.Length; index++)
                {
                    characters[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
                }
            });
    }
}
