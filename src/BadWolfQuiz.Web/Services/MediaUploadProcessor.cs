using System.Diagnostics;
using BadWolfQuiz.Web.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BadWolfQuiz.Web.Services;

public sealed class MediaUploadProcessor(IOptions<MediaProcessingOptions> options)
{
    private readonly MediaProcessingOptions _options = options.Value;

    public int MaximumImageUploadMegabytes =>
        _options.MaximumImageUploadMegabytes;

    public int MaximumAudioUploadMegabytes =>
        _options.MaximumAudioUploadMegabytes;

    public async Task<ProcessedMedia> ProcessContentBlockAsync(
        IFormFile file,
        ContentBlockType blockType,
        bool skipConversion,
        CancellationToken cancellationToken = default) =>
        blockType switch
        {
            ContentBlockType.Image => await ProcessImageAsync(file, skipConversion, cancellationToken),
            ContentBlockType.Audio => await ProcessAudioAsync(file, skipConversion, cancellationToken),
            _ => throw new MediaUploadException("InvalidMediaFile")
        };

    public async Task<ProcessedMedia> ProcessImageAsync(
        IFormFile file,
        bool skipConversion = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file, "image/", "InvalidImageFile");
        var original = await ReadAsync(file, cancellationToken);

        try
        {
            using var image = Image.Load<Rgba32>(original);
            var format = Image.DetectFormat(original);
            var needsResize = image.Width > _options.MaximumImageWidth ||
                              image.Height > _options.MaximumImageHeight;
            var hasTransparency = HasFullyTransparentPixel(image);
            var shouldConvertToJpeg = !skipConversion &&
                                      _options.ConvertOpaqueImagesToJpeg &&
                                      !IsJpeg(file) &&
                                      image.Frames.Count == 1 &&
                                      !hasTransparency;

            if (!needsResize && !shouldConvertToJpeg)
            {
                return EnsureSize(
                    Original(file, original),
                    _options.MaximumImageUploadMegabytes);
            }

            if (needsResize)
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(
                        _options.MaximumImageWidth,
                        _options.MaximumImageHeight)
                }));
            }

            await using var output = new MemoryStream();
            if (shouldConvertToJpeg || IsJpeg(file))
            {
                if (shouldConvertToJpeg)
                {
                    image.Mutate(context => context.BackgroundColor(Color.White));
                }
                await image.SaveAsJpegAsync(
                    output,
                    new JpegEncoder { Quality = _options.JpegQuality },
                    cancellationToken);
            }
            else
            {
                await image.SaveAsync(output, format, cancellationToken);
            }

            var converted = output.ToArray();
            var result = !needsResize && converted.Length >= original.Length
                ? Original(file, original)
                : shouldConvertToJpeg
                ? new ProcessedMedia(
                    converted,
                    "image/jpeg",
                    Path.ChangeExtension(SafeFileName(file), ".jpg"))
                : new ProcessedMedia(converted, file.ContentType, SafeFileName(file));
            return EnsureSize(result, _options.MaximumImageUploadMegabytes);
        }
        catch (UnknownImageFormatException)
        {
            throw new MediaUploadException("InvalidImageFile");
        }
        catch (InvalidImageContentException)
        {
            throw new MediaUploadException("InvalidImageFile");
        }
    }

    public async Task<ProcessedMedia> ProcessAudioAsync(
        IFormFile file,
        bool skipConversion = false,
        CancellationToken cancellationToken = default)
    {
        ValidateFile(file, "audio/", "InvalidAudioFile");
        if (skipConversion || !_options.ConvertAudioToMp3 ||
            IsMp3(file))
        {
            return EnsureSize(
                Original(file, await ReadAsync(file, cancellationToken)),
                _options.MaximumAudioUploadMegabytes);
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
                _options.MaximumAudioUploadMegabytes);
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private static bool HasFullyTransparentPixel(Image<Rgba32> image)
    {
        var result = false;
        foreach (var frame in image.Frames)
        {
            frame.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height && !result; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        if (row[x].A == 0)
                        {
                            result = true;
                            break;
                        }
                    }
                }
            });
            if (result)
            {
                break;
            }
        }
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

    private static bool IsJpeg(IFormFile file) =>
        string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(file.FileName), ".jpg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(file.FileName), ".jpeg", StringComparison.OrdinalIgnoreCase);

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
