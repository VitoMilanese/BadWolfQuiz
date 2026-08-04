using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadWolfQuiz.Web.Services;

internal sealed class ExternalByteArrayJsonConverter(string blobDirectory)
    : JsonConverter<byte[]>
{
    private const string BlobPropertyName = "$blob";
    private readonly HashSet<string> _referencedBlobs =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> ReferencedBlobs => _referencedBlobs;

    public void BeginWrite() => _referencedBlobs.Clear();

    public override byte[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetBytesFromBase64();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Invalid external binary value.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty(BlobPropertyName, out var blobElement))
        {
            throw new JsonException("The external binary reference is missing.");
        }

        var blobName = blobElement.GetString();
        if (!IsValidBlobName(blobName))
        {
            throw new JsonException("The external binary reference is invalid.");
        }

        var path = Path.Combine(blobDirectory, blobName + ".bin");
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new JsonException("The external binary data is unavailable.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        byte[] value,
        JsonSerializerOptions options)
    {
        var blobName = Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
        Directory.CreateDirectory(blobDirectory);
        var path = Path.Combine(blobDirectory, blobName + ".bin");
        if (!File.Exists(path))
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllBytes(temporaryPath, value);
            File.Move(temporaryPath, path, overwrite: true);
        }

        _referencedBlobs.Add(blobName);
        writer.WriteStartObject();
        writer.WriteString(BlobPropertyName, blobName);
        writer.WriteEndObject();
    }

    private static bool IsValidBlobName(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            (character >= '0' && character <= '9') ||
            (character >= 'a' && character <= 'f'));
}
