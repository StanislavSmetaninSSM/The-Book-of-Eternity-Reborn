# Contract: Daren Silent Gallery Partial Aftermath

## Contract Classification

- **Scope**: Client-owned authored showcase prose in shared C# route data.
- **Source issue**: [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998).
- **Parent**: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955).
- **Scene prerequisite**: [#972](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/972).
- **Sibling boundaries**: completed success sibling [#997](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/997) is preserved; fail sibling [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999) remains future work.
- **Downstream boundaries**: completed result trios [#1000](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1000)-[#1008](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1008) are preserved.
- **GM-facing contract impact**: none. This feature does not change GM-authored mechanics, prompts, pending/control files, validation, canonical state, examples, manifests, commands, response fields, or runtime contracts.
- **Console/browser authority**: both clients consume the same shared C# Daren route prose. No browser-only or console-only copy fork is allowed.

## Result Surface

The only authored route result text allowed to change is:

```text
chapterId: stealth_crossing
title: Галерея без звука
actionId: stealth_crossing_action
action label: Пройти галерею без шума
grade: partial
current text: Один страж шевелится от скрипа; сомнение уже тянется к фонарю, но Дарен удерживает тишину до открытых глаз.
```

## Invariants To Preserve

- `stealth_crossing` beat id and title.
- `stealth_crossing_action` action id and label.
- `StealthNoise` check type.
- Dexterity primary characteristic.
- Base difficulty and StealthNoise config values.
- Routing targets for success, partial, and fail.
- Success/partial/fail grade identities.
- Score deltas for all three grades.
- Reward tiers, reward profile persistence, New Game Ink Feather grants, and terminal ending behavior.
- Browser/frontend files and API/endpoints.
- Runtime state, file-backed state, pending/control surfaces, validation, and normalizers.
- #997 success result text.
- #999 fail result text.
- Downstream `guard_interrogation_action`, `lock_pick_action`, and `rune_memory_action` result texts.

## Partial Outcome Semantics

The new partial prose must communicate all of these at player-facing story level:

1. Daren still crosses the gallery and route continuity proceeds toward `guard_interrogation` / "Ключник в галерее".
2. The outcome is not clean. A trace, doubt, delayed sound, stirred guard, lantern sweep, remembered detail, dust mark, journal risk, or comparable consequence remains.
3. The consequence is credible but not a full alarm/fail. The text must not imply that the route should have branched away from `guard_interrogation`.
4. Daren remains the active point-of-view protagonist; the prose should show his body control, breath, hand placement, boot placement, and tactical judgment.
5. The gallery should feel concrete: floorboards or parquet, portrait frames/glass, dust or stale air, curtains/doors, sleeping guard or lantern presence, and service-door/keykeeper continuity.
6. The text remains in-world Russian prose and must not mention implementation or mechanic/debug terms.

## Test Contract

A focused `DarenQteShowcaseTests` guard should reject the current one-sentence partial text and check:

- route/action ids and title;
- action label, check type, primary characteristic, base difficulty, StealthNoise config, and routing;
- success/partial/fail score deltas;
- substantial length and sentence count;
- Daren active POV;
- grouped motif coverage for gallery surfaces/listening atmosphere, Daren body/breath control, floorboard/noise trouble, partial cost/suspicion/evidence, achieved passage, and keykeeper/service-door continuity;
- absence of default player-facing technical terms;
- preservation of #997 success and #999 fail surfaces.

## Verification Contract

Before merge, local verification must include:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "FullyQualifiedName~DarenQteShowcaseTests" \
  --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "DarenQteShowcaseTests|QteSceneServiceTests|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" \
  --logger "console;verbosity=minimal"

git diff --check origin/main...HEAD
```

Builds and code-focused static scans are required before final PR/merge. Frontend verification is required only if browser/frontend files change or a browser rendering bug is discovered.
