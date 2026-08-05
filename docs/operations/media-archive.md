# Quiz media archive

Bad Wolf Quiz stores active quiz data in `App_Data/BadWolfQuiz.db` and archived quiz media in
`App_Data/BadWolfQuiz.Archive.db`. The archive contains only the `FileData` BLOBs from question,
answer, final-question, and final-answer content blocks. Quiz structure, text, MIME types,
file names, ownership, game history, and resumable-game data remain in the main database.

## Configuration

Configure both SQLite files independently:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=App_Data/BadWolfQuiz.db",
    "ArchiveConnection": "Data Source=App_Data/BadWolfQuiz.Archive.db"
  },
  "MediaArchive": {
    "Enabled": true,
    "ArchiveAfterDays": 180,
    "ScanIntervalHours": 24,
    "ScanStartTimeUtc": "03:00:00",
    "MaximumQuizzesPerRun": 2,
    "DeleteArchiveCopyAfterRestoreDays": 14,
    "OrphanRetentionDays": 30
  }
}
```

Relative SQLite paths are resolved from the application content root. Positive limits are
validated at startup; retention periods may be zero but cannot be negative.

`ScanStartTimeUtc` anchors the recurring scan schedule independently of application startup.
For example, an interval of 24 hours with `03:00:00` runs daily at 03:00 UTC. An interval of
12 hours runs at 03:00 and 15:00 UTC. The value must be between `00:00:00` and `23:59:59`.

## Safety and recovery

Archiving claims the quiz with an operation ID, copies all BLOBs in an archive transaction,
verifies count, size, role, actual length, and SHA-256, and only then clears the main BLOBs in a
separate transaction. A failure before the main commit leaves the original media intact. The
operation ID and unique archive index make an interrupted copy idempotent.

Restoring verifies the complete archive and every destination before writing. All BLOBs and the
media state are restored in one main transaction. The archive copy is retained for the configured
period. Interrupted states are marked retryable during startup.

Soft-deleting a quiz retains its archive. Permanent deletion uses `IQuizDeletionService`, which
deletes records matching both quiz ID and host ID from the archive before deleting the main quiz.
Orphans are marked when first observed and removed only after the retention period.

## Backup and restore

`BadWolfQuiz.db` and `BadWolfQuiz.Archive.db` are one logical data set and must be backed up at a
consistent point in time. Stop writes or use a coordinated SQLite online backup for both files.
Restore both files together; restoring only one can leave operation metadata out of sync.

## VACUUM

Clearing BLOBs releases SQLite pages for reuse but does not shrink the file immediately.
`ISqliteVacuumService` exposes separate main and archive operations. Main optimization refuses
to run while a database-backed game is active. VACUUM should run only in a maintenance window:
it can take time, lock the database, and require substantial free disk space. A manual main
VACUUM is recommended after the first large archive batch when physical space must be reclaimed.

Splitting databases improves the active working set, page cache use, migrations, backups, and
active queries. It does not reduce total disk usage while both files remain on the same disk.
