# Anonymous shared wager

## Purpose

Anonymous shared wager is an alternative wager mode for regular single-answer-player questions. The selected answering player does not choose the wager. Instead, every other player captured when the wager starts anonymously contributes part of the question value using their own score as the stake.

Normal wager questions remain available as a separate mode.

## Configuration

For a regular question marked as a wager question, Question Editor exposes **Wager mode** after the wager checkboxes and before the buzzer-mode setting. The available modes are **Normal wager** and **Anonymous shared wager**. The internal presentation-type marker used for persistence is not shown as a question type.

An explicitly marked wager question remains a wager question even when either round-level random wager mode is enabled. **Exclude from random wager selection** controls only whether that position may be selected by the random-wager mechanism; it does not cancel an explicit wager.

Round settings have two independent random-wager controls: the existing normal random wager count and a separate anonymous-shared random wager count. Their count fields remain in stable positions in the round-settings layout and are unavailable while the corresponding toggle is off.

When either random mode is enabled, runtime wager positions are selected for that game. Anonymous-shared positions are reserved first and normal random wagers are selected from the remaining eligible positions, so the two random modes can never select the same question. The displayed available-question counts reflect the actual eligible candidate pool, including explicit random-selection exclusions and presentation-type compatibility.

## Contribution rules

When collection starts, the selected answering player is excluded and the remaining current players are captured as the funding participants. Late joins do not change this participant set.

Each funding player chooses exactly one of:

- 0%
- 25%
- 50%
- 75%
- 100%

The conceptual share is `question value / funding player count`. Each private contribution is that share multiplied by the selected percentage and rounded with `MidpointRounding.AwayFromZero`.

Independent rounding can otherwise make the sum exceed the original question value by a small amount. Any rounding overflow is trimmed deterministically from the last positive captured contributions, so the retained private contributions and combined wager remain exactly zero-sum and the final wager never exceeds the question value.

If there are no eligible funding players, the combined wager is zero and the question proceeds immediately.

## Player UX and privacy

Funding players see their own maximum share and their own selection only. The percentage buttons use a visible selected state, and after confirmation the choice is locked while the player's own submitted percentage remains visible to that player.

Players never receive another player's percentage or contribution. The answering player waits while the funding players submit and does not choose or enter a wager.

The host sees the captured participant names and only `submitted` / `pending` status while collection is active. Percentages, amounts, and partial totals are never returned by the host status endpoint. After every participant has resolved, the final combined wager may be shown in the normal question header used for judging.

For an inactive participant, the host can resolve the missing contribution as 100%. A participant removed before submitting is also resolved automatically as 100%. An already submitted contribution remains unchanged if that player is later removed.

## Answering and buzzer behavior

The selected player is the only answering player for an anonymous shared wager question. Neither the selected player nor the host enters a wager amount.

Once all contributions are resolved, the calculated combined wager is applied to the question and the question content is shown. The normal player buzzer is suppressed for the entire anonymous shared wager lifecycle, including both `AwaitingWager` and `Active` states and server-rendered reload/recovery pages. Funding players do not get an answering attempt, and the answering player is already predetermined.

The host judges the predetermined answering player with the dedicated **Correct** / **Incorrect** controls so settlement always goes through the anonymous shared wager operation rather than the generic question-judging path.

## Settlement

Settlement is zero-sum:

- correct answer: answering player receives the combined wager; each funding player loses their own contribution;
- incorrect/no answer: answering player loses the combined wager; each funding player receives their own contribution.

Funding scores are never changed when a contribution is submitted. Score transfers happen only during settlement, and the answering-player delta and all funding-player deltas are validated as one balanced operation.

## Recovery

The active-game snapshot stores the captured participant set and private submitted choices in an optional shared-wager field. Older `active-games.json` snapshots remain compatible because the field defaults to `null`.

On recovery, collection resumes with the same participants and submitted choices. If the question had already entered the answering phase, the same private contribution state and combined wager are retained for settlement.

## Regression coverage

Game tests cover contribution rounding, the issue example, allowed percentages, duplicate prevention, participant capture, forced 100% fallback, zero-funder behavior, correct/incorrect zero-sum settlement, removed-player settlement, independent random-mode selection, and preservation of explicitly configured wagers while random wager modes are enabled.

Web tests cover private payload boundaries, player/host runtime assets, AFK controls, server-side buzzer suppression, editor exposure, stable round-setting controls, host judging isolation, and active-game recovery persistence.
