from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def write(path, text):
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(path, old, new):
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one match, found {count}\n--- needle ---\n{old[:500]}")
    write(path, text.replace(old, new, 1))


def replace_count(path, old, new, expected):
    text = read(path)
    count = text.count(old)
    if count != expected:
        raise RuntimeError(f"{path}: expected {expected} matches, found {count}\n--- needle ---\n{old[:500]}")
    write(path, text.replace(old, new))


# Persist explicit achievement-history eligibility for stored game players.
replace_once(
    "src/BadWolfQuiz.Web/Models/QuizModels.cs",
    """    public DateTime? LastSeenAtUtc { get; set; }\n    public bool IsActive { get; set; } = true;\n\n    public GameSession Session { get; set; } = null!;\n""",
    """    public DateTime? LastSeenAtUtc { get; set; }\n    public bool IsActive { get; set; } = true;\n    public bool CountsForAchievementHistory { get; set; } = true;\n\n    public GameSession Session { get; set; } = null!;\n""",
)

replace_once(
    "src/BadWolfQuiz.Web/Data/QuizDbContext.cs",
    """        modelBuilder.Entity<PlayerGameAccountLink>()\n            .HasOne(x => x.Player)\n""",
    """        modelBuilder.Entity<GamePlayer>()\n            .Property(x => x.CountsForAchievementHistory)\n            .HasDefaultValue(true)\n            .HasSentinel(true);\n\n        modelBuilder.Entity<PlayerGameAccountLink>()\n            .HasOne(x => x.Player)\n""",
)

# Delay host/nickname adoption until a specific eligible finished game confirms the identity,
# and preserve all prior fallback unlock metadata during adoption.
service_path = "src/BadWolfQuiz.Web/Services/PlayerAchievementService.cs"
old_adoption = """    public async Task AdoptHostNicknameHistoryAsync(\n        string? accountId,\n        string? hostId,\n        string playerName,\n        CancellationToken cancellationToken = default)\n    {\n        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(hostId))\n        {\n            return;\n        }\n\n        var playerKey = NormalizePlayerKey(playerName);\n        if (playerKey.Length == 0)\n        {\n            return;\n        }\n\n        var candidates = await db.GamePlayers\n            .IgnoreQueryFilters()\n            .AsNoTracking()\n            .Where(player =>\n                player.Session.HostId == hostId &&\n                player.Session.Status == GameSessionStatus.Finished)\n            .Select(player => new { player.Id, player.Name })\n            .ToListAsync(cancellationToken);\n        var candidateIds = candidates\n            .Where(player => string.Equals(\n                NormalizePlayerKey(player.Name),\n                playerKey,\n                StringComparison.Ordinal))\n            .Select(player => player.Id)\n            .Distinct()\n            .ToArray();\n        if (candidateIds.Length == 0)\n        {\n            return;\n        }\n\n        var linkedIds = (await db.PlayerGameAccountLinks\n                .AsNoTracking()\n                .Where(link => candidateIds.Contains(link.GamePlayerId))\n                .Select(link => link.GamePlayerId)\n                .ToListAsync(cancellationToken))\n            .ToHashSet();\n\n        foreach (var playerId in candidateIds.Where(id => !linkedIds.Contains(id)))\n        {\n            db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink\n            {\n                GamePlayerId = playerId,\n                AccountId = accountId.Trim()\n            });\n        }\n\n        var accountIdentity = PlayerAchievementIdentity.Create(accountId, null, null);\n        var fallbackEvents = await db.PlayerAchievements\n            .AsNoTracking()\n            .Where(item =>\n                item.AccountId == null &&\n                item.HostId == hostId &&\n                item.PlayerKey == playerKey &&\n                item.AchievementCode.StartsWith(PeerMaximumRatingEventPrefix))\n            .ToListAsync(cancellationToken);\n        var accountEventCodes = (await QueryForIdentity(accountIdentity)\n                .AsNoTracking()\n                .Where(item => item.AchievementCode.StartsWith(PeerMaximumRatingEventPrefix))\n                .Select(item => item.AchievementCode)\n                .ToListAsync(cancellationToken))\n            .ToHashSet(StringComparer.Ordinal);\n        foreach (var progressEvent in fallbackEvents)\n        {\n            if (accountEventCodes.Add(progressEvent.AchievementCode))\n            {\n                AddUnlock(\n                    accountIdentity,\n                    progressEvent.AchievementCode,\n                    progressEvent.SourceGameSessionId);\n            }\n        }\n\n        await db.SaveChangesAsync(cancellationToken);\n    }\n"""
new_adoption = """    public async Task AdoptHostNicknameHistoryAsync(\n        string? accountId,\n        string? hostId,\n        string playerName,\n        int confirmedGameSessionId,\n        CancellationToken cancellationToken = default)\n    {\n        if (string.IsNullOrWhiteSpace(accountId) ||\n            string.IsNullOrWhiteSpace(hostId) ||\n            confirmedGameSessionId <= 0)\n        {\n            return;\n        }\n\n        var normalizedAccountId = accountId.Trim();\n        var normalizedHostId = hostId.Trim();\n        var playerKey = NormalizePlayerKey(playerName);\n        if (playerKey.Length == 0)\n        {\n            return;\n        }\n\n        var candidates = await db.GamePlayers\n            .IgnoreQueryFilters()\n            .AsNoTracking()\n            .Where(player =>\n                player.Session.HostId == normalizedHostId &&\n                player.Session.Status == GameSessionStatus.Finished &&\n                player.CountsForAchievementHistory)\n            .Select(player => new\n            {\n                player.Id,\n                player.GameSessionId,\n                player.Name\n            })\n            .ToListAsync(cancellationToken);\n        var matchingCandidates = candidates\n            .Where(player => string.Equals(\n                NormalizePlayerKey(player.Name),\n                playerKey,\n                StringComparison.Ordinal))\n            .ToArray();\n        var confirmedCandidate = matchingCandidates\n            .SingleOrDefault(player => player.GameSessionId == confirmedGameSessionId);\n        if (confirmedCandidate is null)\n        {\n            return;\n        }\n\n        var candidateIds = matchingCandidates\n            .Select(player => player.Id)\n            .Distinct()\n            .ToArray();\n        var existingLinks = await db.PlayerGameAccountLinks\n            .AsNoTracking()\n            .Where(link => candidateIds.Contains(link.GamePlayerId))\n            .Select(link => new { link.GamePlayerId, link.AccountId })\n            .ToListAsync(cancellationToken);\n        var linkByPlayerId = existingLinks\n            .ToDictionary(link => link.GamePlayerId, link => link.AccountId);\n\n        if (linkByPlayerId.TryGetValue(confirmedCandidate.Id, out var confirmedAccountId) &&\n            !string.Equals(confirmedAccountId, normalizedAccountId, StringComparison.Ordinal))\n        {\n            return;\n        }\n\n        var hasConflictingClaim = existingLinks.Any(link =>\n            !string.Equals(link.AccountId, normalizedAccountId, StringComparison.Ordinal));\n\n        bool CanClaimCandidate(int gamePlayerId)\n        {\n            if (gamePlayerId == confirmedCandidate.Id)\n            {\n                return true;\n            }\n            if (linkByPlayerId.TryGetValue(gamePlayerId, out var linkedAccountId))\n            {\n                return string.Equals(\n                    linkedAccountId,\n                    normalizedAccountId,\n                    StringComparison.Ordinal);\n            }\n            return !hasConflictingClaim;\n        }\n\n        var claimableCandidates = matchingCandidates\n            .Where(candidate => CanClaimCandidate(candidate.Id))\n            .ToArray();\n        foreach (var candidate in claimableCandidates)\n        {\n            if (linkByPlayerId.ContainsKey(candidate.Id))\n            {\n                continue;\n            }\n            db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink\n            {\n                GamePlayerId = candidate.Id,\n                AccountId = normalizedAccountId\n            });\n            linkByPlayerId[candidate.Id] = normalizedAccountId;\n        }\n\n        var claimableGameIds = claimableCandidates\n            .Select(candidate => candidate.GameSessionId)\n            .ToHashSet();\n        var fallbackUnlocks = await db.PlayerAchievements\n            .AsNoTracking()\n            .Where(item =>\n                item.AccountId == null &&\n                item.HostId == normalizedHostId &&\n                item.PlayerKey == playerKey)\n            .OrderBy(item => item.UnlockedAtUtc)\n            .ThenBy(item => item.Id)\n            .ToListAsync(cancellationToken);\n        var accountUnlocks = await db.PlayerAchievements\n            .Where(item => item.AccountId == normalizedAccountId)\n            .ToListAsync(cancellationToken);\n        var accountByCode = accountUnlocks\n            .ToDictionary(item => item.AchievementCode, StringComparer.Ordinal);\n\n        foreach (var fallback in fallbackUnlocks)\n        {\n            if (fallback.SourceGameSessionId is { } sourceGameSessionId &&\n                !claimableGameIds.Contains(sourceGameSessionId))\n            {\n                continue;\n            }\n            if (fallback.SourceGameSessionId is null && hasConflictingClaim)\n            {\n                continue;\n            }\n\n            if (accountByCode.TryGetValue(fallback.AchievementCode, out var existing))\n            {\n                if (fallback.UnlockedAtUtc < existing.UnlockedAtUtc ||\n                    fallback.UnlockedAtUtc == existing.UnlockedAtUtc &&\n                    existing.SourceGameSessionId is null &&\n                    fallback.SourceGameSessionId is not null)\n                {\n                    existing.UnlockedAtUtc = fallback.UnlockedAtUtc;\n                    existing.SourceGameSessionId = fallback.SourceGameSessionId;\n                }\n                continue;\n            }\n\n            var adopted = new PlayerAchievement\n            {\n                AccountId = normalizedAccountId,\n                AchievementCode = fallback.AchievementCode,\n                UnlockedAtUtc = fallback.UnlockedAtUtc,\n                SourceGameSessionId = fallback.SourceGameSessionId\n            };\n            db.PlayerAchievements.Add(adopted);\n            accountByCode.Add(adopted.AchievementCode, adopted);\n        }\n\n        await db.SaveChangesAsync(cancellationToken);\n    }\n"""
replace_once(service_path, old_adoption, new_adoption)

replace_once(
    service_path,
    """        if (!string.IsNullOrWhiteSpace(accountId))\n        {\n            await AdoptHostNicknameHistoryAsync(accountId, hostId, playerName, cancellationToken);\n        }\n\n        var identity = PlayerAchievementIdentity.Create(accountId, hostId, playerName);\n""",
    """        var identity = PlayerAchievementIdentity.Create(accountId, hostId, playerName);\n""",
)

replace_once(
    service_path,
    """        var players = await db.GamePlayers\n            .IgnoreQueryFilters()\n            .Where(player => player.GameSessionId == gameSessionId)\n""",
    """        var players = await db.GamePlayers\n            .IgnoreQueryFilters()\n            .Where(player =>\n                player.GameSessionId == gameSessionId &&\n                player.CountsForAchievementHistory)\n""",
)

replace_once(
    service_path,
    """            .Where(player =>\n                player.Session.HostId == hostId &&\n                player.Session.Status == GameSessionStatus.Finished)\n            .Select(player => new PlayerAchievementGameSource(\n""",
    """            .Where(player =>\n                player.Session.HostId == hostId &&\n                player.Session.Status == GameSessionStatus.Finished &&\n                player.CountsForAchievementHistory)\n            .Select(player => new PlayerAchievementGameSource(\n""",
)

replace_once(
    service_path,
    """            .Where(link =>\n                link.AccountId == accountId &&\n                link.Player.Session.Status == GameSessionStatus.Finished)\n            .Select(link => new PlayerAchievementGameSource(\n""",
    """            .Where(link =>\n                link.AccountId == accountId &&\n                link.Player.Session.Status == GameSessionStatus.Finished &&\n                link.Player.CountsForAchievementHistory)\n            .Select(link => new PlayerAchievementGameSource(\n""",
)

replace_count(
    service_path,
    """                player.Session.HostId,\n                player.Session.Players.Count))\n""",
    """                player.Session.HostId,\n                player.Session.Players.Count(item => item.CountsForAchievementHistory)))\n""",
    1,
)
replace_count(
    service_path,
    """                link.Player.Session.HostId,\n                link.Player.Session.Players.Count))\n""",
    """                link.Player.Session.HostId,\n                link.Player.Session.Players.Count(item => item.CountsForAchievementHistory)))\n""",
    1,
)

replace_once(
    service_path,
    """            .Where(player => gameIds.Contains(player.GameSessionId))\n            .Select(player => new PlayerAchievementGameScoreSource(player.GameSessionId, player.TotalScore))\n""",
    """            .Where(player =>\n                gameIds.Contains(player.GameSessionId) &&\n                player.CountsForAchievementHistory)\n            .Select(player => new PlayerAchievementGameScoreSource(player.GameSessionId, player.TotalScore))\n""",
)

# Completed-game persistence keeps removed players for audit/history, but only final participants count
# for achievements and account adoption.
history_path = "src/BadWolfQuiz.Web/Services/GameHistoryStore.cs"
replace_once(
    history_path,
    """        stored.FinishedAtUtc = DateTime.UtcNow;\n\n        var players = runtime.AllPlayers.ToDictionary(\n""",
    """        stored.FinishedAtUtc = DateTime.UtcNow;\n\n        var eligiblePlayerIds = runtime.Players\n            .Select(player => player.Id)\n            .ToHashSet();\n        var players = runtime.AllPlayers.ToDictionary(\n""",
)
replace_once(
    history_path,
    """                JoinedAtUtc = player.JoinedAtUtc.UtcDateTime,\n                LastSeenAtUtc = stored.FinishedAtUtc,\n                IsActive = false\n""",
    """                JoinedAtUtc = player.JoinedAtUtc.UtcDateTime,\n                LastSeenAtUtc = stored.FinishedAtUtc,\n                IsActive = false,\n                CountsForAchievementHistory = eligiblePlayerIds.Contains(player.Id)\n""",
)
replace_once(
    history_path,
    """        foreach (var pair in players)\n        {\n            stored.Players.Add(pair.Value);\n\n            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(\n                registration,\n                pair.Key);\n            if (!string.IsNullOrWhiteSpace(accountId))\n            {\n                db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink\n                {\n                    Player = pair.Value,\n                    AccountId = accountId\n                });\n            }\n        }\n""",
    """        foreach (var pair in players)\n        {\n            stored.Players.Add(pair.Value);\n        }\n""",
)
replace_once(
    history_path,
    """        await db.SaveChangesAsync(cancellationToken);\n\n        var achievements = new PlayerAchievementService(db);\n        var peerMaximumRatingEvents = new List<PlayerMaximumRatingEventSource>();\n""",
    """        await db.SaveChangesAsync(cancellationToken);\n\n        var achievements = new PlayerAchievementService(db);\n        foreach (var player in runtime.Players)\n        {\n            var accountId = PlayerAchievementRuntimeState.GetPlayerAccountId(\n                registration,\n                player.Id);\n            if (!string.IsNullOrWhiteSpace(accountId))\n            {\n                await achievements.AdoptHostNicknameHistoryAsync(\n                    accountId,\n                    registration.HostId,\n                    player.Name,\n                    stored.Id,\n                    cancellationToken);\n            }\n        }\n\n        var peerMaximumRatingEvents = new List<PlayerMaximumRatingEventSource>();\n""",
)
replace_once(
    history_path,
    """            foreach (var rating in review.Ratings.Where(item => item.Stars == 5))\n            {\n                var answerPlayer = runtime.AllPlayers.SingleOrDefault(player =>\n                    player.Id == rating.AnswerPlayerId);\n                if (answerPlayer is null)\n                {\n                    continue;\n                }\n""",
    """            foreach (var rating in review.Ratings.Where(item => item.Stars == 5))\n            {\n                if (!eligiblePlayerIds.Contains(rating.AnswerPlayerId) ||\n                    !eligiblePlayerIds.Contains(rating.RaterPlayerId))\n                {\n                    continue;\n                }\n\n                var answerPlayer = runtime.Players.SingleOrDefault(player =>\n                    player.Id == rating.AnswerPlayerId);\n                if (answerPlayer is null)\n                {\n                    continue;\n                }\n""",
)
replace_count(
    history_path,
    """                var player = runtime.AllPlayers.SingleOrDefault(item => item.Id == wager.PlayerId);\n""",
    """                var player = runtime.Players.SingleOrDefault(item => item.Id == wager.PlayerId);\n""",
    1,
)
replace_count(
    history_path,
    """            var player = runtime.AllPlayers.SingleOrDefault(item => item.Id == submission.PlayerId);\n""",
    """            var player = runtime.Players.SingleOrDefault(item => item.Id == submission.PlayerId);\n""",
    1,
)

text = read(history_path)
start = text.index("    private static async Task UnlockGameplayAchievementsAsync(")
end = text.index("    private async Task UnlockBeatPreviousWinnerAsync(", start)
section = text[start:end]
if "runtime.AllPlayers" not in section:
    raise RuntimeError("GameHistoryStore: expected runtime.AllPlayers inside gameplay-achievement section")
section = section.replace("runtime.AllPlayers", "runtime.Players")
text = text[:start] + section + text[end:]
write(history_path, text)

replace_once(
    history_path,
    """        if (string.IsNullOrWhiteSpace(registration.HostId) || runtime.AllPlayers.Count == 0)\n""",
    """        if (string.IsNullOrWhiteSpace(registration.HostId) || runtime.Players.Count == 0)\n""",
)
replace_once(
    history_path,
    """            .Where(player => player.GameSessionId == previousGameId)\n            .Select(player => new\n""",
    """            .Where(player =>\n                player.GameSessionId == previousGameId &&\n                player.CountsForAchievementHistory)\n            .Select(player => new\n""",
)
replace_count(
    history_path,
    "runtime.AllPlayers.Max(player => player.Score)",
    "runtime.Players.Max(player => player.Score)",
    1,
)
replace_count(
    history_path,
    """        var currentPlayers = runtime.AllPlayers\n""",
    """        var currentPlayers = runtime.Players\n""",
    1,
)

# Document the agreed lifecycle/security model.
docs_path = "docs/features/player-achievements.md"
replace_once(
    docs_path,
    """Achievement records are stored in the main SQLite database. When a stable Bad Wolf account is known, achievements are associated with that account. Anonymous gameplay can fall back to the host plus normalized player name, and later account linking can adopt matching nickname history.\n""",
    """Achievement records are stored in the main SQLite database. When a stable Bad Wolf account is known, achievements are associated with that account. Anonymous gameplay falls back to the host plus normalized player name. A signed-in player may be associated with the current runtime immediately, but matching anonymous nickname history is adopted into the account only after that player remains an eligible participant in a successfully finished game with the same host.\n""",
)
replace_once(
    docs_path,
    """Active-game achievement runtime state is included in the normal active-game snapshot. Pending event milestones therefore survive application restart together with the rest of the recoverable game state.\n\nThe `20260909225025_AddPlayerAchievements` migration creates the achievement tables and indexes. Startup contains compatibility handling for development databases that already contain an older physical version of those tables but do not contain the current migration-history entry.\n""",
    """Active-game achievement runtime state is included in the normal active-game snapshot. Pending event milestones therefore survive application restart together with the rest of the recoverable game state.\n\n### Finished-game boundary and account adoption\n\nGameplay-derived achievement history is authoritative only for persisted games whose status is `Finished`. Running, interrupted, abandoned, or otherwise unfinished games do not contribute to completed-game counts, correct-answer totals, wins, streaks, score milestones, tag/media progress, longevity, peer-rating progress, or gameplay-event unlocks. Gameplay achievements are finalized from this finished-game history when a completed game is persisted. Account/community achievements that do not depend on game completion can still unlock immediately from their authoritative server-side event.\n\nStored `GamePlayer` history records carry `CountsForAchievementHistory`. Players who are still legitimate participants when the game finishes are stored with the flag enabled. Players removed by the host are kept in stored game/audit history with the flag disabled and are excluded from achievement metrics, winner comparisons, gameplay-event unlocks, peer-rating progress, and account-history adoption. If a removed player is restored and finishes as a participant, the flag is enabled normally. Legacy stored players created before this marker existed default to enabled because their historical removed state cannot be reconstructed safely.\n\nSigning in during a game only records the account as a candidate/current-game identity. It does not immediately claim old `HostId + normalized nickname` history. After an eligible finished-game participation confirms the identity, the service links eligible finished appearances for that host/nickname to the account and adopts the matching fallback unlock records. Adoption preserves the original achievement code, unlock timestamp, and source game-session ID, including direct event-based achievements, and de-duplicates an achievement by retaining the earliest authoritative unlock. History already claimed by a different account is never reassigned automatically.\n\nOnce confirmed game links exist, built-in account achievement metrics aggregate eligible finished-game history across all hosts. A registered player can therefore continue the same built-in counters while playing with different hosts. Future host-defined custom achievements remain host-scoped and must not merge progress across hosts.\n\nThe `20260909225025_AddPlayerAchievements` migration creates the achievement tables and indexes. Startup contains compatibility handling for development databases that already contain an older physical version of those tables but do not contain the current migration-history entry.\n""",
)

# Regression: stored removed players remain available but cannot enter achievement history.
game_history_tests = "tests/BadWolfQuiz.Web.Tests/GameHistoryStoreTests.cs"
replace_once(
    game_history_tests,
    """    [Fact]\n    public async Task SaveCompletedGameAsync_updates_existing_history_after_correction()\n""",
    """    [Fact]\n    public async Task SaveCompletedGameAsync_marks_removed_players_ineligible_for_achievement_history()\n    {\n        await using var fixture = await HistoryFixture.CreateAsync();\n        var registration = fixture.CreateCompletedGame();\n        var removed = registration.Session.AllPlayers.Single(player => player.Name == \"Mickey\");\n        registration.Session.RemovePlayer(removed.Id);\n\n        var saved = await fixture.Store.SaveCompletedGameAsync(registration);\n\n        Assert.True(saved);\n        var players = await fixture.Db.GamePlayers\n            .IgnoreQueryFilters()\n            .AsNoTracking()\n            .OrderBy(player => player.Name)\n            .ToListAsync();\n        Assert.True(players.Single(player => player.Name == \"Rose\").CountsForAchievementHistory);\n        Assert.False(players.Single(player => player.Name == \"Mickey\").CountsForAchievementHistory);\n    }\n\n    [Fact]\n    public async Task SaveCompletedGameAsync_updates_existing_history_after_correction()\n""",
)

# Service-level regressions for delayed adoption, direct-unlock preservation, exclusion, conflicts,
# and cross-host account aggregation.
service_tests = "tests/BadWolfQuiz.Web.Tests/PlayerAchievementServiceTests.cs"
insert_marker = """    [Fact]\n    public void BuildProgress_marks_only_unlocks_from_current_game_as_new_and_sorts_unlocks_first()\n"""
new_tests = r'''    [Fact]
    public async Task LoadForPlayerAsync_does_not_adopt_host_history_before_finished_game_confirmation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var host = CreateHost("host-delayed", "host-delayed@example.test");
        var account = CreateHost("account-delayed", "account-delayed@example.test");
        var quiz = CreateQuiz(host, "Delayed adoption");
        var previous = CreateStoredGame(
            quiz,
            host,
            "DELAY1",
            "Rose",
            1_000,
            GameSessionStatus.Finished,
            countsForAchievementHistory: true,
            DateTime.UtcNow.AddDays(-2));
        db.Hosts.AddRange(host, account);
        db.GameSessions.Add(previous);
        await db.SaveChangesAsync();
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = DateTime.UtcNow.AddDays(-2),
            SourceGameSessionId = previous.Id
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null,
            account.Id);

        Assert.False(await db.PlayerGameAccountLinks
            .AsNoTracking()
            .AnyAsync(link => link.AccountId == account.Id));
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == account.Id &&
                item.AchievementCode == "DoubleReward"));
    }

    [Fact]
    public async Task AdoptHostNicknameHistoryAsync_preserves_unlocks_and_skips_ineligible_or_unfinished_games()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-adopt", "host-adopt@example.test");
        var account = CreateHost("account-adopt", "account-adopt@example.test");
        var quiz = CreateQuiz(host, "Adoption");
        var eligible = CreateStoredGame(
            quiz, host, "ADOPT1", "Rose", 1_000, GameSessionStatus.Finished, true, now.AddDays(-30));
        var removed = CreateStoredGame(
            quiz, host, "ADOPT2", "Rose", 30_000, GameSessionStatus.Finished, false, now.AddDays(-20));
        var unfinished = CreateStoredGame(
            quiz, host, "ADOPT3", "Rose", 30_000, GameSessionStatus.Running, true, now.AddDays(-10));
        var confirmed = CreateStoredGame(
            quiz, host, "ADOPT4", "Rose", 2_000, GameSessionStatus.Finished, true, now);
        db.Hosts.AddRange(host, account);
        db.GameSessions.AddRange(eligible, removed, unfinished, confirmed);
        await db.SaveChangesAsync();

        var playerKey = PlayerAchievementService.NormalizePlayerKey("Rose");
        var originalUnlockAt = now.AddDays(-29);
        db.PlayerAchievements.AddRange(
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = originalUnlockAt,
                SourceGameSessionId = eligible.Id
            },
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "AvatarChanged",
                UnlockedAtUtc = now.AddDays(-28),
                SourceGameSessionId = null
            },
            new PlayerAchievement
            {
                HostId = host.Id,
                PlayerKey = playerKey,
                AchievementCode = "HalfReward",
                UnlockedAtUtc = now.AddDays(-19),
                SourceGameSessionId = removed.Id
            },
            new PlayerAchievement
            {
                AccountId = account.Id,
                AchievementCode = "DoubleReward",
                UnlockedAtUtc = now.AddDays(-1),
                SourceGameSessionId = confirmed.Id
            });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.AdoptHostNicknameHistoryAsync(
            account.Id,
            host.Id,
            "Rose",
            confirmed.Id);

        var links = await db.PlayerGameAccountLinks
            .AsNoTracking()
            .Where(link => link.AccountId == account.Id)
            .Select(link => link.GamePlayerId)
            .ToListAsync();
        Assert.Contains(eligible.Players.Single().Id, links);
        Assert.Contains(confirmed.Players.Single().Id, links);
        Assert.DoesNotContain(removed.Players.Single().Id, links);
        Assert.DoesNotContain(unfinished.Players.Single().Id, links);

        var accountUnlocks = await db.PlayerAchievements
            .AsNoTracking()
            .Where(item => item.AccountId == account.Id)
            .ToListAsync();
        var doubleReward = accountUnlocks.Single(item => item.AchievementCode == "DoubleReward");
        Assert.Equal(originalUnlockAt, doubleReward.UnlockedAtUtc);
        Assert.Equal(eligible.Id, doubleReward.SourceGameSessionId);
        Assert.Contains(accountUnlocks, item => item.AchievementCode == "AvatarChanged");
        Assert.DoesNotContain(accountUnlocks, item => item.AchievementCode == "HalfReward");
    }

    [Fact]
    public async Task AdoptHostNicknameHistoryAsync_does_not_steal_history_claimed_by_another_account()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 11, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-conflict", "host-conflict@example.test");
        var firstAccount = CreateHost("account-first", "account-first@example.test");
        var secondAccount = CreateHost("account-second", "account-second@example.test");
        var quiz = CreateQuiz(host, "Conflicting adoption");
        var alreadyClaimed = CreateStoredGame(
            quiz, host, "CLAIM1", "Rose", 1_000, GameSessionStatus.Finished, true, now.AddDays(-10));
        var confirmed = CreateStoredGame(
            quiz, host, "CLAIM2", "Rose", 1_000, GameSessionStatus.Finished, true, now);
        db.Hosts.AddRange(host, firstAccount, secondAccount);
        db.GameSessions.AddRange(alreadyClaimed, confirmed);
        await db.SaveChangesAsync();
        db.PlayerGameAccountLinks.Add(new PlayerGameAccountLink
        {
            GamePlayerId = alreadyClaimed.Players.Single().Id,
            AccountId = firstAccount.Id
        });
        db.PlayerAchievements.Add(new PlayerAchievement
        {
            HostId = host.Id,
            PlayerKey = PlayerAchievementService.NormalizePlayerKey("Rose"),
            AchievementCode = "DoubleReward",
            UnlockedAtUtc = now.AddDays(-9),
            SourceGameSessionId = alreadyClaimed.Id
        });
        await db.SaveChangesAsync();

        var service = new PlayerAchievementService(db);
        await service.AdoptHostNicknameHistoryAsync(
            secondAccount.Id,
            host.Id,
            "Rose",
            confirmed.Id);

        Assert.Equal(
            firstAccount.Id,
            await db.PlayerGameAccountLinks
                .Where(link => link.GamePlayerId == alreadyClaimed.Players.Single().Id)
                .Select(link => link.AccountId)
                .SingleAsync());
        Assert.Equal(
            secondAccount.Id,
            await db.PlayerGameAccountLinks
                .Where(link => link.GamePlayerId == confirmed.Players.Single().Id)
                .Select(link => link.AccountId)
                .SingleAsync());
        Assert.False(await db.PlayerAchievements
            .AsNoTracking()
            .AnyAsync(item =>
                item.AccountId == secondAccount.Id &&
                item.AchievementCode == "DoubleReward"));
    }

    [Fact]
    public async Task LoadForPlayerAsync_ignores_ineligible_and_unfinished_game_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
        var host = CreateHost("host-filter", "host-filter@example.test");
        var quiz = CreateQuiz(host, "History filtering");
        var eligible = CreateStoredGame(
            quiz, host, "FILTER1", "Rose", 5_000, GameSessionStatus.Finished, true, now.AddDays(-3));
        var removed = CreateStoredGame(
            quiz, host, "FILTER2", "Rose", 30_000, GameSessionStatus.Finished, false, now.AddDays(-2));
        var unfinished = CreateStoredGame(
            quiz, host, "FILTER3", "Rose", 50_000, GameSessionStatus.Running, true, now.AddDays(-1));
        db.Hosts.Add(host);
        db.GameSessions.AddRange(eligible, removed, unfinished);
        await db.SaveChangesAsync();

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            host.Id,
            "Rose",
            currentGameCode: null);

        Assert.Equal(1, progress.Single(item => item.Code == "Regular").Progress);
        Assert.Equal(5_000, progress.Single(item => item.Code == "BigGame").Progress);
        Assert.Equal(5_000, progress.Single(item => item.Code == "TotalScore100K").Progress);
    }

    [Fact]
    public async Task Account_history_combines_eligible_finished_games_across_hosts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<QuizDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new QuizDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 9, 11, 13, 0, 0, DateTimeKind.Utc);
        var account = CreateHost("account-global", "account-global@example.test");
        var hostA = CreateHost("host-global-a", "host-global-a@example.test");
        var hostB = CreateHost("host-global-b", "host-global-b@example.test");
        var quizA = CreateQuiz(hostA, "Host A");
        var quizB = CreateQuiz(hostB, "Host B");
        var gameA = CreateStoredGame(
            quizA, hostA, "GLOBAL1", "Rose", 10_000, GameSessionStatus.Finished, true, now.AddDays(-2));
        var gameB = CreateStoredGame(
            quizB, hostB, "GLOBAL2", "Rose", 20_000, GameSessionStatus.Finished, true, now.AddDays(-1));
        db.Hosts.AddRange(account, hostA, hostB);
        db.GameSessions.AddRange(gameA, gameB);
        await db.SaveChangesAsync();
        db.PlayerGameAccountLinks.AddRange(
            new PlayerGameAccountLink
            {
                GamePlayerId = gameA.Players.Single().Id,
                AccountId = account.Id
            },
            new PlayerGameAccountLink
            {
                GamePlayerId = gameB.Players.Single().Id,
                AccountId = account.Id
            });
        await db.SaveChangesAsync();

        var progress = await new PlayerAchievementService(db).LoadForPlayerAsync(
            hostA.Id,
            "Rose",
            currentGameCode: null,
            account.Id);

        Assert.Equal(2, progress.Single(item => item.Code == "Regular").Progress);
        Assert.Equal(30_000, progress.Single(item => item.Code == "TotalScore100K").Progress);
        Assert.True(progress.Single(item => item.Code == "Score30K").IsUnlocked == false);
    }

    private static HostAccount CreateHost(string id, string email) => new()
    {
        Id = id,
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        PasswordHash = "test"
    };

    private static Quiz CreateQuiz(HostAccount host, string title) => new()
    {
        Host = host,
        HostId = host.Id,
        Title = title
    };

    private static GameSession CreateStoredGame(
        Quiz quiz,
        HostAccount host,
        string publicCode,
        string playerName,
        int score,
        GameSessionStatus status,
        bool countsForAchievementHistory,
        DateTime timestamp)
    {
        var session = new GameSession
        {
            Quiz = quiz,
            Host = host,
            HostId = host.Id,
            PublicCode = publicCode,
            Status = status,
            CreatedAtUtc = timestamp.AddHours(-1),
            StartedAtUtc = timestamp.AddMinutes(-50),
            FinishedAtUtc = status == GameSessionStatus.Finished ? timestamp : null
        };
        session.Players.Add(new GamePlayer
        {
            Name = playerName,
            ReconnectToken = string.Empty,
            TotalScore = score,
            JoinedAtUtc = timestamp.AddMinutes(-45),
            LastSeenAtUtc = timestamp,
            IsActive = false,
            CountsForAchievementHistory = countsForAchievementHistory
        });
        return session;
    }

'''
replace_once(service_tests, insert_marker, new_tests + insert_marker)

# Ensure no accidental references to the old adoption signature remain.
for path in [service_path, history_path, service_tests]:
    text = read(path)
    if "AdoptHostNicknameHistoryAsync(accountId, hostId, playerName, cancellationToken)" in text:
        raise RuntimeError(f"{path}: old eager adoption call still present")

print("Issue 622 source, docs, and regression tests patched successfully.")
