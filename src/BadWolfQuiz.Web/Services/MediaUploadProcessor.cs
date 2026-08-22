using System.ComponentModel;
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
                var normalized = await NormalizeAnimatedGifAsync(
                    originalMedia,
                    codec,
                    maximumUploadMegabytes,
                    cancellationToken);
                return EnsureSize(normalized, maximumUploadMegabytes);
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
                exception is InvalidOperationException or Win32Exception)
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

    private async Task<ProcessedMedia> NormalizeAnimatedGifAsync(
        ProcessedMedia originalMedia,
        SKCodec codec,
        int maximumUploadMegabytes,
        CancellationToken cancellationToken)
    {
        var lastVisibleFrameIndex = GetLastVisibleGifFrameIndex(codec);
        var inputPath = Path.Combine(
            Path.GetTempPath(),
            $"badwolf-gif-{Guid.NewGuid():N}.gif");
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"badwolf-gif-{Guid.NewGuid():N}-normalized.gif");

        try
        {
            await File.WriteAllBytesAsync(
                inputPath,
                originalMedia.Data,
                cancellationToken);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegExecutablePath,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("-nostdin");
            process.StartInfo.ArgumentList.Add("-y");
            process.StartInfo.ArgumentList.Add("-i");
            process.StartInfo.ArgumentList.Add(inputPath);
            process.StartInfo.ArgumentList.Add("-filter_complex");
            process.StartInfo.ArgumentList.Add(
                $"[0:v]select=lte(n\\,{lastVisibleFrameIndex})[selected];" +
                "[selected]split[palette_source][video_source];" +
                "[palette_source]palettegen=reserve_transparent=1[palette];" +
                "[video_source][palette]paletteuse=dither=sierra2_4a");
            process.StartInfo.ArgumentList.Add("-loop");
            process.StartInfo.ArgumentList.Add("0");
            process.StartInfo.ArgumentList.Add("-gifflags");
            process.StartInfo.ArgumentList.Add("-offsetting-transdiff");
            process.StartInfo.ArgumentList.Add("-map_metadata");
            process.StartInfo.ArgumentList.Add("-1");
            process.StartInfo.ArgumentList.Add(outputPath);

            try
            {
                process.Start();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or Win32Exception)
            {
                return originalMedia;
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
                return originalMedia;
            }

            var normalized = await File.ReadAllBytesAsync(
                outputPath,
                cancellationToken);
            if (normalized.Length == 0 ||
                normalized.LongLength > maximumUploadMegabytes * 1024L * 1024L)
            {
                return originalMedia;
            }

            using var normalizedData = SKData.CreateCopy(normalized);
            using var normalizedCodec = SKCodec.Create(normalizedData);
            if (normalizedCodec is null ||
                normalizedCodec.EncodedFormat != SKEncodedImageFormat.Gif ||
                normalizedCodec.FrameCount <= 1)
            {
                return originalMedia;
            }

            return new ProcessedMedia(
                normalized,
                "image/gif",
                originalMedia.FileName);
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    internal static int GetLastVisibleGifFrameIndex(SKCodec codec)
    {
        var frameInfo = codec.FrameInfo;
        var lastFrameIndex = Math.Min(codec.FrameCount, frameInfo.Length) - 1;
        if (lastFrameIndex <= 0)
        {
            return Math.Max(0, lastFrameIndex);
        }

        while (lastFrameIndex > 0 &&
               IsGifFrameFullyTransparent(codec, lastFrameIndex))
        {
            lastFrameIndex--;
        }

        return lastFrameIndex;
    }

    private static bool IsGifFrameFullyTransparent(SKCodec codec, int frameIndex)
    {
        var info = new SKImageInfo(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var bitmap = new SKBitmap(
            info.Width,
            info.Height,
            info.ColorType,
            info.AlphaType);
        bitmap.Erase(SKColors.Transparent);

        var result = codec.GetPixels(
            info,
            bitmap.GetPixels(),
            bitmap.RowBytes,
            new SKCodecOptions(frameIndex, -1));
        if (result != SKCodecResult.Success)
        {
            return false;
        }

        return bitmap.Pixels.All(pixel => pixel.Alpha == 0);
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
