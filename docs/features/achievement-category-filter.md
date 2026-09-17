# Achievement category filters

BadWolfQuiz Web `1.47.0` adds a shared category filter to the built-in achievement catalog.

The same filter is available on the authenticated `/Achievements` page, in the player-side achievement dialog in `/Player/Lobby`, and in the host-side individual-player achievement dialog in `/Admin/Games/Lobby`.

The categories are presented in this order:

1. **All** — every built-in achievement.
2. **Quizzes** — quiz-related achievements that do not belong to another category.
3. **Guess What I Play?** — `SoloAi` and `RoomCreatorWin`.
4. **Word Rings** — every built-in achievement whose code starts with `WordRings`.
5. **Out of game** — `Registered`, `GitHubVisitor`, `PasswordChanged`, `DeveloperContacted`, `DeveloperReplied`, and `Contributor`.

`PlayerAchievementCategoryCatalog` is the shared source of truth for category assignment. The player and host surfaces reuse the same filter values instead of maintaining separate client-side mappings.

Filtering is immediate and client-side. The visible unlocked/total counter follows the selected category. On the authenticated `/Achievements` page, the hero summary continues to represent overall account progress. Resetting an achievement keeps the current category selection and recalculates the visible counter.

Filter labels are localized through `AchievementCategoryFilterResource`. English, Ukrainian, and Italian use localized labels; Russian follows the repository-wide `Україна` marker policy.
