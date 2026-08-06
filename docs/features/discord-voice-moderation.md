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

## Host setup

Open **Discord voice** from the authenticated menu, connect the host's Discord
account, install the bot if necessary, and select a server and standard voice
channel. Only servers where the user can manage the server and where the bot is
present are offered. The page reports bot, server, channel, and permission health.

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
automatic reason.

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
