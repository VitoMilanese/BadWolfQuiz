using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public sealed record GameSessionRegistration(
    string PublicCode,
    GameSession Session);
