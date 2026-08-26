# Host-selected multiple-choice questions

## Purpose

Host-selected multiple-choice questions combine the normal buzzer flow with a fixed list of answer options controlled by the host. Players still buzz from their own devices; the host judges the answer by selecting the option the player gave.

## Authoring contract

- A question contains the normal question content blocks.
- The Correct answer section starts with one required, non-removable **Answer options** structural block.
- **Answer options** contains between 4 and 10 distinct Text options.
- Each selectable option is limited to 20 characters.
- The first option inside **Answer options** is the correct option and the editor presents that state explicitly.
- Reordering options changes which option is correct.
- Normal answer blocks may be added after **Answer options** as optional reveal-only content. These blocks are not selectable and support the normal answer content types, including Text, Image, Audio, YouTube, Container, and legacy Video compatibility.
- This question type cannot be a wager question and is excluded from random wager selection.

Legacy host-selected multiple-choice questions that store the old flat four-to-ten Text answer blocks are treated as selectable options automatically. Opening a legacy question in the editor creates the **Answer options** structure in the view model; the database is not changed until Save.

## Answer presentation

Only the correct selectable option is retained from the option list when the answer is revealed. Optional normal answer content is then rendered after it in configured order.

The same vertical reveal composition is used by all supported answer surfaces:

1. the correct option from **Answer options**;
2. optional reveal-only answer blocks that follow the structural block.

Incorrect options are omitted from every reveal surface. This applies to:

- live gameplay while the question is in `ShowingAnswer`;
- Question Editor **Preview - Correct answer**;
- the separate host **Correct answer** (`AnswerKey`) screen;
- the resolved/closed question preview opened from the board.

Reveal-only image and audio content uses the deferred-media path, including restored or recovered active games.

## Host answer-option panel

During an active question, the host sees a vertical answer-option panel on the right side of the gameplay screen.

Only the children of **Answer options** participate in this panel. Reveal-only answer blocks never appear as host choices and never affect elimination, judging, scoring, or dynamic value.

- Available answer options are displayed in a stable randomized order for the current game/question rather than editor order.
- Eliminating an incorrect option does not reshuffle the remaining options.
- **Nobody answered** is always rendered after all answer-option buttons and is never part of the shuffle.
- Before a player owns the buzzer, answer-option buttons are disabled and **Nobody answered** is enabled.
- After a player owns the buzzer, answer-option buttons become the judgment controls and **Nobody answered** is disabled until the buzzer becomes free again.
- The standard **Correct / Incorrect** judgment controls and the generic resolve control are not rendered for this presentation type.
- The header action menu remains above the answer-option panel in the stacking order.

The panel participates in the normal AJAX host-gameplay lifecycle. The host-selected multiple-choice assets are loaded when the Lobby shell is mounted, including the initial `RoundIntro` → `Lobby` transition, so opening the first question does not require a browser refresh before the panel appears.

## Dynamic value

The current reward and penalty use an equal-step percentage from 100% with all original options present to 50% with three options remaining. The percentage is calculated as:

`50 + ceil(50 * (remaining - 3) / (original - 3))`

Values at or below three remaining options are clamped to 50%. The question value is the base point value multiplied by the current percentage and rounded to the nearest integer, away from zero.

Examples:

- 4 options: 100%, 50%
- 5 options: 100%, 75%, 50%
- 6 options: 100%, 84%, 67%, 50%
- 7 options: 100%, 88%, 75%, 63%, 50%

The 7-option series intentionally follows the equal-step formula above; it replaces the earlier inconsistent 100/90/80/70/50 example from issue #259.

Whenever the current value changes after an incorrect answer or timer elimination, both the host panel and the reward displayed beside the question/category heading are updated to the new value.

## Runtime behavior

Selecting the correct option awards the current value, closes the question, and shows the shared correct-option-plus-additional-content reveal through the normal AJAX gameplay refresh without a full-page reload.

Selecting an incorrect option subtracts the current value, removes that option, recalculates the value, and reopens the buzzer for players who have not already answered. A player keeps the normal one-attempt-per-question restriction.

If an incorrect answer leaves no player who is still eligible to buzz, the question closes automatically and shows the correct answer.

**Nobody answered** closes the question through the existing no-correct-answer flow and immediately shows the correct answer.

When the buzzer timer expires, one random remaining incorrect option is removed. The correct option is never removed. The current value is recalculated and, while more than two options remain, the buzzer timer restarts for the next attempt.

If only two options remain after either an incorrect answer or timer elimination, the question automatically closes and shows the correct answer without another buzzer attempt.

## Recovery

The set of remaining selectable options is part of the active-game snapshot, so recovery does not restore eliminated choices. Reveal-only answer blocks are stored separately from the runtime option set and are preserved in the active-game JSON snapshot. The current reward is derived from the recovered remaining-option state, and the stable host display order is reconstructed from the game and question identifiers.

Legacy active-game snapshots without the structural marker continue to interpret the old flat four-to-ten Text answer blocks as selectable options and reveal only the first/correct option.

## Release

Host-selected multiple-choice questions were introduced in BadWolfQuiz Web `1.20.0` (`web-v1.20.0`). Structured **Answer options** with separate reveal-only answer content are added in BadWolfQuiz Web `1.22.38`.
