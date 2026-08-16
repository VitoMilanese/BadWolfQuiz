# Unfinished game lifecycle

BadWolfQuiz keeps one resumable unfinished game per host and quiz, but a game does not become resumable merely because its lobby or round intro was opened.

## When persistence begins

A newly created game remains transient while the host is in the lobby or on round/category intro screens. The active-game persistence service begins storing it only after the first regular quiz question has been opened. In runtime terms, at least one board question must have moved away from `Available`.

After that first question is opened, the existing active-game snapshot behavior continues normally and gameplay changes are persisted for recovery.

## Starting a replacement game

On `/Admin/Quizzes`, starting a new game for a quiz that already has a resumable unfinished game requires an explicit in-app confirmation.

- **Cancel** leaves the existing unfinished game untouched and does not create a new game.
- **Start new game** creates a new lobby, but the previously persisted unfinished game remains the resumable snapshot while the replacement is only in the lobby or intro flow.
- When the replacement opens its first regular question, it becomes persistable and replaces the older stored unfinished state for the same host and quiz.
- If the replacement later completes, the older game is not restored as the resumable snapshot.

This also applies when archived quiz media must be restored before the replacement lobby can be created: replacement confirmation happens before the restore-and-play flow.

## Deleting an unfinished game

When a quiz has a resumable unfinished game, its **Actions** menu includes **Delete unfinished game** immediately before the normal quiz **Delete** action.

Deleting an unfinished game:

- requires an in-app destructive confirmation;
- removes the resumable active-game snapshot and its live unfinished runtime registration;
- does not archive or delete the quiz definition;
- removes both **Continue game** and **Delete unfinished game** from the quiz card after the page reloads.

The active-game store records an in-memory deletion cutoff while the application is running so a persistence write that was already in flight cannot recreate the snapshot that the host just deleted.

## Compatibility

Older active-game snapshots that were captured before any question was opened are treated as transient lobby/intro state. They are not restored as resumable games and are removed on the next persistence pass.
