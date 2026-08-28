# Anonymous shared wager

## Purpose

Anonymous shared wager is an alternative wager mode for regular single-answer-player questions. The selected answering player does not choose the wager. Instead, every other player captured when the wager starts anonymously contributes part of the question value.

Normal wager questions remain unchanged.

## Configuration

For a regular question marked as a wager question, Question Editor exposes **Wager mode** after the wager checkboxes and before the buzzer-mode setting. The available modes are **Normal wager** and **Anonymous shared wager**. The internal presentation-type marker used for persistence is not shown as a question type.

Round settings have two independent random-wager controls: the existing normal random wager count and a separate anonymous-shared random wager count. When either random mode is enabled, runtime wager positions are selected for that game. Anonymous-shared positions are reserved first and normal random wagers are selected from the remaining eligible positions, so the two random modes can never select the same question.

## Contribution rules

When collection starts, the current answering player is excluded and the remaining current players are captured as the funding participants. Late joins do not change this participant set.

Each funding player chooses exactly one of:

- 0%
- 25%
- 50%
- 75%
- 100%

The conceptual share is `question value / funding player count`. Each private contribution is that share multiplied by the selected percentage and rounded with `MidpointRounding.AwayFromZero`.

Independent rounding can otherwise make the sum exceed the original question value by a small amount. Any rounding overflow is trimmed deterministically from the last positive captured contributions, so the retained private contributions and combined wager remain exactly zero-sum and the final wager never exceeds the question value.

If there are no eligible funding players, the combined wager is zero and the question proceeds immediately.

## Privacy and host controls

Funding players see their own maximum share and their own submission state only. After confirming, their choice is locked and the UI shows only that the contribution was submitted.

The host sees the captured participant names and only `submitted` / `pending` status while collection is active. Percentages, amounts, and partial totals are never returned by the host status endpoint. After every participant has resolved, the final combined wager may be shown.

For an inactive participant, the host can resolve the missing contribution as 100%. A participant removed before submitting is also resolved automatically as 100%. An already submitted contribution remains unchanged if that player is later removed.

## Answering and settlement

The answering player waits while the other players submit. The buzzer is suppressed for every player throughout collection, including server-rendered reload/recovery pages.

Once all contributions are resolved, the existing wager-question answering flow becomes active with the calculated combined wager.

Settlement is zero-sum:

- correct answer: answering player receives the combined wager; each funding player loses their own contribution;
- incorrect/no answer: answering player loses the combined wager; each funding player receives their own contribution.

Funding scores are never changed when a contribution is submitted. Score transfers happen only during settlement.

## Recovery

The active-game snapshot stores the captured participant set and private submitted choices in an optional shared-wager field. Older `active-games.json` snapshots remain compatible because the field defaults to `null`.

On recovery, collection resumes with the same participants and submitted choices. If the question had already entered the answering phase, the same private contribution state is retained for settlement.

## Regression coverage

Game tests cover contribution rounding, the issue example, allowed percentages, duplicate prevention, participant capture, forced 100% fallback, zero-funder behavior, correct/incorrect zero-sum settlement, removed-player settlement, and random-wager integration.

Web tests cover private payload boundaries, player/host runtime assets, AFK controls, server-side buzzer suppression, editor exposure, and active-game recovery persistence.
