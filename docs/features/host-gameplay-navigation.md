# Host Gameplay Navigation

## Purpose

The host gameplay UI keeps long-lived game resources mounted while the changing gameplay state is updated asynchronously. Opening questions, judging answers, previewing resolved content, moving between rounds, and progressing through the Final Question should not normally replace the whole browser document.

The server-rendered Razor pages remain the source of truth. Client-side navigation fetches fresh server HTML and mounts only the gameplay content that changed instead of duplicating presentation rules in JavaScript.

## Persistent host shell

During a running game, the host page keeps the board shell, SignalR connection, player/sidebar state, header controls, media coordination, and other long-lived client state alive across gameplay transitions.

The replaceable gameplay region is rooted at `[data-host-gameplay-view]`. The normal Lobby navigation layer fetches the current Lobby with `X-Requested-With: XMLHttpRequest`, parses the server-rendered response, and applies the relevant state inside that region.

The board itself remains mounted whenever possible. When the active round does not change, question tiles are synchronized from the latest server markup. When the round changes, the category/question grid, round heading, and Previous/Next Round tool visibility are synchronized from the new Lobby response while the surrounding host shell stays mounted.

If the persistent board and the server-rendered board claim the same round but contain different question-id sets, the whole grid is rebuilt from the fresh server markup. This is a recovery path for incomplete client board state and prevents a resolved question cell from disappearing until a manual refresh.

## Header gameplay controls

The running-game header keeps the gameplay controls in a single horizontal row with consistent spacing: Tools, join QR, Discord settings when available, manual Discord mute/unmute when available, and the player join lock.

Mute, unmute, and lock use the same compact square control sizing and icon scale as the Discord settings button. The lock control keeps its existing state-dependent background, and its player-admission menu opens downward from the header.

Manual mute/unmute controls are rendered only while Discord voice control is configured, enabled, connected, and ready. If the game page was loaded before Discord became ready, the host does not require a browser refresh: when readiness changes, it fetches fresh server-rendered Lobby markup, hydrates the missing mute/unmute buttons into the existing header, and adopts the fresh anti-forgery token before enabling manual Discord operations. Dynamically hydrated buttons use the same delegated command path as initially rendered controls.

If Discord later becomes unavailable, already-mounted mute/unmute controls are hidden immediately. The lock control remains independent from Discord and continues to work even when the Discord settings button is not rendered.

Manual Discord operation status messages are temporary and clear automatically after a short delay. Discord mute/media requests explicitly target the Lobby handlers so they remain valid when persistent host navigation leaves another host-flow URL in the browser address bar.

## Supported navigation flows

The in-place host flow covers the common running-game transitions, including:

- selecting an available question;
- judging correct/incorrect answers and resolving a question with no correct answer;
- showing the correct answer and returning to the board;
- opening a resolved question preview, switching between its question and answer, and returning to the board;
- wager submission and other same-Lobby gameplay forms;
- player remove/block actions that do not require leaving the game flow;
- regular round summaries, round/category intros, forced Previous/Next Round transitions, and returns to unfinished rounds;
- forced and natural entry into the Final Question transition;
- Final Wagering, Final Answering, Final Judging, and final standings updates.

Responses that cannot be represented safely inside the current host shell still use normal browser navigation as a failure or unsupported-boundary fallback.

## First-round bootstrap

The first round starts before the persistent running-game Lobby shell exists, so it uses `RoundIntro.cshtml` as a standalone presentation.

Within that first intro, category frames are loaded asynchronously and replace only `[data-game-intro-page]`. **Skip** and **Start game** submit through `fetch`. After the server redirects to the running Lobby, the returned Lobby header and main gameplay content are mounted into the existing browser document rather than replacing the document itself.

The existing global JavaScript listeners therefore survive the transition. Lobby-specific scripts are initialized for the newly mounted shell and a `badwolf:host-shell-mounted` event activates bindings that could not initialize while the standalone intro was present. Normal form navigation remains the fallback if the soft transition fails.

## Round intros and transition protection

Later round intros and the Final Question transition can be rendered inside the existing host gameplay region. Mounting one of these external-flow presentations invalidates stale in-flight Lobby updates so an older response cannot overwrite the intro.

SignalR events that would normally request a gameplay refresh also avoid replacing an active intro. This keeps the transition presentation stable until it completes or the host explicitly leaves it.

Duplicate refreshes of the same round-summary or final-results podium do not recreate the existing podium DOM. Keeping the same leaderboard mounted prevents entrance animations from restarting when a repeated live update reports the same visible state.

## Resolved question previews

Resolved previews reuse the same server-rendered content partial as gameplay. The preview heading includes the state in the category/value line, for example `Category — 200 — Question` or `Category — 200 — Answer`, so a separate Question/Answer eyebrow does not consume presentation space.

Four-clue question previews preserve the same horizontal clue layout used during gameplay. Resolved question and answer content is vertically centered within the available presentation area.

## YouTube and media behavior

YouTube playback is managed for the lifetime of the browser document rather than only for frames present during initial page load. A `MutationObserver` discovers YouTube players inserted by host partial navigation and by the regular/final-question editor previews.

When a tracked YouTube video starts:

- the player expands to the full-viewport presentation;
- other YouTube players and native audio/video elements are paused;
- a running question timer is paused through the lightweight timer command path rather than by replacing the gameplay view;
- the expanded presentation collapses automatically when playback ends.

Escape or the close control leaves only the expanded presentation. Playback continues and the timer remains paused until the video actually pauses, stops, ends, or is removed. The paired Escape `keyup` is consumed so the same key press cannot also trigger question-editor or final-question-editor Escape navigation.

If the host manually resumes the timer while the video is still playing, the YouTube manager relinquishes its pending automatic resume. When playback later ends, it does not submit a second Resume command and the already-running timer continues normally.

## Live updates and commands

Host commands and SignalR updates share the same persistent shell. A command in flight defers refresh work that would conflict with the command response, avoiding races where a live state broadcast replaces content before the HTTP request finishes.

Timer pause/resume commands stay on their lightweight command handler and are excluded from the general gameplay-view replacement path. Timer state itself is updated from SignalR without remounting the current question or media player.

## Failure model

The asynchronous navigation layer is an optimization over the authoritative server-rendered flow, not a separate source of game state. If a fetch fails, a response is unsupported, authentication redirects outside the expected route, or the client cannot safely mount the response, normal navigation/reload remains the fallback. Reloading the page must reconstruct the same authoritative state from the server.
