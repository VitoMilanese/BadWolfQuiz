# Buzzer Activation Modes

## Scope

Authored buzzer activation modes apply to the three question types that use the normal buzzer flow:

- Standard questions, except when the question is used as a wager question;
- Four Clues questions;
- Host-selected multiple-choice questions.

Mandatory all-player text and all-player multiple-choice questions do not use the normal buzzer. Wager questions also use their own answer flow instead of the normal buzzer.

## User-facing modes

Question authors can choose one of the following modes:

- **Use round default** — use the round's default buzzer mode. If the round also inherits the game default, the game-level `RegularQuestionBuzzerStartMode` Automatic/Manual setting remains the compatibility fallback. App-created quizzes historically persisted a hidden round-level `Manual` default even though that setting is not exposed in the current round editor; inherited questions treat that legacy value as game-level inheritance so **Automatic** continues to work. An explicit **Manual** mode selected on the question itself remains manual.
- **Manual** — the question opens with the buzzer inactive. The host can activate it with the normal **Activate buzzer** control.
- **Immediately** — the buzzer opens as soon as the question is selected and the existing buzzer-window timer starts.
- **After media** — if the initial question presentation contains audio, video, or YouTube media, the buzzer stays inactive until that media ends or fails. If there is no relevant media, the buzzer opens immediately. The behavior is the same whether playback starts automatically or the host starts it manually.
- **After delay** — the buzzer opens after the configured `BuzzDelaySeconds` interval. A zero-second delay behaves as immediate activation.

`Disabled` is not a user-facing authoring mode. Internally, the disabled state remains available for wager and all-player flows where the normal buzzer is structurally unavailable. Legacy buzzer-based questions that already store `Disabled` are treated as **Manual**.

## Media completion

For **After media**, the host page treats media completion as part of the question state rather than as a best-effort UI event. Completion is remembered even if it happens before the asynchronous buzzer-policy request has returned, which prevents autoplay media from finishing before the activation policy is ready.

Native audio/video completion and failure are supported, as are YouTube ended/error events. If browser autoplay is blocked and the host starts the media manually, the same completion path is used.

For Four Clues questions, only the initially visible first two clues participate in the initial **After media** gate. Revealing clue 3 or 4 later does not restart or re-run the initial buzzer activation policy.

## Runtime ownership

The authored mode and delay are copied from the quiz definition into the immutable runtime quiz snapshot when the game is created. The Game Engine resolves the effective policy and remains authoritative for whether the buzzer may open. Host-side delayed/media orchestration submits a validated activation command; it does not bypass the runtime policy.
