# BadWolfQuiz Documentation

This directory contains product vision, gameplay architecture, feature specifications, and architecture decision records for BadWolfQuiz.

## Contents

- [Product vision](vision.md)
- [Game engine architecture](architecture/game-engine.md)
- [Wagering and final question](features/wagering-and-final-question.md)
- [ADR-0001: Separate runtime game model](decisions/ADR-0001-runtime-game-model.md)

## Documentation principles

- Product behavior is documented before implementation.
- Runtime gameplay rules are kept independent from UI and persistence concerns.
- Important architectural decisions are recorded as ADRs.
- Documentation should be updated together with the code that changes the documented behavior.
