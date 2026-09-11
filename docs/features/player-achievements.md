# Player achievements

Player achievements were introduced in BadWolfQuiz Web `1.27.0` by issue #442.
They provide persistent long-term milestones without changing normal quiz scoring or trusting client-only unlock signals.

## Identity and persistence

Achievement records are stored in the main SQLite database. When a stable Bad Wolf account is known, achievements are associated with that account. Anonymous gameplay falls back to the host plus normalized player name. A signed-in player may be associated with the current runtime immediately, but matching anonymous nickname history is adopted into the account only after that player remains an eligible participant in a successfully finished game with the same host.

The persistence model also stores game/account and user-question/account links used by achievements that depend on gameplay identity or developer conversations. One-time achievements are de-duplicated before persistence, so evaluating the same completed game again does not create duplicate unlocks.

Active-game achievement runtime state is included in the normal active-game snapshot. Pending event milestones therefore survive application restart together with the rest of the recoverable game state.

### Finished-game boundary and account adoption

Gameplay-derived achievement history is authoritative only for persisted games whose status is `Finished`. Running, interrupted, abandoned, or otherwise unfinished games do not contribute to completed-game counts, correct-answer totals, wins, streaks, score milestones, tag/media progress, longevity, peer-rating progress, or gameplay-event unlocks. Gameplay achievements are finalized from this finished-game history when a completed game is persisted. Account/community achievements that do not depend on game completion can still unlock immediately from their authoritative server-side event.

Stored `GamePlayer` history records carry `CountsForAchievementHistory`. Players who are still legitimate participants when the game finishes are stored with the flag enabled. Players removed by the host are kept in stored game/audit history with the flag disabled and are excluded from achievement metrics, winner comparisons, gameplay-event unlocks, peer-rating progress, and account-history adoption. If a removed player is restored and finishes as a participant, the flag is enabled normally. Legacy stored players created before this marker existed default to enabled because their historical removed state cannot be reconstructed safely.

Signing in during a game only records the account as a candidate/current-game identity. It does not immediately claim old `HostId + normalized nickname` history. After an eligible finished-game participation confirms the identity, the service links eligible finished appearances for that host/nickname to the account and adopts the matching fallback unlock records. Adoption preserves the original achievement code, unlock timestamp, and source game-session ID, including direct event-based achievements, and de-duplicates an achievement by retaining the earliest authoritative unlock. History already claimed by a different account is never reassigned automatically.

Once confirmed game links exist, built-in account achievement metrics aggregate eligible finished-game history across all hosts. A registered player can therefore continue the same built-in counters while playing with different hosts. Future host-defined custom achievements remain host-scoped and must not merge progress across hosts.

The `20260909225025_AddPlayerAchievements` migration creates the achievement tables and indexes. Startup contains compatibility handling for development databases that already contain an older physical version of those tables but do not contain the current migration-history entry.

## Authoritative evaluation

Unlock conditions are evaluated from server-owned game state and persisted history. The client is responsible only for presentation and user actions; it does not decide that an achievement has been earned.

Two kinds of conditions are used:

- historical metrics such as completed games, correct answers, wins, streaks, best final score, accumulated score, peer-rating progress, and long-term participation;
- direct server-observed events such as all-in wagers, first question selection, buzzer races, reward modifiers, anonymous shared wagers, avatar/webcam actions, account/community actions, disconnect/rejoin flows, and other unusual gameplay situations.

Direct events that need the result of the current game are tracked as pending runtime unlocks and persisted through the completed-game achievement evaluation.

## Catalog

The initial catalog in Web `1.27.0` contained 47 achievements. Web `1.29.0` expanded the catalog to 65 achievements, and Web `1.32.0` expands it again to 80. The source of truth for codes, secrecy, metrics, and numeric targets is `PlayerAchievementService.Catalog`.

Long-term progression includes the first completed game, first correct answer, 5 and 25 completed games, 25 and 100 correct answers, first and fifth wins, a five-answer correct streak, score milestones, flawless play, recovery from a negative score, consecutive wins, and one-month / six-month / one-year participation spans. `BigGame` unlocks at a final score of at least **15,000** points; additional score milestones cover 30,000 in one game and 100,000 / 500,000 / 1,000,000 accumulated points.

Account and community milestones include account registration, playing an owned quiz with at least two other players, playing a public quiz hosted by somebody other than its author, Minigames/AI milestones, visiting the project repository from the site, rating a quiz, changing a password, developer conversations, contributor recognition, and receiving maximum peer ratings on answer-review questions.

Gameplay-specific milestones include first question selection, opening the second round, last-to-first comeback wins, final-question reversals, category coverage, silent rounds, all-in outcomes, explicit double/half answer rewards, defeating the immediately previous game's winner, anonymous shared wagers, Four Clues performance, close buzzer races, kick/rejoin behavior, late joining, avatar changes, and webcam use.

### Topic and media milestones

Web `1.29.0` added 18 topic and media achievements. Web `1.32.0` adds seven more tag-driven milestones while preserving the same correct-answer-only progression rule: Star Trek, anime, Counter-Strike, Dota, Ukraine, geography, and history.

The 25-answer topic milestones use normalized question tags. Tag matching trims surrounding whitespace and is case-insensitive. A correct answer advances a given topic at most once even if the question carries multiple matching tags from that topic group.

The Web `1.32.0` aliases are:

- `StarTrekTag` (secret, one correct answer): `Зоряний Шлях`, `Star Trek`;
- `Anime25`: `аніме`, `anime`;
- `CounterStrike25`: `кс`, `cs`, `counter strike`;
- `Dota25`: `dota`, `dota2`;
- `Ukraine25`: `Україна`, `Ukraine`;
- `Geography25`: `географія`, `geography`;
- `History25`: `історія`, `history`.

Earlier 25-answer topic milestones cover films, series, cartoons, animated series, games, Harry Potter, Star Wars, fantasy, science fiction, animals, horror, and music.

`AudioQuestions25` / **На слух** requires 25 correct answers to questions containing an `Audio` content block. `VideoQuestions25` / **На екрані** requires 25 correct answers to questions containing either a `Video` or `YouTube` content block. These media milestones are derived from question content blocks and do not depend on media-related tags.

The one-answer secret topic milestones include Doctor Who, Robocop, Terminator, Mafia / The Godfather, and Star Trek.

### Reward, competition, rating, and longevity milestones

Web `1.32.0` adds milestone types that are not derived from ordinary answer totals:

- `DoubleReward`: unlocks from a correct answer explicitly judged with the `Double` reward modifier.
- `HalfReward`: unlocks from a correct answer explicitly judged with the `Half` reward modifier.
- `TwoWinsInRow`: unlocks when the player's best consecutive finished-game win streak reaches two.
- `BeatPreviousWinner`: unlocks for the current winner when a winner of the immediately previous finished game for the same host also participated in the current game and lost.
- `PeerMaxRatings10`: unlocks after receiving ten maximum five-star ratings from other players on peer-rated answer-review questions. Progress events are de-duplicated per game/question/rater and are adopted together with nickname history when an account becomes available.
- `PlayOneMonth`, `PlaySixMonths`, and `PlayOneYear`: use completed calendar months between the player's first and latest finished-game appearances, with targets of 1, 6, and 12 months.

Double/half reward achievements use the explicit runtime `AnswerRewardModifier` stored on the authoritative answer attempt. They are not inferred from raw point values, which avoids false positives from wagers, Four Clues, multiple-choice reward scaling, or other scoring mechanics.

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

The active-game **Tools** menu also exposes **Player achievements**, which opens a dedicated host-owned, read-only history page. Its **Unlocked in this game** view shows persisted unlocks whose `SourceGameSessionId` belongs to the current stored game and associates each row with the matching current player identity. Its **Players' histories** view lets the host select any current player and inspect that player's complete persisted unlock history, newest first. Signed-in players use their account identity; anonymous players use the existing host + normalized-name fallback identity, so the page follows the same confirmed adoption rules as gameplay without triggering adoption itself.

The history page never calls achievement evaluation or unlock logic. It reads `PlayerAchievements` with no tracking, excludes internal progress-helper rows, refreshes from persistence on every request, and remains available while the game is running. Player selection limits the full-history view to one current player at a time and the result list uses a bounded scrolling region so large histories do not make the host UI unusable. Unknown non-internal achievement codes remain displayable with fallback presentation so future host-defined achievements can plug their own metadata/artwork into the same history surface.

Web `1.32.1` replaces achievement-card emoji with dedicated PNG artwork from `wwwroot/images/achievements/{AchievementCode}.png` in both player and host dialogs. Artwork is shown borderlessly on a dark header area that fades into the card background, and fully transparent PNG margins are ignored when fitting the visible artwork without modifying the source asset.

Cards are ordered consistently in both dialogs: unlocked achievements first, then locked public achievements, then locked secret achievements. Locked public artwork is rendered in grayscale. Locked achievements use the hollow-circle state, unlocked public achievements use the gold check state, and unlocked secret achievements use a distinct revealed-secret eye state with a check inside the pupil. Locked secret achievements keep the shared question-mark placeholder and do not expose their artwork until unlocked.

Achievement cards use the shared responsive dialog layout with a fixed header and independently scrolling body so the title and close action remain visible on smaller screens.

## Localization

Achievement resources follow the site's existing localization policy. English, Ukrainian, and Italian contain localized achievement UI text, names, and descriptions. Russian achievement resources intentionally use the shared `Україна` marker, matching the repository-wide Russian localization rule enforced by regression tests.

Achievement-specific regression coverage verifies that every catalog achievement has a non-empty name and description entry in every supported achievement resource, while the existing repository-wide localization regression continues to enforce the intentional Russian marker policy.

The 15 achievements added in Web `1.32.0` use user-facing English, Ukrainian, and Italian descriptions that describe the subject or action directly rather than exposing the internal tag aliases used for matching.

## Regression coverage

The achievement test suite covers catalog composition and ordering, the 15,000-point Big Game threshold, history metrics, duplicate prevention, account/nickname adoption, current-game highlighting, persistence/recovery, gameplay-specific direct unlocks, explicit double/half reward modifiers, consecutive wins, previous-winner competition, peer five-star rating progress, completed playing months, topic-tag normalization, correct-answer-only topic/media progress, Audio/Video/YouTube block detection, migration compatibility, player/host UI wiring, PNG artwork and cache-busted asset wiring, compact card layout, alpha-margin trimming, unlocked/public/secret card ordering, locked-artwork grayscale presentation, secret reveal state rendering, and localization-resource completeness.
