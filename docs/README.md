# BadWolfQuiz Documentation

This directory contains product vision, gameplay architecture, feature specifications, and architecture decisions for BadWolfQuiz.

## Product

- [Product vision](vision.md)

## Architecture

- [Runtime model](architecture/runtime-model.md)
- [Production OOM diagnostics](operations/oom-diagnostics.md)
- [Reverse-proxy upload limits](operations/reverse-proxy-upload-limits.md)
- [Quiz media archive operations](operations/media-archive.md)

## Features

- [Quiz and question editing](features/quiz-editor.md)
- [Wagering and active player rules](features/wagering.md)
- [Question judging](features/question-judging.md)
- [Final question](features/final-question.md)
- [Answer history editing](features/answer-history-editing.md)
- [Game settings](features/game-settings.md)
- [Player and host cards](features/player-and-host-cards.md)
- [QR player join and player-device behavior](features/qr-player-join.md)
- [Active game recovery](features/active-game-recovery.md)

## Documentation principles

- Product behavior is documented before implementation.
- Runtime gameplay rules are kept independent from UI and persistence concerns.
- Important architectural decisions are recorded as ADRs.
- Documentation is updated together with code that changes documented behavior.
- Links are added only after the referenced document exists.
