# Player achievements

Player achievements were introduced in BadWolfQuiz Web `1.27.0` by issue #442.
They provide persistent long-term milestones without changing normal quiz scoring or trusting client-only unlock signals.

## Identity and persistence

Achievement records are stored in the main SQLite database. When a stable Bad Wolf account is known, achievements are associated with that account. Anonymous gameplay can fall back to the host plus normalized player name, and later account linking can adopt matching nickname history.

The persistence model also stores game/account and user-question/account links used by achievements that depend on gameplay identity or developer conversations. One-time achievements are de-duplicated before persistence, so evaluating the same completed game again does not create duplicate unlocks.

Active-game achievement runtime state is included in the normal active-game snapshot. Pending event milestones therefore survive application restart together with the rest of the recoverable game state.

The `20260909225025_AddPlayerAchievements` migration creates the achievement tables and indexes. Startup contains compatibility handling for development databases that already contain an older physical version of those tables but do not contain the current migration-history entry.

## Authoritative evaluation

Unlock conditions are evaluated from server-owned game state and persisted history. The client is responsible only for presentation and user actions; it does not decide that an achievement has been earned.

Two kinds of conditions are used:

- historical metrics such as completed games, correct answers, wins, streaks, best final score, accumulated score, and flawless-answer streaks;
- direct server-observed events such as all-in wagers, first question selection, buzzer races, anonymous shared wagers, avatar/webcam actions, account/community actions, disconnect/rejoin flows, and other unusual gameplay situations.

Direct events that need the result of the current game are tracked as pending runtime unlocks and persisted through the completed-game achievement evaluation.

## Initial catalog

The initial catalog contains 47 achievements. The source of truth for codes, icons, secrecy, metrics, and numeric targets is `PlayerAchievementService.Catalog`.

Long-term progression includes the first completed game, first correct answer, 5 and 25 completed games, 25 and 100 correct answers, first and fifth wins, a five-answer correct streak, score milestones, flawless play, and recovery from a negative score. `BigGame` unlocks at a final score of at least **15,000** points; additional score milestones cover 30,000 in one game and 100,000 / 500,000 / 1,000,000 accumulated points.

Account and community milestones include account registration, playing an owned quiz with at least two other players, playing a public quiz hosted by somebody other than its author, Minigames/AI milestones, visiting the project repository from the site, rating a quiz, changing a password, developer conversations, and contributor recognition.

Gameplay-specific milestones include first question selection, opening the second round, last-to-first comeback wins, final-question reversals, category coverage, silent rounds, all-in outcomes, anonymous shared wagers, Four Clues performance, close buzzer races, kick/rejoin behavior, late joining, avatar changes, and webcam use.

### Special gameplay rules

- `AnonymousStake100Profit`: the player voluntarily stakes 100% on another player during an anonymous shared wager, the answering player is wrong, and the contribution actually awards points. Forced contributions do not count.
- `AnonymousStakeZeroSave`: the player voluntarily stakes 0% on another player and preserves their points because the answering player is correct.
- `FourCluesTwoClues`: the player answers a Four Clues question correctly while only clues 1 and 2 have been revealed; clues 3 and 4 must still be hidden.
- `BuzzerPhotoFinishFirst`: the buzzer winner is followed by the second-fastest player in less than 50 ms.
- `BuzzerPhotoFinishSecond`: the second-fastest player presses less than 50 ms after the winner.
- `KickedAndReturned` (secret): the host removes the player and the same player identity later returns to that same game.
- `FirstToThirdReturn` (secret): the player participates in round one, genuinely disconnects, returns in round three, and finishes the game. Short page-transition disconnects covered by a valid transition token do not count.
- `LateJoiner` (secret): a new player first joins after the first round has already finished, i.e. during round two or later.
- `AvatarChanged`: the initial avatar choice does not count; the player must change the avatar after an initial choice already exists.
- `WebcamEnabled`: enabling either a normal webcam or a URL webcam counts.

## Secret achievements

Secret achievements keep their real name and condition hidden in the player UI until they are unlocked. Locked secret cards show the shared secret placeholder instead of exposing the requirement.

The catalog intentionally mixes normal and secret milestones. Secrecy is a catalog property and is independent from whether the unlock is metric-based or event-based.

## Player and host UX

Players can open an achievement dialog that shows unlocked milestones and progress for visible locked milestones. Newly earned achievements are highlighted without interrupting active gameplay.

The host lobby exposes a trophy action on each player row. It opens a host-owned achievement dialog for that specific player; the endpoint verifies ownership of the active game before returning achievement data.

Achievement cards use the shared responsive dialog layout with a fixed header and independently scrolling body so the title and close action remain visible on smaller screens.

## Localization

Achievement UI text, names, and descriptions follow the normal localization model and are provided for English, Ukrainian, Italian, and Russian in `Resources/Localization/AchievementResource*.resx`.

Localization regression coverage verifies that every catalog achievement has a non-empty name and description in every supported achievement resource and that placeholder values are not accidentally shipped.

## Regression coverage

The achievement test suite covers catalog composition, the 15,000-point Big Game threshold, history metrics, duplicate prevention, account/nickname adoption, current-game highlighting, persistence/recovery, gameplay-specific direct unlocks, migration compatibility, player/host UI wiring, and localization-resource completeness.
