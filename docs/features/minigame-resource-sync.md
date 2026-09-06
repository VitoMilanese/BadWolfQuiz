# Minigame resource synchronization

The MasterHost **Minigame editor** can explicitly synchronize game cards from `Resources/Minigames/GameCards` without replacing the question catalog.

## Synchronization rules

The synchronization scans supported top-level game images (`.png`, `.jpg`, `.jpeg`, `.webp`, and `.gif`) and matches games by the image file name without its extension, case-insensitively.

For each resource game:

- a game that does not yet exist in the database is added with the resource image;
- an existing game keeps its database name but receives the current resource image and content type;
- if a matching `<GameName>.txt` file exists, it replaces that game's complete answer set using the current question order;
- TXT answer rows use the editor format: `1` = YES, `0` = NO, empty = Unassigned;
- if no matching TXT file exists, existing database answers are left unchanged; a newly added game simply starts without assigned answers.

The question list, question ordering, and enabled/disabled question state are never changed by this operation.

All resource files are validated before database changes begin. Duplicate image names, duplicate matching TXT names, unreadable files, invalid answer values, or an answer-line count that differs from the current question count stop synchronization before any game is added or updated.

## Database-only games

Synchronization never automatically deletes a database game that is missing from the resource folder.

After a successful synchronization, database games with no matching resource image are returned to the editor and shown in a checklist dialog:

- the dialog displays 10 games per page;
- checkbox selections are preserved while paging;
- **Keep all** closes the cleanup step without deleting anything;
- **Delete selected** requires confirmation and removes only the explicitly selected games and their stored answers.

The delete endpoint performs its own resource-folder check immediately before deletion. A crafted or stale request cannot delete a selected game if a matching resource image currently exists.

## Authorization

Both synchronization and cleanup endpoints require the existing `MasterHost` authorization policy. POST requests retain Razor Pages antiforgery validation, and the browser uses the shared busy indicator while requests are running.
