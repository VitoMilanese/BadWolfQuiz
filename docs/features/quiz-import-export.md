# Quiz Import and Export

BAD WOLF QUIZ exchanges individual quiz definitions through `.bwquiz` packages. A package is a ZIP archive containing a versioned `manifest.json` and a `media/` directory for uploaded images and audio.

The manifest contains quiz metadata, rounds, row values, categories, questions, content blocks, wager and buzzer settings, four-clue presentation types, and final-question content. Host ownership, database identifiers, active sessions, and game history are intentionally excluded.

Import always creates a new quiz with new local identifiers. A suffix is added when necessary to avoid a duplicate title. Before database changes are made, the importer validates the format version, archive paths, expanded size, manifest limits, and referenced media entries.

Format version 1 limits the compressed package to 1 GB and its expanded contents to 2 GB. Import request limits apply only to the authenticated quiz-import endpoint. Export archives are streamed through an automatically deleted temporary file instead of being assembled entirely in memory.
