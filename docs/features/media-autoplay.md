# Media autoplay

Question and answer content blocks can opt into automatic playback for native audio and video media. The setting is stored on each content block, so autoplay is explicit per item rather than a global game option.

The regular question editor and final-question editor expose a clearly labeled **Autoplay** option for audio, direct video, and YouTube video blocks. New media blocks start with Autoplay enabled by default; reopening an existing editor restores the value that was actually saved for each block. YouTube blocks use the same per-item setting as other video content.

## Selection rule

When a question or answer becomes active on the host gameplay view, the autoplay controller considers visible Autoplay-enabled Audio, direct Video, and YouTube blocks together in DOM order and starts only the first one. Later Autoplay-enabled media in the same presentation is left untouched and remains available for manual playback.

Native audio/video is started with `HTMLMediaElement.play()`. YouTube placeholders are launched through the explicit autoplay API, including the `autoplay=1` embed URL and YouTube Player API `playVideo()` request, and an autoplay-launched managed YouTube video opens in the same expanded presentation used by manual video launch. Browser autoplay policy still has the final say; a blocked first attempt does not interrupt gameplay and does not fall through to a later media block.

This first-media rule prevents multiple YouTube placeholders from launching or expanding during the same presentation and prevents a later configured media block from replacing the first video's managed presentation.

Once a question or answer presentation is active, later gameplay refreshes with the same media state update only the surrounding DOM path while keeping the live media presentation continuously connected. This keeps a playing native media element or expanded YouTube iframe alive without even a temporary detach while surrounding gameplay controls refresh. Genuinely removed media still follows the normal pause/rewind and teardown lifecycle.

## Four-clue questions

For four-clue questions, unrevealed clue media is excluded from the initial selection. When the question opens, only the first Autoplay-enabled media among the first two visible clues is started.

`QuestionClueRevealed` is the authoritative live reveal notification, so the host updates clue visibility, the stored reveal count, reward state, and the media presentation state in place without a gameplay DOM refresh. A `2 -> 3` transition stops earlier game media and selects only the first Autoplay-enabled media inside clue 3; a `3 -> 4` transition does the same inside clue 4. Earlier visible clues are never rescanned or restarted, and question-to-answer transitions use a distinct autoplay presentation state.

For manual four-clue reveal, the host performs the next-clue media handoff directly in the `+ hint` button click handler before form submission or awaited network work. Existing game media outside the target clue is stopped first, the target clue's previous autoplay-attempt markers are cleared, and then only that clue is evaluated for its first Autoplay-enabled media while the browser still has the host's click activation. The AJAX response and `QuestionClueRevealed` notification reconcile the optimistic local reveal with the authoritative server state; a rejected reveal triggers an authoritative gameplay refresh.

The AJAX response also returns the authoritative reveal count and reveal availability. The host applies that result before flushing any gameplay refresh that arrived while the reveal POST was in flight. Gameplay refreshes carry the current live timer visibility/value/control state into replacement markup so an unrelated refresh does not make the timer flash off and back on.

## Persistence and compatibility

For host rendering, the current saved per-block `Autoplay` value is re-read from the quiz database and overrides the frozen game-snapshot value when that source block still exists. This keeps presentation-only autoplay changes effective in an already-created or restored game session; the snapshot value remains the fallback for content that is no longer available from the editor database.

The autoplay flag is preserved in game snapshots and `.bwquiz` export/import packages. Older packages and existing legacy data that do not contain the flag keep autoplay disabled rather than being silently changed by the editor's default-on behavior for newly created media blocks.

## Validation

Real-host verification confirmed:

- normal Audio/Video/YouTube blocks start when Autoplay is enabled;
- only the first visible configured media is started when several blocks are marked Autoplay;
- four-clue opening selects only the first configured media among clues 1-2;
- `+ hint` stops the previous game media and starts configured media from the newly revealed clue;
- clue 3 and clue 4 are targeted independently without restarting earlier clues;
- the live timer does not flash during clue reveal.

## Release

BadWolfQuiz Web: **1.12.0**

Tag after merge: `web-v1.12.0`.
