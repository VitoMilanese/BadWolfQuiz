# Host-defined tag achievements

Hosts can define their own player achievements from **Achievement editor** in the authenticated host menu (localized as **Редактор досягнень** in Ukrainian).

Each definition has a stable GUID-backed identity, a title, description, explicit correct-answer target, 1–20 question tags, and PNG artwork. Artwork must be a valid PNG exactly **250 × 250 px** and no larger than 2 MB. The original PNG bytes are stored in SQLite so alpha transparency is preserved and artwork survives restarts and deployments.

## Progress rules

Custom achievements count only correct answers from eligible, persisted `Finished` games owned by the host that created the achievement. Question-tag matching trims whitespace and is case-insensitive. A question contributes at most one progress step to an achievement even when several of the achievement's configured tags match that question.

The unlock threshold is always explicit in the editor; there is no hidden or hard-coded custom-achievement target. Signed-in players keep custom unlocks on their account, while anonymous players use the existing host plus normalized-player-name fallback identity.

Custom progress never combines gameplay from different hosts. Built-in achievement counters retain their existing account-wide behavior.

## Persistence and editing

Unlocks are stored in the normal `PlayerAchievements` history using a stable `Custom:{guid}` achievement code and the source game-session ID that crossed the configured target. Re-evaluation is idempotent and does not duplicate an existing unlock.

Editing a definition keeps its stable identity, so already-earned unlocks remain valid. Deleting a custom achievement is a soft delete: it stops future progress/display for the active definition but retains the definition metadata and artwork so historical unlock records can still resolve correctly. Deleted identifiers are never reused.

The `20260911210000_AddHostCustomAchievements` migration adds the custom definition and tag tables.

## Player and host views

Host-defined cards reuse the normal achievement-card presentation and are marked with a **HOST** badge outside the host editor list. They are available in the player's achievement UI, the authenticated account achievement page, host player-achievement dialogs, and achievement history metadata.

During an active game, anonymous/nickname-only players remain scoped to custom achievements created by the current host. Signed-in players see the current host's active custom achievements, including locked/progress state, plus every host-defined achievement that the account already unlocked with any other host. Locked or in-progress achievements from unrelated hosts are not exposed. The two sets are merged by stable custom-achievement code so current-host unlocks are not duplicated.

Persisted account unlocks remain visible during games hosted by other accounts and continue to resolve their original title, description, and artwork even if the creator later soft-deletes the definition. `IsNewInCurrentGame` is reserved for an unlock whose persisted source game-session ID matches the game currently being viewed.
