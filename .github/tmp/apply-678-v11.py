from pathlib import Path


def rep(path, old, new, count=1):
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    found = text.count(old)
    if found < count:
        raise SystemExit(f"{path}: expected at least {count} occurrence(s), found {found}: {old[:120]!r}")
    text = text.replace(old, new, count)
    p.write_text(text, encoding="utf-8")


css = "src/BadWolfQuiz.Web/wwwroot/css/word-rings-action-cards-v2.css"
rep(css,
    ".word-rings-action-enable {\n    display: flex;\n    align-items: center;\n    gap: 8px;\n    cursor: pointer;\n}\n\n.word-rings-action-enable input {\n    flex: 0 0 auto;\n}",
    ".word-rings-action-enable {\n    display: grid;\n    grid-template-columns: auto minmax(0, 1fr);\n    align-items: center;\n    gap: 8px;\n    width: 100%;\n    min-width: 0;\n    cursor: pointer;\n}\n\n.word-rings-action-enable input {\n    width: auto !important;\n    min-width: 0 !important;\n    min-height: auto !important;\n    margin: 0;\n}\n\n.word-rings-action-enable span {\n    min-width: 0;\n    overflow-wrap: anywhere;\n}")
rep(css,
    "    box-sizing: border-box;\n    width: 100%;\n    min-height: 36px;",
    "    box-sizing: border-box;\n    width: 100%;\n    min-width: 0;\n    min-height: 36px;")
rep(css,
    ".word-rings-action-settings-dialog {\n    width: min(92vw, 520px);\n}\n\n.word-rings-action-settings-card {\n    display: grid;\n    gap: 14px;\n    padding: 18px;\n}",
    ".word-rings-action-settings-dialog {\n    width: min(92vw, 520px);\n    max-width: calc(100vw - 20px);\n    overflow-x: hidden;\n}\n\n.word-rings-action-settings-card {\n    box-sizing: border-box;\n    display: grid;\n    gap: 14px;\n    width: 100%;\n    min-width: 0;\n    max-width: 100%;\n    padding: 18px;\n}\n\n.word-rings-action-settings-card .word-rings-action-config,\n.word-rings-action-settings-card .word-rings-action-config-fields,\n.word-rings-action-settings-card .word-rings-action-config-fields label {\n    min-width: 0;\n    max-width: 100%;\n}")

action_js = "src/BadWolfQuiz.Web/wwwroot/js/word-rings-action-cards-v2.js"
rep(action_js,
    "['Mask', 'Hide some letters in the selected player’s words for one attempt.'],",
    "['Mask', 'Hide some letters in the selected player’s visible words until each affected word is checked.'],")
rep(action_js,
    "['Anagram', 'Shuffle letters in the selected player’s words for one attempt.'],",
    "['Anagram', 'Shuffle letters in the selected player’s visible words until each affected word is checked.'],")
rep(action_js,
    "['Маскування', 'Приховати частину літер у словах обраного гравця на одну спробу.'],",
    "['Маскування', 'Приховати частину літер у видимих словах обраного гравця, доки кожне з них не буде перевірене.'],")
rep(action_js,
    "['Анаграма', 'Перемішати літери в словах обраного гравця на одну спробу.'],",
    "['Анаграма', 'Перемішати літери у видимих словах обраного гравця, доки кожне з них не буде перевірене.'],")
rep(action_js,
    "['Mascheramento', 'Nascondi alcune lettere nelle parole del giocatore scelto per un tentativo.'],",
    "['Mascheramento', 'Nascondi alcune lettere nelle parole visibili del giocatore scelto finché ogni parola interessata non viene verificata.'],")
rep(action_js,
    "['Anagramma', 'Mescola le lettere nelle parole del giocatore scelto per un tentativo.'],",
    "['Anagramma', 'Mescola le lettere nelle parole visibili del giocatore scelto finché ogni parola interessata non viene verificata.'],")
rep(action_js,
    "    let wordSyncQueued = false;\n    const observer = new MutationObserver(() => {",
    "    root.addEventListener('wordrings:bank-rendered', () => {\n        if (isSolo) return;\n        syncMultiplayerVisibleWords();\n        applyWordEffects();\n    });\n\n    let wordSyncQueued = false;\n    const observer = new MutationObserver(() => {")

solo_js = "src/BadWolfQuiz.Web/wwwroot/js/word-rings.js"
rep(solo_js,
    "    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]\n        .filter(token => !token.hidden);\n\n    const replenishWordBank = () => {\n        if (gameOver) return;\n\n        while (visibleBankWords().length < maximumBankWords && queuedWords.length > 0) {\n            const nextWord = queuedWords.shift();\n            if (!nextWord || !expected.has(nextWord)) continue;\n            createBankWord(nextWord);\n        }\n    };\n\n    const trimWordBankToLimit = returnedWord => {\n        let visible = visibleBankWords();\n        while (visible.length > maximumBankWords) {\n            const removable = [...visible]\n                .reverse()\n                .find(token => token.dataset.word !== returnedWord);\n            if (!(removable instanceof HTMLElement)) break;\n\n            const word = removable.dataset.word;\n            removable.remove();\n            if (word) queuedWords.unshift(word);\n            visible = visibleBankWords();\n        }\n    };",
    "    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]\n        .filter(token => !token.hidden);\n    const regularVisibleBankWords = () => visibleBankWords()\n        .filter(token => !token.classList.contains('is-action-temporary-word'));\n\n    const replenishWordBank = () => {\n        if (gameOver) return;\n\n        let guard = queuedWords.length;\n        while (regularVisibleBankWords().length < maximumBankWords && queuedWords.length > 0 && guard-- > 0) {\n            const nextWord = queuedWords.shift();\n            if (!nextWord || !expected.has(nextWord)) continue;\n            const created = createBankWord(nextWord);\n            if (created) continue;\n\n            const existing = wordList.querySelector(`.word-rings-word[data-word=\"${CSS.escape(nextWord)}\"]`);\n            if (existing?.classList.contains('is-action-temporary-word')) queuedWords.push(nextWord);\n        }\n    };\n\n    const trimWordBankToLimit = returnedWord => {\n        let visible = regularVisibleBankWords();\n        while (visible.length > maximumBankWords) {\n            const removable = [...visible]\n                .reverse()\n                .find(token => token.dataset.word !== returnedWord);\n            if (!(removable instanceof HTMLElement)) break;\n\n            const word = removable.dataset.word;\n            removable.remove();\n            if (word) queuedWords.unshift(word);\n            visible = regularVisibleBankWords();\n        }\n    };")

coop_js = "src/BadWolfQuiz.Web/wwwroot/js/word-rings-coop.js"
rep(coop_js,
    "    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]\n        .filter(token => !token.hidden);\n\n    const replenishLocalBank = () => {\n        while (visibleBankWords().length < maximumBankWords && localQueuedWords.length > 0) {\n            createBankWord(localQueuedWords.shift());\n        }\n    };\n\n    const trimLocalBank = returnedWord => {\n        let visible = visibleBankWords();\n        while (visible.length > maximumBankWords) {\n            const removable = [...visible].reverse().find(item => item.dataset.word !== returnedWord);\n            if (!(removable instanceof HTMLElement)) break;\n            if (removable.dataset.word) localQueuedWords.unshift(removable.dataset.word);\n            removable.remove();\n            visible = visibleBankWords();\n        }\n    };\n\n    const renderBank = nextState => {\n        wordList.replaceChildren();\n        for (const word of nextState.bankWords || []) createBankWord(word);\n        localQueuedWords = [...(nextState.queuedWords || [])];\n    };",
    "    const visibleBankWords = () => [...wordList.querySelectorAll('.word-rings-word[data-word]')]\n        .filter(token => !token.hidden);\n    const regularVisibleBankWords = () => visibleBankWords()\n        .filter(token => !token.classList.contains('is-action-temporary-word'));\n\n    const replenishLocalBank = () => {\n        while (regularVisibleBankWords().length < maximumBankWords && localQueuedWords.length > 0) {\n            createBankWord(localQueuedWords.shift());\n        }\n    };\n\n    const trimLocalBank = returnedWord => {\n        let visible = regularVisibleBankWords();\n        while (visible.length > maximumBankWords) {\n            const removable = [...visible].reverse().find(item => item.dataset.word !== returnedWord);\n            if (!(removable instanceof HTMLElement)) break;\n            if (removable.dataset.word) localQueuedWords.unshift(removable.dataset.word);\n            removable.remove();\n            visible = regularVisibleBankWords();\n        }\n    };\n\n    const renderBank = nextState => {\n        const preservedOverflow = new Map(\n            [...wordList.querySelectorAll('.word-rings-word[data-action-overflow-word=\"true\"]')]\n                .filter(token => token instanceof HTMLButtonElement)\n                .map(token => [String(token.dataset.word || '').toLocaleLowerCase(), token]));\n\n        wordList.replaceChildren();\n        for (const word of nextState.bankWords || []) {\n            const key = String(word).toLocaleLowerCase();\n            const preserved = preservedOverflow.get(key);\n            if (preserved) {\n                preservedOverflow.delete(key);\n                delete preserved.dataset.actionOverflowWord;\n                wordList.append(preserved);\n                wireWord?.(preserved);\n            } else {\n                createBankWord(word);\n            }\n        }\n        preservedOverflow.forEach(token => {\n            wordList.append(token);\n            wireWord?.(token);\n        });\n        localQueuedWords = [...(nextState.queuedWords || [])];\n        root.dispatchEvent(new CustomEvent('wordrings:bank-rendered'));\n    };")

service = "src/BadWolfQuiz.Web/Services/WordRingsActionCardCoordinator.cs"
rep(service,
    "        var state = SynchronizeTurnState(roomCode, playerToken);\n        var normalizedWord = word?.Trim() ?? string.Empty;\n        string expectedMembership;",
    "        var state = SynchronizeTurnState(roomCode, playerToken);\n        if (state.DedicatedHostMode)\n        {\n            // In host-controlled rooms the host must always judge the placement first.\n            // Immunity is consumed only if the host marks the attempt as failed.\n            return null;\n        }\n\n        var normalizedWord = word?.Trim() ?? string.Empty;\n        string expectedMembership;")

tag_helper = "src/BadWolfQuiz.Web/TagHelpers/WordRingsActionCardsAssetsTagHelper.cs"
rep(tag_helper, "word-rings-action-cards-v2.css?v=3", "word-rings-action-cards-v2.css?v=4")
rep(tag_helper, "word-rings-action-cards-v2.js?v=3", "word-rings-action-cards-v2.js?v=4")

tests = "tests/BadWolfQuiz.Web.Tests/WordRingsActionCardsRegressionTests.cs"
rep(tests,
    "    [Fact]\n    public void Room_tuning_allows_the_host_to_raise_target_score_to_twenty()",
    "    [Fact]\n    public void Mask_and_anagram_are_consumed_only_for_the_attempted_word()\n    {\n        var root = Path.Combine(Path.GetTempPath(), $\"badwolf-word-rings-obfuscation-{Guid.NewGuid():N}\");\n        Directory.CreateDirectory(root);\n\n        try\n        {\n            var environment = new TestWebHostEnvironment(root);\n            var rooms = WordRingsRoomHostCoordinator.Get(environment);\n            var cards = WordRingsActionCardCoordinator.Get(environment);\n            var host = rooms.CreateRoom(\"Host\", 15, partialScoreEnabled: false, hostChoosesRules: false);\n            _ = rooms.JoinRoom(host.RoomCode, \"Guest\");\n            cards.RegisterRoom(host, enabled: true, correctWordsPerCard: 2, maximumCards: 3);\n            _ = rooms.StartGame(host.RoomCode, host.PlayerToken);\n            cards.BeginRound(host.RoomCode, host.PlayerToken);\n\n            var method = typeof(WordRingsActionCardCoordinator).GetMethod(\n                \"ApplyWordObfuscation\",\n                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);\n            Assert.NotNull(method);\n\n            method.Invoke(cards, [host.RoomCode, host.State.PlayerId, true]);\n            var maskedBefore = cards.GetState(host.RoomCode, host.PlayerToken);\n            Assert.True(maskedBefore.MaskedWords.Count >= 2);\n            var maskedAttempt = maskedBefore.MaskedWords[0];\n            var maskedUntouched = maskedBefore.MaskedWords[1];\n\n            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, maskedAttempt, fullyCorrect: false);\n            var maskedAfter = cards.GetState(host.RoomCode, host.PlayerToken);\n            Assert.DoesNotContain(maskedAfter.MaskedWords, word =>\n                string.Equals(word, maskedAttempt, StringComparison.OrdinalIgnoreCase));\n            Assert.Contains(maskedAfter.MaskedWords, word =>\n                string.Equals(word, maskedUntouched, StringComparison.OrdinalIgnoreCase));\n            Assert.Equal(maskedBefore.MaskedWords.Count - 1, maskedAfter.MaskedWords.Count);\n\n            method.Invoke(cards, [host.RoomCode, host.State.PlayerId, false]);\n            var anagramBefore = cards.GetState(host.RoomCode, host.PlayerToken);\n            Assert.True(anagramBefore.AnagrammedWords.Count >= 2);\n            var anagramAttempt = anagramBefore.AnagrammedWords[0];\n            var anagramUntouched = anagramBefore.AnagrammedWords[1];\n\n            cards.RecordPlacementAttempt(host.RoomCode, host.State.PlayerId, anagramAttempt, fullyCorrect: false);\n            var anagramAfter = cards.GetState(host.RoomCode, host.PlayerToken);\n            Assert.DoesNotContain(anagramAfter.AnagrammedWords, word =>\n                string.Equals(word, anagramAttempt, StringComparison.OrdinalIgnoreCase));\n            Assert.Contains(anagramAfter.AnagrammedWords, word =>\n                string.Equals(word, anagramUntouched, StringComparison.OrdinalIgnoreCase));\n            Assert.Equal(anagramBefore.AnagrammedWords.Count - 1, anagramAfter.AnagrammedWords.Count);\n        }\n        finally\n        {\n            Directory.Delete(root, recursive: true);\n        }\n    }\n\n    [Fact]\n    public void Room_tuning_allows_the_host_to_raise_target_score_to_twenty()")
rep(tests,
    "        Assert.Contains(\"syncMultiplayerVisibleWords\", script, StringComparison.Ordinal);\n        Assert.Contains(\"lastRenderedCardSignature\", script, StringComparison.Ordinal);",
    "        Assert.Contains(\"syncMultiplayerVisibleWords\", script, StringComparison.Ordinal);\n        Assert.Contains(\"wordrings:bank-rendered\", script, StringComparison.Ordinal);\n        Assert.Contains(\"lastRenderedCardSignature\", script, StringComparison.Ordinal);")
rep(tests,
    "        Assert.Contains(\"soundEffectsEnabled\", coopScript, StringComparison.Ordinal);\n        Assert.Contains(\"data-room-sound-enabled\", page, StringComparison.Ordinal);",
    "        Assert.Contains(\"soundEffectsEnabled\", coopScript, StringComparison.Ordinal);\n        Assert.Contains(\"regularVisibleBankWords\", coopScript, StringComparison.Ordinal);\n        Assert.Contains(\"data-action-overflow-word=\\\"true\\\"\", coopScript, StringComparison.Ordinal);\n        Assert.Contains(\"wordrings:bank-rendered\", coopScript, StringComparison.Ordinal);\n        Assert.Contains(\"regularVisibleBankWords\", soloScript, StringComparison.Ordinal);\n        Assert.Contains(\"data-room-sound-enabled\", page, StringComparison.Ordinal);")
rep(tests,
    "        Assert.Contains(\"word-rings-sound-toggle\", css, StringComparison.Ordinal);\n        Assert.Contains(\"word-rings-room-dialog[data-create-room-dialog]\", css, StringComparison.Ordinal);",
    "        Assert.Contains(\"word-rings-sound-toggle\", css, StringComparison.Ordinal);\n        Assert.Contains(\"width: auto !important;\", css, StringComparison.Ordinal);\n        Assert.Contains(\"overflow-x: hidden;\", css, StringComparison.Ordinal);\n        Assert.Contains(\"word-rings-room-dialog[data-create-room-dialog]\", css, StringComparison.Ordinal);")
rep(tests, "word-rings-action-cards-v2.css?v=3", "word-rings-action-cards-v2.css?v=4")
rep(tests, "word-rings-action-cards-v2.js?v=3", "word-rings-action-cards-v2.js?v=4")
rep(tests,
    "        Assert.Contains(\"return false;\", service, StringComparison.Ordinal);",
    "        Assert.Contains(\"if (state.DedicatedHostMode)\", service, StringComparison.Ordinal);\n        Assert.Contains(\"return null;\", service, StringComparison.Ordinal);\n        Assert.Contains(\"return false;\", service, StringComparison.Ordinal);")
