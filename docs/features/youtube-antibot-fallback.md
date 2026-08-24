# YouTube anti-bot playback fallback

BadWolfQuiz keeps the privacy-enhanced `youtube-nocookie.com` embed as the primary YouTube playback path. This preserves the existing pre-play placeholder, timestamp handling, autoplay behavior, managed fullscreen presentation, timer integration, and media coordination.

## Block detection

YouTube does not expose the text of the `Sign in to confirm you're not a bot` screen to the parent page, and that screen does not have a dedicated documented IFrame Player API error code. BadWolfQuiz therefore uses a best-effort health check:

- explicit YouTube player errors are treated as a failed playback attempt;
- a launched player that does not reach a usable player state within the configured startup watchdog is treated as blocked;
- a healthy player state cancels the watchdog.

When playback is considered blocked, the current player is paused before its iframe is hidden or replaced. A dedicated blocked state is shown instead of exposing YouTube metadata or returning to the normal pre-play placeholder.

## Alternative playback path

The blocked-state action launches the same video through the standard `www.youtube.com/embed/...` endpoint instead of retrying the identical privacy-enhanced iframe. The alternative URL preserves the video id and existing query parameters, including `start`, and adds the normal JavaScript API, autoplay, and page `origin` parameters required by the shared player flow.

For managed playback, the fullscreen presentation stays open while the blocked state is visible. The alternative attempt then uses the same normal player-health/error handling as any real YouTube playback attempt.

The alternative embed is still a YouTube-hosted player. It is a second supported playback path, not a guarantee that YouTube will never request verification.

## Debug simulation

`src/BadWolfQuiz.Web/appsettings.json` contains:

```json
"SimulateYouTubeAntiBot": false
```

Set it to `true` only for deterministic fallback testing. In this mode, the primary `youtube-nocookie.com` attempt is forced through the production blocked-state path. The subsequent standard `www.youtube.com` alternative is **not** force-failed, so it can be verified as a real playback path.

Expected manual test flow:

1. Set `SimulateYouTubeAntiBot` to `true` and restart the web application.
2. Start a YouTube video from any supported quiz surface.
3. Confirm that the primary attempt changes to the dedicated blocked state.
4. Use **Play another way / Відтворити іншим способом**.
5. Confirm that the standard YouTube embed plays normally and that no audio from the failed primary player continues in the background.
6. Restore `SimulateYouTubeAntiBot` to `false` after testing.

The shared fallback manager is used by gameplay question/answer content, editor previews, Answer Key, closed-question/answer review, and other surfaces that use the shared YouTube renderer.

## Release

Introduced in Web `1.22.34` (`web-v1.22.34`).
