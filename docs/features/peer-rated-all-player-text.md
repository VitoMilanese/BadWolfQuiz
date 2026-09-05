# Peer-rated all-player text questions

## Purpose

`AllPlayerPeerRatedText` is a separate all-player question presentation for answers that should be judged by the other players rather than by the host.

Every participating player submits one private text answer. After all required answers exist, the host reviews the submitted answers sequentially. While one answer is on screen, every other participating player rates it from **0 to 5 stars**. The author never rates their own answer.

The presentation uses the existing `QuestionPresentationType` field and therefore does not require a database migration.

## Editor behavior

The Question Editor exposes **All players — peer-rated text** as its own presentation type.

Peer-rated questions are always normal, non-wager questions. Their buzzer is disabled and they are excluded from random wager selection at snapshot/runtime level, even if older or manually edited persisted values contain incompatible settings.

A peer-rated question has **no separate correct answer**. While this presentation is selected, the correct-answer editor and answer-preview action are hidden. The question content is the only authored reference content for this presentation. Runtime/legacy answer surfaces mirror the question content instead of exposing a stored answer.

## Gameplay flow

The host flow deliberately has three separate phases so ratings and score results are not revealed early.

1. The host selects the peer-rated question and the normal question content remains on screen.
2. The current players are captured as the participants for this question and sorted by their current score from lowest to highest. That captured order is used for both voting and the later results pass and is preserved by recovery.
3. The normal buzzer is unavailable for the entire peer-rated lifecycle, including text entry, rating, and the waiting state after a player has submitted a rating.
4. Every participant submits one text answer from their player page.
5. A right-side host panel shows submitted/waiting progress. A missing answer can be recorded as an empty response (`—`).
6. When every active participant has an answer, the answer belonging to the lowest-scoring captured participant is shown first. The original question is displayed directly above that player's answer so the voting context stays visible throughout review.
7. Every participant except that answer's author chooses a rating from 0 to 5 stars. The choice stays local and can be changed until the player presses **Confirm rating**; only that confirmation submits the rating to the server.
8. The right-side host panel shows each required rater only as pending, rated, or AFK/excluded. The exact number of stars is deliberately hidden throughout the voting pass, and no average or awarded points are shown.
9. When all required ratings are present, the host advances to the next answer. The process repeats in captured score order until every eligible answer has been voted on, with the original question remaining above each reviewed answer.
10. After the final voting pass, the host selects **Show results**.
11. Results are then shown in the same sequential order. Each result keeps the original question above the player's answer and shows the average star rating, the awarded points, and the right-side list of who voted and how many stars they gave.
12. Scores are revealed/applied together with each result, so player totals do not expose a result before the host reaches it.
13. The host advances with **Next result** and uses **Return to board** after the final result. Returning forces a fresh host-shell load so the resolved question cannot flash/reappear with legacy buzzer or no-correct-answer controls.

A player who joins after the participant set was captured does not become a required answerer or rater for that question.

## Host layout

Peer-rated host controls are overlays inside the current-question area and do not participate in normal document flow. Nothing is inserted below the question, so the feature does not create an additional page scrollbar merely because the status/rating UI exists.

During the voting and results phases, the host keeps a dedicated **question context** above the currently reviewed answer. That context reuses a clone of the already server-rendered gameplay question instead of maintaining a second question renderer, so text, captions, images, audio, video, YouTube embeds, and existing media URLs stay consistent with the normal question presentation.

The cloned review context disables autoplay-related attributes so entering review does not replay question media unexpectedly. The context is keyed by source question ID and lives outside the frequently rebuilt peer-rating status overlay, which prevents incoming votes or status changes from constantly recreating the question media.

The participant/rater/result-status panel stays on the **right** during all three phases. The client reserves horizontal room for that panel instead of covering the question or reviewed answer. During review, the question context uses the same reserved right boundary and the answer stage is moved below it. If the question content is too tall for the available area, the question context receives its own bounded scrolling region while a minimum visible area is preserved for the reviewed answer.

The layout also detects a visible vertical scrollbar owned by the question area. The right panel is shifted left of that scrollbar with a safety gap, including overlay-scrollbar environments where the browser reports a zero-width page scrollbar. The reservation is recalculated after host-shell updates, page restore, and viewport resize.

A single controller is kept per game code so host-shell remounts or F5/reinitialization do not create competing peer-review render loops.

Base host question controls are suppressed for the entire peer-rated question lifecycle. This prevents standard **Activate buzzer** / **No correct answer** controls from appearing during transient shell updates or after the peer overlay is removed.

## Question-only answer/review surfaces

Because peer-rated questions have no canonical correct answer:

- the host's answer/correct-answer presentation renders the original **question** content;
- the normal answer heading is converted back to the question heading for this presentation;
- resolved-question preview offers only **Show question** / **Return to board**; **Show answer** is hidden;
- a direct legacy `previewAnswer` URL is normalized back to the question-only preview;
- the Question Editor hides the correct-answer block editor and answer-preview button while the peer-rated presentation is selected.

These rules apply even when older quiz data still contains answer blocks from before the presentation was changed to peer-rated. Those blocks are not exposed by the runtime peer-rated question.

## Rating and scoring

The player rating control is intentionally two-step. Selecting any star count, including **0 stars**, only updates the local draft selection. The player may change that draft repeatedly before pressing **Confirm rating**. Once confirmed, the normal server-side one-rating-per-answer rule applies.

Star labels use explicit mobile-friendly touch targets and map a tap to the exact associated radio input rather than relying on browser-specific label hit testing. The same shared star-rating behavior also applies to the normal end-of-game quiz rating control, including iOS Safari.

During the voting pass, other participants and the host do not see the exact submitted star count. The host sees only that the player has rated. Exact per-player votes are revealed only in the results pass.

The arithmetic mean of the remaining valid ratings determines the reward percentage.

- every complete star contributes **20%**;
- a half-star component contributes **10%**;
- any additional positive fraction smaller than half a star contributes **5%**;
- exactly `0.0` stars awards **0%**;
- exactly `5.0` stars awards **100%**.

Examples:

| Average | Reward |
| ---: | ---: |
| 2.0 | 40% |
| 2.1–2.49 | 45% |
| 2.5 | 50% |
| 2.51–2.99 | 55% |
| 3.0 | 60% |
| 5.0 | 100% |

The question value is multiplied by the resulting percentage and rounded to the nearest whole point. The score is written through the normal answer-history path when that player's result is revealed, keeping player totals and score corrections consistent with the rest of the game runtime without spoiling the results during voting.

## AFK / exclusion behavior

During the voting pass the host may mark a currently non-responsive rater other than the displayed answer author as AFK for this question.

Exclusion is question-local and immediately:

- removes that player from the required-rater set;
- removes every rating previously submitted by that player for this question;
- prevents the player from rating later answers in the same question;
- makes the excluded player's own answer worth **0 points**;
- stops the player from blocking the remaining voting flow.

If the excluded player rated earlier answers, those ratings are absent when the later results pass calculates the affected averages. If the excluded player's own answer was already voted on, it may still be shown in the results pass, but its awarded score is forced to zero.

The exclusion does not carry into later questions.

## Persistence and recovery

Active-game snapshots persist the complete peer-review state alongside the normal game session state:

- captured participant identifiers in the score order established when the review state is first created;
- submitted text answers;
- per-answer/per-rater star values;
- AFK/excluded players;
- answers whose voting pass is complete;
- the current sequential position.

The persisted review index encodes both the voting pass and, after all voting is complete, the result-pass position. This keeps the existing snapshot shape compatible while allowing recovery in either pass.

The lifecycle never advances that index while answers are still being collected. This prevents the review cursor from being moved past all participants before the first answer exists, and it also repairs the legacy stuck `0/N` state created by the initial implementation.

After active-game recovery the same answer/result remains on screen with the same ratings, exclusions, progress, captured player order, and already revealed scores. Host-shell remount, reconnect, page restore, and normal gameplay refresh paths rebuild the question context above the recovered reviewed answer from the freshly rendered current question.

## Validation coverage

Regression coverage includes:

- the 0–5 star reward mapping and representative fractional averages;
- point calculation from the question value;
- forced non-wager / disabled-buzzer snapshot behavior;
- no separate correct-answer content for peer-rated snapshots;
- lowest-score-first participant ordering and persistence of that captured order;
- persistence/restoration of answers, ratings, exclusions, completed voting, and sequential position;
- the regression where answer collection incorrectly advanced the review index past every participant;
- separate `answering`, `rating`, and `results` client/server phases;
- right-side absolute host layout, content reservation, scrollbar-lane protection, and single-controller reinitialization behavior;
- question-above-answer review context, reuse of normal rendered question content, autoplay suppression for cloned media, bounded question scrolling, and refresh/recovery remount hooks;
- player buzzer suppression throughout answering/rating/waiting states and host legacy-control suppression;
- question-only editor, answer/correct-answer presentation, and resolved-question preview behavior;
- local rating drafts, explicit confirmation, centered zero-star action, mobile-safe star touch targets, and hiding exact vote values until the results pass;
- dedicated editor option, zero-to-five star control, AFK action, voting advance, result advance, forced board refresh, and return-to-board wiring;
- active-game snapshot capture/restore wiring.
