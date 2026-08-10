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

## Packages

- `MaterialDesignThemes` 5.3.2
- `SSH.NET` 2025.1.0

## Build

```powershell
dotnet restore
dotnet build
```

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
