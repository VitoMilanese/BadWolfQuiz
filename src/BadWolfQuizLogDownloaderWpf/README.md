# BadWolfQuiz Log Downloader — WPF

WPF rewrite of the BadWolfQuiz log utility with Material Design in XAML Toolkit.

Features:

- SSH download from Ubuntu/systemd via `journalctl`
- cancelable operations
- open local `.log` / `.txt`
- live `journalctl -f` over SSH
- optional live recording to file
- reverse chronological display (newest first)
- fast paging: 100 / 250 / 500 / 1000 entries per page
- log-level coloring and filtering
- multi-line log entries stay grouped
- themes: Light, Dark, Matrix, Obsidian, Ukrainian, UPA, Italian, Warm Parchment, Mint Fog, LGBTQ+
- supplied Bad Wolf logger artwork as application/window icon
- independent semantic product version displayed in the window title bar

## Packages

- `MaterialDesignThemes` 5.3.2
- `SSH.NET` 2025.1.0

## Build

```powershell
dotnet restore
dotnet build
```

## Versioning

`BadWolfQuizLogDownloaderWpf` has its own semantic version and release lifecycle, independent from `BadWolfQuiz.Web`.

The product version is defined once in `BadWolfQuizLogDownloaderWpf.csproj` through the `<Version>` property and is read from the built assembly at runtime. The main window title therefore displays the actual product version, for example:

`BadWolfQuiz Log Downloader v1.0.0`

Use PATCH releases for compatible fixes, MINOR releases for backwards-compatible features, and MAJOR releases for major/breaking release milestones. Downloader release tags should be distinguishable from web application release tags when both products are published from this repository.

## Configuration

Edit `appsettings.json` next to the executable.

```json
{
  "Host": "quiz.example.com",
  "Port": 22,
  "Username": "ubuntu",
  "Password": "your-password",
  "ServiceName": "badwolfquiz.service",
  "UseSudo": true,
  "OutputDirectory": "Logs"
}
```

The password is intentionally read from `appsettings.json`. Do not commit a real password.


## WPF icon loading

`logger.ico` is copied beside the executable and loaded explicitly at runtime.
This avoids WPF treating `Icon="logger.ico"` as a pack-resource URI and throwing
a `XamlParseException` when the icon is not compiled as a WPF Resource.


## Material Design button style fix

`MaterialDesignFilledButton` is not a valid resource key in the configured MaterialDesignThemes toolkit resources. The two primary buttons now use `MaterialDesignRaisedButton`, while outlined buttons continue to use `MaterialDesignOutlinedButton`.
