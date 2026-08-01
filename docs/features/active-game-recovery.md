# Active Game Recovery

## Purpose

An interrupted game can be continued without losing the board state, players,
scores, wagers, or answer history. The application automatically persists active
sessions and restores them after a process restart or unexpected interruption.

## Snapshot scope

The saved session contains:

- the immutable quiz snapshot and game-specific settings;
- the public join code and whether new players may join;
- the current game phase and round;
- available, active, and resolved question states;
- randomly selected wager questions and submitted wagers;
- active and removed players, their scores, and their selected visual fallback;
- the active player and round-start scores used by standings;
- regular and final answer submissions and judging history.

Timer progress is intentionally excluded. Both question timers are stopped after
recovery so the host can inspect the restored state before continuing. If a
regular question had an open buzzer, the question remains active but its buzzer
is paused until the host activates it again. A buzzer already claimed by a
player remains claimed.

## Lifecycle

Only one unfinished session is retained for each host and quiz. Creating a new
game from that quiz replaces its previous unfinished session. Completed games are
removed from active recovery storage and remain available through game history.

The quiz list displays **Continue game** when an unfinished session exists. The
host returns to the restored game using the original join code. Existing players
rejoin with the same normalized name, receive their original runtime identity and
score, and require the normal host approval before becoming active again.
Saved players may rejoin an active question even when joining is closed to new
players. New names remain blocked until the host permits them.

Snapshots are written atomically to `App_Data/active-games.json`. The persistence
worker checks active sessions regularly and avoids rewriting the file when the
serialized gameplay state has not changed.
