using System.Diagnostics;
using BadWolfQuiz.Web.Models;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BadWolfQuiz.Web.Services;

public sealed class MediaUploadProcessor(
    IOptions<MediaProcessingOptions> options,
    IOptions<PremiumHostOptions> premiumOptions)
{
    private readonly MediaProcessingOptions _options = options.Value;
    private readonly PremiumHostOptions _premiumOptions = premiumOptions.Value;

    public int MaximumImageUploadMegabytes(bool isPremium) =>
        isPremium
            ? _premiumOptions.MaximumImageUploadMegabytes
            : _options.MaximumImageUploadMegabytes;

    public int MaximumGifUploadMegabytes(bool isPremium) =>
        isPremium
            ? _premiumOptions.MaximumGifUploadMegabytes
            : _options.MaximumGifUploadMegabytes;

    public int MaximumAudioUploadMegabytes(bool isPremium) =>
        isPremium
            ? _premiumOptions.MaximumAudioUploadMegabytes
            : _options.MaximumAudioUploadMegabytes;

    public async Task<ProcessedMedia> ProcessContentBlockAsync(
        IFormFile file,
        ContentBlockType blockType,
        bool isPremium,
        CancellationToken cancellationToken = default) =>
        blockType switch
        {
            ContentBlockType.Image => await ProcessImageAsync(file, isPremium, cancellationToken),
            ContentBlockType.Audio => await ProcessAudioAsync(file, isPremium, cancellationToken),
            _ => throw new MediaUploadException("InvalidMediaFile")
        };

    public async Task<ProcessedMedia> ProcessImageAsync(
        IFormFile file,
        bool isPremium = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file, "image/", "InvalidImageFile");
        var original = await ReadAsync(file, cancellationToken);

        try
        {
            using var encodedData = SKData.CreateCopy(original);
            using var codec = SKCodec.Create(encodedData) ??
                throw new MediaUploadException("InvalidImageFile");

            var isGif = codec.EncodedFormat == SKEncodedImageFormat.Gif;
            var maximumUploadMegabytes = isGif
                ? MaximumGifUploadMegabytes(isPremium)
                : MaximumImageUploadMegabytes(isPremium);
            var originalMedia = Original(file, original);

            if (isGif)
            {
                EnsureSize(originalMedia, maximumUploadMegabytes);
            }

            if (isGif && codec.FrameCount > 1)
            {
                var loopSafeGif = NormalizeAnimatedGifLoop(original);
                return EnsureSize(
                    new ProcessedMedia(
                        loopSafeGif,
                        "image/gif",
                        SafeFileName(file)),
                    maximumUploadMegabytes);
            }

            using var decoded = SKBitmap.Decode(original) ??
                throw new MediaUploadException("InvalidImageFile");
            var needsResize = decoded.Width > _options.MaximumImageWidth ||
                              decoded.Height > _options.MaximumImageHeight;
            var hasTransparency = decoded.Pixels.Any(pixel => pixel.Alpha == 0);
            var shouldConvertToJpeg = !isPremium &&
                                      _options.ConvertOpaqueImagesToJpeg &&
                                      codec.EncodedFormat != SKEncodedImageFormat.Jpeg &&
                                      codec.FrameCount <= 1 &&
                                      !hasTransparency;

            if (!needsResize && !shouldConvertToJpeg)
            {
                return EnsureSize(originalMedia, maximumUploadMegabytes);
            }

            SKBitmap? resized = null;
            var image = decoded;
            if (needsResize)
            {
                var scale = Math.Min(
                    (double)_options.MaximumImageWidth / decoded.Width,
                    (double)_options.MaximumImageHeight / decoded.Height);
                var width = Math.Max(1, (int)Math.Round(decoded.Width * scale));
                var height = Math.Max(1, (int)Math.Round(decoded.Height * scale));
                resized = Resize(decoded, width, height);
                image = resized;
            }

            try
            {
                var outputFormat = shouldConvertToJpeg
                    ? SKEncodedImageFormat.Jpeg
                    : codec.EncodedFormat;
                var quality = isPremium ? 100 : _options.JpegQuality;
                using var outputImage = SKImage.FromBitmap(image);
                using var output = outputImage.Encode(outputFormat, quality) ??
                    throw new MediaUploadException("InvalidImageFile");
                var converted = output.ToArray();
                var result = !needsResize && converted.Length >= original.Length
                    ? originalMedia
                    : shouldConvertToJpeg
                    ? new ProcessedMedia(
                        converted,
                        "image/jpeg",
                        Path.ChangeExtension(SafeFileName(file), ".jpg"))
                    : new ProcessedMedia(converted, file.ContentType, SafeFileName(file));
                return EnsureSize(result, maximumUploadMegabytes);
            }
            finally
            {
                resized?.Dispose();
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new MediaUploadException("InvalidImageFile");
        }
    }

    public async Task<ProcessedMedia> ProcessAudioAsync(
        IFormFile file,
        bool isPremium = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file, "audio/", "InvalidAudioFile");
        if (isPremium || !_options.ConvertAudioToMp3 ||
            IsMp3(file))
        {
            return EnsureSize(
                Original(file, await ReadAsync(file, cancellationToken)),
                MaximumAudioUploadMegabytes(isPremium));
        }

        var inputPath = Path.Combine(Path.GetTempPath(), $"badwolf-audio-{Guid.NewGuid():N}.input");
        var outputPath = Path.ChangeExtension(inputPath, ".mp3");
        try
        {
            await using (var input = new FileStream(
                inputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true))
            {
                await file.CopyToAsync(input, cancellationToken);
            }
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegExecutablePath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add("-nostdin");
            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(inputPath);
            process.StartInfo.ArgumentList.Add("-vn");
            process.StartInfo.ArgumentList.Add("-codec:a");
            process.StartInfo.ArgumentList.Add("libmp3lame");
            process.StartInfo.ArgumentList.Add("-b:a");
            process.StartInfo.ArgumentList.Add($"{_options.Mp3BitrateKbps}k");
            process.StartInfo.ArgumentList.Add("-map_metadata");
            process.StartInfo.ArgumentList.Add("-1");
            process.StartInfo.ArgumentList.Add(outputPath);

            try
            {
                process.Start();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                throw new MediaUploadException("AudioConversionUnavailable");
            }

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }
            _ = await errorTask;
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new MediaUploadException("InvalidAudioFile");
            }

            var converted = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return EnsureSize(
                new ProcessedMedia(
                    converted,
                    "audio/mpeg",
                    Path.ChangeExtension(SafeFileName(file), ".mp3")),
                MaximumAudioUploadMegabytes(isPremium));
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    internal static byte[] NormalizeAnimatedGifLoop(byte[] gif)
    {
        if (!TryFindLastGifFrameGraphicControlExtension(
                gif,
                out var frameCount,
                out var packedFieldOffset) ||
            frameCount <= 1 ||
            packedFieldOffset < 0)
        {
            return gif;
        }

        const int disposalMethodMask = 0x1C;
        const int restoreToBackground = 2;
        const int doNotDispose = 1;

        var packedField = gif[packedFieldOffset];
        var disposalMethod =
            (packedField & disposalMethodMask) >> 2;
        if (disposalMethod != restoreToBackground)
        {
            return gif;
        }

        var result = (byte[])gif.Clone();
        result[packedFieldOffset] = (byte)(
            (packedField & ~disposalMethodMask) |
            (doNotDispose << 2));
        return result;
    }

    private static bool TryFindLastGifFrameGraphicControlExtension(
        byte[] data,
        out int frameCount,
        out int packedFieldOffset)
    {
        frameCount = 0;
        packedFieldOffset = -1;

        if (data.Length < 13 ||
            data[0] != (byte)'G' ||
            data[1] != (byte)'I' ||
            data[2] != (byte)'F' ||
            data[3] != (byte)'8' ||
            (data[4] != (byte)'7' && data[4] != (byte)'9') ||
            data[5] != (byte)'a')
        {
            return false;
        }

        var offset = 13;
        var logicalScreenPackedField = data[10];
        if ((logicalScreenPackedField & 0x80) != 0)
        {
            var colorCount = 1 << ((logicalScreenPackedField & 0x07) + 1);
            var colorTableBytes = 3 * colorCount;
            if (offset + colorTableBytes > data.Length)
            {
                return false;
            }
            offset += colorTableBytes;
        }

        var pendingGraphicControlPackedFieldOffset = -1;
        while (offset < data.Length)
        {
            var introducer = data[offset];
            switch (introducer)
            {
                case 0x3B:
                    return true;

                case 0x21:
                    if (offset + 2 > data.Length)
                    {
                        return false;
                    }

                    var extensionLabel = data[offset + 1];
                    offset += 2;
                    if (extensionLabel == 0xF9)
                    {
                        if (offset + 6 > data.Length ||
                            data[offset] != 4 ||
                            data[offset + 5] != 0)
                        {
                            return false;
                        }

                        pendingGraphicControlPackedFieldOffset = offset + 1;
                        offset += 6;
                    }
                    else
                    {
                        if (!SkipGifSubBlocks(data, ref offset))
                        {
                            return false;
                        }

                        if (extensionLabel == 0x01)
                        {
                            pendingGraphicControlPackedFieldOffset = -1;
                        }
                    }
                    break;

                case 0x2C:
                    if (offset + 10 > data.Length)
                    {
                        return false;
                    }

                    var imagePackedField = data[offset + 9];
                    offset += 10;
                    if ((imagePackedField & 0x80) != 0)
                    {
                        var colorCount = 1 << ((imagePackedField & 0x07) + 1);
                        var colorTableBytes = 3 * colorCount;
                        if (offset + colorTableBytes > data.Length)
                        {
                            return false;
                        }
                        offset += colorTableBytes;
                    }

                    if (offset >= data.Length)
                    {
                        return false;
                    }

                    offset++;
                    if (!SkipGifSubBlocks(data, ref offset))
                    {
                        return false;
                    }

                    frameCount++;
                    packedFieldOffset = pendingGraphicControlPackedFieldOffset;
                    pendingGraphicControlPackedFieldOffset = -1;
                    break;

                default:
                    return false;
            }
        }

        return false;
    }

    private static bool SkipGifSubBlocks(byte[] data, ref int offset)
    {
        while (offset < data.Length)
        {
            var blockLength = data[offset++];
            if (blockLength == 0)
            {
                return true;
            }

            if (offset + blockLength > data.Length)
            {
                return false;
            }
            offset += blockLength;
        }

        return false;
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        var result = new SKBitmap(
            width,
            height,
            SKColorType.Bgra8888,
            source.AlphaType);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        canvas.Flush();
        return result;
    }

    private static void ValidateFile(
        IFormFile file,
        string contentTypePrefix,
        string invalidResourceKey)
    {
        if (file.Length <= 0 ||
            !file.ContentType.StartsWith(contentTypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new MediaUploadException(invalidResourceKey);
        }
    }

    private static ProcessedMedia EnsureSize(ProcessedMedia media, int maximumMegabytes)
    {
        if (media.Data.LongLength > maximumMegabytes * 1024L * 1024L)
        {
            throw new MediaUploadException("FileSizeLimitExceeded", maximumMegabytes);
        }

        return media;
    }

    private static async Task<byte[]> ReadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    private static ProcessedMedia Original(IFormFile file, byte[] data) =>
        new(data, file.ContentType, SafeFileName(file));

    private static bool IsMp3(IFormFile file) =>
        string.Equals(file.ContentType, "audio/mpeg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(file.ContentType, "audio/mp3", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(file.FileName), ".mp3", StringComparison.OrdinalIgnoreCase);

    private static string SafeFileName(IFormFile file) =>
        Path.GetFileName(file.FileName);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed record ProcessedMedia(byte[] Data, string ContentType, string FileName);

public sealed class MediaUploadException(
    string resourceKey,
    params object[] resourceArguments) : Exception(resourceKey)
{
    public string ResourceKey { get; } = resourceKey;
    public object[] ResourceArguments { get; } = resourceArguments;
}
