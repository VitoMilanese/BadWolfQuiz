# Player admission controls

Issue #114 groups the in-game player-admission controls behind the existing lock button on the host game page.

## Menu behavior

During a running game, clicking the lock button opens a context menu instead of immediately changing the new-player connection policy. The menu opens upward and to the left of the lock button and contains these actions in order:

1. **Accept all waiting players** — immediately approves every player currently waiting for host approval. This action is hidden when no players are waiting.
2. **Automatically accept new players** — enables or disables automatic approval for newly connected players who would otherwise enter the waiting-for-approval state. This action remains available regardless of whether players are currently waiting.
3. **Allow / deny new player connections** — preserves the existing connection-policy toggle and keeps the lock icon synchronized with the current policy.

The menu closes on an outside click or Escape without changing state. Selecting an action performs only that action.

## Waiting-player behavior

Existing individual approval remains available. Bulk approval uses the same approval semantics as approving waiting players individually and does not affect players that are not waiting for approval.

When automatic approval is enabled, a newly connected player that would normally require host approval is admitted immediately. Disabling automatic approval restores the existing manual approval flow.

## Related issues

- #82 — accept all waiting players
- #83 — automatically accept newly connected waiting players
- #114 — grouped player-admission menu
