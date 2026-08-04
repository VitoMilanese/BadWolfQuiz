using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace BadWolfQuiz.Web.Tests;

public sealed class MediaUploadProcessorTests
{
    [Fact]
    public async Task Opaque_image_is_converted_to_smaller_jpeg()
    {
        var processor = CreateProcessor();
        var original = CreateOpaqueBitmap(100, 100);

        var result = await processor.ProcessImageAsync(
            CreateFile(original, "image/bmp", "question.bmp"));

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal("question.jpg", result.FileName);
        Assert.True(result.Data.Length < original.Length);
    }

    [Fact]
    public async Task Image_with_fully_transparent_pixel_keeps_original_format()
    {
        var processor = CreateProcessor();
        var transparentPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgZGJmAAAAFQAHLKogTgAAAABJRU5ErkJggg==");

        var result = await processor.ProcessImageAsync(
            CreateFile(transparentPng, "image/png", "overlay.png"));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(transparentPng, result.Data);
    }

    [Fact]
    public async Task Media_type_uses_its_own_configured_size_limit()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageUploadMegabytes = 1,
            MaximumAudioUploadMegabytes = 2,
            ConvertAudioToMp3 = false
        });
        var oversizedImage = CreateFile(
            CreateOpaqueBitmap(800, 600),
            "image/bmp",
            "large.bmp");
        var allowedAudio = CreateFile(
            new byte[1024 * 1024 + 1],
            "audio/wav",
            "allowed.wav");

        var exception = await Assert.ThrowsAsync<MediaUploadException>(
            () => processor.ProcessImageAsync(oversizedImage, skipConversion: true));
        var audio = await processor.ProcessAudioAsync(allowedAudio);

        Assert.Equal("FileSizeLimitExceeded", exception.ResourceKey);
        Assert.Equal("audio/wav", audio.ContentType);
    }

    [Fact]
    public async Task Oversized_image_is_resized_proportionally_before_size_check()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageUploadMegabytes = 1,
            MaximumImageWidth = 50,
            MaximumImageHeight = 50
        });

        var result = await processor.ProcessImageAsync(CreateFile(
            CreateOpaqueBitmap(200, 100),
            "image/bmp",
            "wide.bmp"));
        var info = Image.Identify(result.Data);

        Assert.NotNull(info);
        Assert.Equal(50, info.Width);
        Assert.Equal(25, info.Height);
    }

    [Fact]
    public async Task Premium_image_keeps_its_original_format()
    {
        var processor = CreateProcessor();
        var original = CreateOpaqueBitmap(100, 100);

        var result = await processor.ProcessImageAsync(
            CreateFile(original, "image/bmp", "premium.bmp"),
            skipConversion: true);

        Assert.Equal("image/bmp", result.ContentType);
        Assert.Equal(original, result.Data);
    }

    [Fact]
    public async Task Compressed_result_can_fit_even_when_original_exceeds_limit()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageUploadMegabytes = 1
        });
        var original = CreateOpaqueBitmap(800, 600);

        var result = await processor.ProcessImageAsync(
            CreateFile(original, "image/bmp", "large.bmp"));

        Assert.True(original.Length > 1024 * 1024);
        Assert.True(result.Data.Length <= 1024 * 1024);
        Assert.Equal("image/jpeg", result.ContentType);
    }

    private static MediaUploadProcessor CreateProcessor(
        MediaProcessingOptions? options = null) =>
        new(Options.Create(options ?? new MediaProcessingOptions()));

    private static FormFile CreateFile(
        byte[] data,
        string contentType,
        string fileName)
    {
        var stream = new MemoryStream(data);
        return new FormFile(stream, 0, data.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreateOpaqueBitmap(int width, int height)
    {
        var rowSize = (width * 3 + 3) & ~3;
        var pixelBytes = rowSize * height;
        using var stream = new MemoryStream(54 + pixelBytes);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(54 + pixelBytes);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(pixelBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        for (var index = 0; index < pixelBytes; index += 3)
        {
            writer.Write((byte)40);
            writer.Write((byte)120);
            writer.Write((byte)220);
        }
        return stream.ToArray();
    }
}
