# Persistent Game History

## Purpose

Completed games are copied from the in-memory runtime into the SQLite database
so the host can review them after the live session or application process ends.

## Current implementation

The first persistent-history version stores:

- the source quiz and public game code;
- creation, start, and completion timestamps;
- every player name and final score;
- every regular question in the runtime board;
- each recorded regular-question result, including correctness, signed score
  change, and judgment time.

The save operation is idempotent by public game code. Saving a completed game
again replaces its persisted player, question, and answer snapshot. This keeps
the archive synchronized when the host corrects answer history after the game
has ended.

Games without a configured final question are persisted when the last question
of the last round closes. Games with a final question are persisted when the
last final answer is judged.

The administration area provides a chronological game-history list and a
details page with final scores and regular-question answers. The details page
also groups correct answers, attempts, accuracy, and score changes by round and
player.

The player-statistics page groups player names case-insensitively within the
current host account. It shows completed games, the sum of final scores,
lifetime correct answers and attempts, accuracy, and the latest game date.
Players do not need accounts: their trimmed nickname is their identity inside a
host's history, so similarly named people must still be distinguished by the
host when they join.

## Data retention

Deleting a quiz from the quiz list archives it instead of physically deleting
it. The archived definition remains available to existing game-history rows but
is excluded from the active quiz list and cannot be used to start a new game.

Reconnect credentials are not copied into history. The legacy required token
column receives an empty value for completed-game player snapshots.

## Future extensions

- final-question wagers, submitted answers, and judgments;
- exact persisted final positions and tie-break metrics;
- history filters and search;
- deleting or exporting history entries;
- automatic cleanup and retention policies.
