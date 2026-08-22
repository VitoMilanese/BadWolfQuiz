using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BadWolfQuiz.Web.Tests;

public sealed class MediaUploadProcessorTests
{
    [Fact]
    public void Default_gif_limits_are_30_and_50_megabytes()
    {
        var processor = CreateProcessor();

        Assert.Equal(30, processor.MaximumGifUploadMegabytes(isPremium: false));
        Assert.Equal(50, processor.MaximumGifUploadMegabytes(isPremium: true));
    }

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
    public async Task Animated_gif_remains_animated_when_dimensions_exceed_resize_limits()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageWidth = 1,
            MaximumImageHeight = 1,
            MaximumGifUploadMegabytes = 30
        });
        var animatedGif = CreateAnimatedGif();

        var result = await processor.ProcessImageAsync(
            CreateFile(animatedGif, "image/gif", "animated.gif"));

        Assert.Equal("image/gif", result.ContentType);
        Assert.Equal("animated.gif", result.FileName);

        using var encoded = SKData.CreateCopy(result.Data);
        using var codec = SKCodec.Create(encoded) ??
            throw new InvalidOperationException("The processed GIF could not be decoded.");
        Assert.Equal(SKEncodedImageFormat.Gif, codec.EncodedFormat);
        Assert.True(codec.FrameCount > 1);
        Assert.Equal(4, codec.Info.Width);
        Assert.Equal(2, codec.Info.Height);
    }

    [Fact]
    public void Gif_loop_normalization_detects_trailing_blank_frame()
    {
        using var encoded = SKData.CreateCopy(CreateGifWithTrailingBlankFrame());
        using var codec = SKCodec.Create(encoded) ??
            throw new InvalidOperationException("The test GIF could not be decoded.");

        var lastVisibleFrameIndex =
            MediaUploadProcessor.GetLastVisibleGifFrameIndex(codec);

        Assert.Equal(4, codec.FrameCount);
        Assert.Equal(2, lastVisibleFrameIndex);
    }

    [Fact]
    public async Task Gif_uses_dedicated_configured_size_limit()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageUploadMegabytes = 1,
            MaximumGifUploadMegabytes = 2,
            ConvertOpaqueImagesToJpeg = false
        });
        var gif = AddGifCommentPadding(
            CreateAnimatedGif(),
            1024 * 1024 + 1024);

        var result = await processor.ProcessImageAsync(
            CreateFile(gif, "image/gif", "large.gif"));

        Assert.Equal("image/gif", result.ContentType);
        using var encoded = SKData.CreateCopy(result.Data);
        using var codec = SKCodec.Create(encoded);
        Assert.NotNull(codec);
        Assert.True(codec.FrameCount > 1);
    }

    [Fact]
    public async Task Premium_gif_uses_higher_configured_limit()
    {
        var processor = CreateProcessor(
            new MediaProcessingOptions
            {
                MaximumGifUploadMegabytes = 1
            },
            new PremiumHostOptions
            {
                MaximumImageUploadMegabytes = 3,
                MaximumGifUploadMegabytes = 2,
                MaximumAudioUploadMegabytes = 2
            });
        var gif = AddGifCommentPadding(
            CreateAnimatedGif(),
            1024 * 1024 + 1024);
        var regularFile = CreateFile(gif, "image/gif", "premium.gif");
        var premiumFile = CreateFile(gif, "image/gif", "premium.gif");

        var exception = await Assert.ThrowsAsync<MediaUploadException>(
            () => processor.ProcessImageAsync(regularFile));
        var result = await processor.ProcessImageAsync(
            premiumFile,
            isPremium: true);

        Assert.Equal("FileSizeLimitExceeded", exception.ResourceKey);
        Assert.Equal(1, (int)Assert.Single(exception.ResourceArguments));
        Assert.Equal("image/gif", result.ContentType);
    }

    [Fact]
    public async Task Media_type_uses_its_own_configured_size_limit()
    {
        var processor = CreateProcessor(new MediaProcessingOptions
        {
            MaximumImageUploadMegabytes = 1,
            MaximumAudioUploadMegabytes = 2,
            ConvertAudioToMp3 = false,
            ConvertOpaqueImagesToJpeg = false
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
            () => processor.ProcessImageAsync(oversizedImage));
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
        using var image = SKBitmap.Decode(result.Data);

        Assert.NotNull(image);
        Assert.Equal(50, image.Width);
        Assert.Equal(25, image.Height);
    }

    [Fact]
    public async Task Premium_image_keeps_its_original_format()
    {
        var processor = CreateProcessor();
        var original = CreateOpaqueBitmap(100, 100);

        var result = await processor.ProcessImageAsync(
            CreateFile(original, "image/bmp", "premium.bmp"),
            isPremium: true);

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

    [Fact]
    public async Task Premium_media_uses_its_higher_configured_limit()
    {
        var processor = CreateProcessor(
            new MediaProcessingOptions
            {
                MaximumAudioUploadMegabytes = 1,
                ConvertAudioToMp3 = false
            },
            new PremiumHostOptions
            {
                MaximumImageUploadMegabytes = 3,
                MaximumGifUploadMegabytes = 4,
                MaximumAudioUploadMegabytes = 2
            });
        var file = CreateFile(
            new byte[1024 * 1024 + 1],
            "audio/wav",
            "premium.wav");

        var result = await processor.ProcessAudioAsync(file, isPremium: true);

        Assert.Equal("audio/wav", result.ContentType);
    }

    private static MediaUploadProcessor CreateProcessor(
        MediaProcessingOptions? options = null,
        PremiumHostOptions? premiumOptions = null) =>
        new(
            Options.Create(options ?? new MediaProcessingOptions()),
            Options.Create(premiumOptions ?? new PremiumHostOptions()));

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

    private static byte[] CreateAnimatedGif() =>
        Convert.FromBase64String(
            "R0lGODlhBAACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAABAACAAAIBwABCBwoMCAAIfkEAQoAAQAsAAAAAAQAAgCBAAD/AAAAAAAAAAAACAcAAQgcKDAgADs=");

    private static byte[] CreateGifWithTrailingBlankFrame() =>
        Convert.FromBase64String(
            "R0lGODlhBAACAIEAAAAAAP8AAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQJCgAAACwAAAAABAACAAAICQADABgIQCCAgAAh+QQJCgAAACwBAAAAAgACAIEAAAD/AAAAAAAAAAAIBwADAAAQICAAIfkECQoAAAAsAgAAAAIAAgCBAAAA/wAAAAAAAAAACAcAAwAAECAgAEdJRjg5YQQAAgCBAAAAAAAAAAAAAAAAAAAh/wtORVRTQ0FQRTIuMAMBAAAAIfkECQwAAAAsAAAAAAQAAgAACAcAAQgcKDAgADs=");

    private static byte[] AddGifCommentPadding(byte[] gif, int payloadBytes)
    {
        using var stream = new MemoryStream(
            gif.Length + payloadBytes + payloadBytes / 255 + 8);
        stream.Write(gif, 0, gif.Length - 1);
        stream.WriteByte(0x21);
        stream.WriteByte(0xFE);

        var remaining = payloadBytes;
        var buffer = new byte[255];
        while (remaining > 0)
        {
            var blockSize = Math.Min(buffer.Length, remaining);
            stream.WriteByte((byte)blockSize);
            stream.Write(buffer, 0, blockSize);
            remaining -= blockSize;
        }

        stream.WriteByte(0);
        stream.WriteByte(0x3B);
        return stream.ToArray();
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
