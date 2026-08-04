# Reverse-proxy upload limits

Quiz question and answer forms can contain image or audio uploads. Nginx rejects a request before ASP.NET Core sees it when the request exceeds `client_max_body_size`; the browser then receives `413 Request Entity Too Large` and no Razor Page handler runs.

The production Nginx `server` block must therefore allow the same workloads as the application. This project permits quiz package imports and media editor forms up to approximately 1.1 GiB. A compatible Nginx setting is:

```nginx
server {
    server_name badwolf.buzz;
    client_max_body_size 1200M;

    # Existing TLS and proxy configuration...
}
```

After changing the configuration, validate and reload it:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

Nginx may buffer large request bodies to disk. Keep adequate free space in its temporary directory and retain the application's per-file validation; raising the proxy limit does not make every uploaded file valid.

Quiz media is currently stored in SQLite so a database copy and a `.bwquiz` export remain self-contained. Moving media to the filesystem would reduce database size, but it would require coordinated file cleanup, authorization-aware serving, migration, and backups of both the database and the media directory. It should be implemented as a separate storage migration rather than as an upload-limit workaround.

## Media processing

Per-file limits and conversion settings are configured in `appsettings.json`:

```json
"MediaProcessing": {
  "MaximumImageUploadMegabytes": 10,
  "MaximumAudioUploadMegabytes": 10,
  "MaximumImageWidth": 3840,
  "MaximumImageHeight": 2160,
  "ConvertAudioToMp3": true,
  "Mp3BitrateKbps": 128,
  "FfmpegExecutablePath": "ffmpeg",
  "ConvertOpaqueImagesToJpeg": true,
  "JpegQuality": 85
}
```

Image and audio size limits are checked after resizing, compression, or conversion. Images wider or taller than the configured dimensions are resized proportionally without upscaling.

Audio conversion requires FFmpeg on the application host. On Ubuntu it can be installed with:

```bash
sudo apt update
sudo apt install ffmpeg
ffmpeg -version
```

`FfmpegExecutablePath` may be an executable name available through `PATH` or an absolute path. MP3 uploads are kept as-is; other accepted audio formats are converted to MP3. Single-frame images without fully transparent pixels are encoded as JPEG only when the JPEG is smaller than the original. Existing JPEG files, animated images, and images containing fully transparent pixels are preserved.

Premium hosts can be configured by copying their database ID from the Settings page:

```json
"PremiumHosts": {
  "MaximumImageUploadMegabytes": 50,
  "MaximumAudioUploadMegabytes": 100,
  "HostIds": [
    "host-id-from-settings"
  ]
}
```

Premium uploads retain their original image or audio format and skip optional JPEG/MP3 conversion. Their separate, higher file-size limits still apply, and oversized images are proportionally resized in their original format.
