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
- available, active, and resolved question states, including revealed-clue progress for four-clue questions;
- randomly selected wager questions and submitted wagers;
- active and removed players, their scores, and their selected visual fallback;
- the active player and round-start scores used by standings;
- regular and final answer submissions and judging history.

Timer progress is intentionally excluded. Both question timers are stopped after
recovery so the host can inspect the restored state before continuing. If a
regular question had an open buzzer, the question remains active but its buzzer
is paused until the host activates it again. A buzzer already claimed by a
player remains claimed.

## Persistence threshold

A lobby is not written to unfinished-game storage merely because it was created,
players joined, or the host pressed **Start game**. Persistence begins when the
session contains gameplay state worth recovering:

- at least one regular question has left the `Available` state; or
- the game has entered `FinalWagering`, `FinalAnswering`, or `FinalJudging`.

The second rule covers games where the host advances directly to the Final
Question before opening any regular question. Such a snapshot can be restored
with every regular question still `Available`, preserving the final phase and its
submissions for continuation after a restart.

## Lifecycle

Only one unfinished session is retained for each host and quiz. Creating a new
game from that quiz does not replace the previous unfinished session until the
new session crosses the persistence threshold above. This prevents a newly
created lobby from accidentally overwriting a recoverable game. Completed games
are removed from active recovery storage and remain available through game
history.

The quiz list displays **Continue game** when an unfinished session exists. The
host returns to the restored game using the original join code. Existing players
rejoin with the same normalized name, receive their original runtime identity and
score, and require the normal host approval before becoming active again.
Saved players may rejoin an active question even when joining is closed to new
players. New names remain blocked until the host permits them.

Snapshots are written atomically to `App_Data/active-games.json`. Each registered
game exposes a persistence revision that changes only when recoverable state is
mutated. The persistence worker checks these lightweight revisions before it
captures or serializes a snapshot, so an unchanged media-heavy quiz is not
serialized every second.

When a revision changes, JSON is streamed directly to the temporary snapshot
file and atomically moved into place. The store does not build or retain a full
UTF-16 JSON string. This is important because quiz snapshots contain embedded
image, audio, and video bytes that JSON represents as Base64.
