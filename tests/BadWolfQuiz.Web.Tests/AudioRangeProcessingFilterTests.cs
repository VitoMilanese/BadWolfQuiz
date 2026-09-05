using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace BadWolfQuiz.Web.Tests;

public sealed class AudioRangeProcessingFilterTests
{
    [Theory]
    [InlineData("audio/mpeg")]
    [InlineData("audio/ogg")]
    [InlineData("Audio/Wav")]
    public void Audio_file_results_enable_range_processing(string contentType)
    {
        var result = new FileContentResult([1, 2, 3], contentType);

        Execute(result);

        Assert.True(result.EnableRangeProcessing);
    }

    [Fact]
    public void Audio_download_results_keep_their_download_name()
    {
        var result = new FileContentResult([1, 2, 3], "audio/mpeg")
        {
            FileDownloadName = "clip.mp3"
        };

        Execute(result);

        Assert.True(result.EnableRangeProcessing);
        Assert.Equal("clip.mp3", result.FileDownloadName);
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    [InlineData("video/mp4")]
    public void Non_audio_file_results_are_unchanged(string contentType)
    {
        var result = new FileContentResult([1, 2, 3], contentType);

        Execute(result);

        Assert.False(result.EnableRangeProcessing);
    }

    private static void Execute(IActionResult result)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        var context = new ResultExecutingContext(
            actionContext,
            [],
            result,
            new object());

        new AudioRangeProcessingFilter().OnResultExecuting(context);
    }
}
