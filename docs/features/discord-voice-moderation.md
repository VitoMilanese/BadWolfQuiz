# Discord Voice Moderation

## Purpose

Discord voice moderation lets an authenticated host control server mute for the
participants in one selected Discord voice channel. A single Discord application
and bot installation serve every BadWolfQuiz host. Each host stores an independent
Discord identity, server, channel, and automatic-media preference.

The host's Discord user ID is always excluded. If the host is not present in the
selected voice channel, every non-bot participant is targeted.

## Discord application setup

Create a Discord application and bot, then enable the `Guilds` and
`Guild Voice States` gateway intents. Register the exact OAuth callback URL used
by the deployment. Configure secrets through environment variables or another
server-side secret provider; never commit production values.

```text
DiscordIntegration__Enabled=true
DiscordIntegration__ClientId=<application id>
DiscordIntegration__ClientSecret=<oauth client secret>
DiscordIntegration__BotToken=<bot token>
DiscordIntegration__CallbackUrl=https://quiz.example.com/Admin/Settings/Discord?handler=Callback
DiscordIntegration__AutomaticMuteTimeoutMinutes=15
DiscordIntegration__MaximumParallelOperations=4
```

The bot install flow requests View Channel, Connect, and Mute Members. The bot
does not join the voice channel: it observes voice states through the gateway and
updates guild-member mute state through Discord's API.

## Linux production configuration

ASP.NET Core maps double underscores in environment-variable names to nested
configuration keys. Production credentials should therefore be registered as
`DiscordIntegration__*` variables on the application process. Do not put the
client secret or bot token in the repository, deployment script, shell history,
or a world-readable file.

### systemd

For a `badwolfquiz.service` deployment, keep the values in a root-owned
environment file:

```bash
sudo install -d -m 750 /etc/badwolfquiz
sudoedit /etc/badwolfquiz/discord.env
sudo chmod 600 /etc/badwolfquiz/discord.env
sudo chown root:root /etc/badwolfquiz/discord.env
```

The contents of `/etc/badwolfquiz/discord.env` should be:

```text
DiscordIntegration__Enabled=true
DiscordIntegration__ClientId=APPLICATION_ID
DiscordIntegration__ClientSecret=CLIENT_SECRET
DiscordIntegration__BotToken=BOT_TOKEN
DiscordIntegration__CallbackUrl=https://quiz.example.com/Admin/Settings/Discord?handler=Callback
DiscordIntegration__AutomaticMuteTimeoutMinutes=15
DiscordIntegration__MaximumParallelOperations=4
```

Connect that file to the service with a systemd override:

```bash
sudo systemctl edit badwolfquiz.service
```

```ini
[Service]
EnvironmentFile=/etc/badwolfquiz/discord.env
```

Apply the change and verify that the service starts:

```bash
sudo systemctl daemon-reload
sudo systemctl restart badwolfquiz.service
sudo systemctl status badwolfquiz.service
sudo journalctl -u badwolfquiz.service -n 100 --no-pager
```

Do not print the environment file or inspect the process environment in shared
logs, because both operations can disclose the bot token and OAuth secret.

### Docker Compose

Container deployments can reference the same uncommitted environment file:

```yaml
services:
  badwolfquiz:
    env_file:
      - /etc/badwolfquiz/discord.env
```

Recreate the container after changing a value:

```bash
docker compose up -d --force-recreate badwolfquiz
```

### Temporary shell launch

For a short-lived manual launch, export the variables in the same shell before
starting the application. This is not recommended for permanent hosting because
the values disappear with the session and can leak into shell history.

### OAuth callback and reverse proxy

`DiscordIntegration__CallbackUrl` must use the public HTTPS origin visible to
the host's browser, not the internal Kestrel address or container name. Register
the exact same value under **OAuth2 → Redirects** in the Discord Developer Portal.
The scheme, host, port, path, and `handler=Callback` query parameter must match.

For example, even when Kestrel listens internally on `http://127.0.0.1:5080`, a
deployment behind a reverse proxy should normally use:

```text
https://quiz.example.com/Admin/Settings/Discord?handler=Callback
```

Restart the service or recreate the container after rotating the client secret
or bot token. Update the environment file first, then replace the corresponding
credential in the Discord Developer Portal to keep downtime short.

## Host setup

Open the Discord voice dialog from the global settings page or directly from an
active host game, connect the host's Discord account, install the bot if necessary,
and select a server and standard voice channel. Only servers where the user can
manage the server and where the bot is present are offered. The dialog reports
bot, server, channel, and permission health.

OAuth access tokens and CSRF state are kept only in short-lived server memory.
Only stable Discord IDs, display names, and the automatic-media preference are
stored in the application database.

## Runtime behavior

The game tools menu exposes explicit mute and unmute commands when the connection
is healthy. Commands use AJAX and return target, success, failure, and skipped
counts. One participant failing does not stop the rest of the operation.

Automatic moderation observes native audio/video and YouTube playback on the host
game page. It uses a set of active media elements: the first playback start requests
mute and the last playback stop requests unmute. Pause, ended, error, abort, answer
close, question transition, round completion, and game completion release the
automatic reason. Changing the automatic-mute preference in the Discord dialog is
applied to the open game immediately without reloading it. While media remains
active, the page sends a lightweight status update every minute so the safety
timeout is reserved for abandoned playback sessions.

Manual and automatic requests are independent:

| Manual | Media | Effective state |
|---|---|---|
| off | off | unmuted |
| on | off | muted |
| off | on | muted |
| on | on | muted |

Automatic requests expire after 15 minutes by default. Operations for the same
server and channel are serialized, while member updates use bounded parallelism
and Discord.Net's rate-limit handling.

## Security and diagnostics

All handlers derive the host from the authenticated session and verify ownership
of the active game. Gameplay requests never accept a server or channel ID from
the browser. POST requests use Razor Pages antiforgery validation.

Structured operation logs include host ID, optional game session ID, server and
channel IDs, operation reason, result counts, and elapsed time. Bot tokens, OAuth
codes, access tokens, and participant names are not logged.
