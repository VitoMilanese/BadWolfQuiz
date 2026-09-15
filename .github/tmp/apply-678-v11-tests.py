from pathlib import Path

path = Path('tests/BadWolfQuiz.Web.Tests/WordRingsGameplayRefinementRegressionTests.cs')
text = path.read_text(encoding='utf-8')
old = '        Assert.Contains("visibleBankWords().length < maximumBankWords", script, StringComparison.Ordinal);'
new = '        Assert.Contains("regularVisibleBankWords().length < maximumBankWords", script, StringComparison.Ordinal);'
if old not in text:
    raise SystemExit('streaming-bank regression assertion not found')
path.write_text(text.replace(old, new, 1), encoding='utf-8')
