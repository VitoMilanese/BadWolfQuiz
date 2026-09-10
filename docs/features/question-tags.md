# Question Tags

## Purpose

Question tags let hosts classify individual quiz questions with reusable free-form metadata. Tags are currently authoring metadata; they establish the data needed for future question filtering and search without changing gameplay behavior.

## Editing tags

The regular Question Editor supports multiple tags on each question. Hosts can add custom text or select an existing suggestion from the autocomplete dropdown below the tag input.

Tag names are trimmed before saving. Blank values are ignored, tag names are limited to 100 characters, and duplicate tags on the same question are prevented case-insensitively. Removing a tag from the editor removes that question/tag association when the question is saved.

## Suggestions

Autocomplete suggestions are built from tags already used by the current host. The server filters suggestions using the text currently entered, ranks matching tags by usage frequency and then by name, and returns at most eight values. Tags already selected on the current question are omitted from the client-side suggestion list.

There is no separate global tag catalog: reusable suggestions are derived from the per-question tag records already stored for that host.

## Persistence

Tags are stored as `QuizQuestionTag` rows linked to `QuizQuestion`. Each row stores both the display name and a normalized name used for duplicate protection. Deleting the owning question cascades to its tags.

Question tags are preserved when questions are copied, when quizzes are cloned, and when `.bwquiz` packages are exported and imported. Package import writes tags after imported questions receive their database IDs and verifies that the expected tag rows were persisted before the import transaction is committed.

## Performance

The Question Editor loads the tracked question/content/tag graph with split queries. This avoids a cartesian JOIN across question blocks, answer blocks, and tags, which would otherwise multiply stored media BLOB data and significantly slow saves on media-heavy questions.

## Release

Question tags are the headline backwards-compatible feature for BadWolfQuiz Web `1.28.0`.
