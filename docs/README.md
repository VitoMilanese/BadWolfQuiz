# BadWolfQuiz Documentation

This directory contains product vision, gameplay architecture, feature specifications, and architecture decisions for BadWolfQuiz.

## Product

- [Product vision](vision.md)
- [Product versioning and release tags](versioning.md)

## Architecture

- [Runtime model](architecture/runtime-model.md)
- [Portal layout and persistent footer](architecture/portal-layout.md)
- [Production OOM diagnostics](operations/oom-diagnostics.md)
- [Reverse-proxy upload limits](operations/reverse-proxy-upload-limits.md)
- [Quiz media archive operations](operations/media-archive.md)
- [SEO indexing operations](operations/seo-indexing.md)
- [External project links](operations/external-project-links.md)

## Features

- [Quiz and question editing](features/quiz-editor.md)
- [Question copying and cloning](features/question-copying.md)
- [Host-selected multiple-choice questions](features/host-multiple-choice.md)
- [Fullscreen busy feedback](features/busy-indicators.md)
- [Round and category intros](features/round-category-intros.md)
- [Host gameplay navigation](features/host-gameplay-navigation.md)
- [Quiz import and export](features/quiz-import-export.md)
- [Wagering and active player rules](features/wagering.md)
- [Question judging](features/question-judging.md)
- [Buzzer activation modes](features/buzzer-activation-modes.md)
- [Answer reward decay](features/answer-reward-decay.md)
- [Four-clue questions](features/four-clue-questions.md)
- [Mandatory all-player questions](features/all-player-questions.md)
- [Final question](features/final-question.md)
- [Answer history editing](features/answer-history-editing.md)
- [Game settings](features/game-settings.md)
- [Player and host cards](features/player-and-host-cards.md)
- [QR player join and player-device behavior](features/qr-player-join.md)
- [Player admission controls](features/player-admission-controls.md)
- [Active game recovery](features/active-game-recovery.md)
- [Game history](features/game-history.md)
- [Host accounts](features/host-accounts.md)
- [Discord voice moderation](features/discord-voice-moderation.md)
- [User feedback conversations](features/user-feedback-conversations.md)

## Documentation principles

- Product behavior is documented before implementation.
- Runtime gameplay rules are kept independent from UI and persistence concerns.
- Important architectural decisions are recorded as ADRs.
- Documentation is updated together with code that changes documented behavior.
- Links are added only after the referenced document exists.
