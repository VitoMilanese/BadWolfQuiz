using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BadWolfQuiz.Web.Tests;

public sealed class JoinUrlBuilderTests
{
    [Fact]
    public void Build_uses_configured_public_base_url()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Game:PublicBaseUrl"] = "https://quiz.example.com/"
            })
            .Build();
        var request = new DefaultHttpContext().Request;

        var result = new JoinUrlBuilder(configuration).Build(request, "ABC123");

        Assert.Equal("https://quiz.example.com/Join/ABC123", result);
    }

    [Fact]
    public void Build_falls_back_to_current_request_address()
    {
        var configuration = new ConfigurationBuilder().Build();
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("quiz.local", 7080);
        request.PathBase = "/badwolf";

        var result = new JoinUrlBuilder(configuration).Build(request, "WOLF42");

        Assert.Equal("https://quiz.local:7080/badwolf/Join/WOLF42", result);
    }
}
