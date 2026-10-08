# Final readiness — bounded #1553 integration handoff

Source: [issue1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), accepted `20bdeb6c42a8729acbfdf39d55f758d3e746240f`.
Owner on 2026-10-08 accepts explicit OwnedTerminal/NativeLineage Linux use while actual systemd qualification is deferred. Native Windows verification follows completion/merge in the existing HOME-PC «Лориан-Codex bridge» task; parent coordinates it. No HOME-PC access or merge here.

## Accepted requirements and evidence reused

| Contract | Accepted bounded evidence / current source | Limit |
|---|---|---|
| Trusted-local canonical save/load, generation, rollback/recovery | [filesystem qualification](recovery/load-filesystem-linux-qualification.json), later F1–F3 and T031 slices; existing FileSystemManager + SaveLoadService | Ordinary isolated roots; no power-loss/reboot salvage or owner-save protection |
| Original persistent terminal, T042 immutable paste → observation → submit | terminal14888663 / input1bc9d675; ProductionMainLaunch, BridgeHost, original InputLifetime | Supported VT subset, no arbitrary TUI/readiness promise |
| Main owner/schema1 durable fence + original operation pins | F1f4e7621f / F28b0c416e / F3fc49f271; coordinator and participating consumers | Native lineage/actual I/O conjunction; cold nonterminal refusal, no replay |
| Installed Linux main and retained disabled worker inventory | M14d456d5d; ordinary launcher/ProductionMainLaunch | Explicit NativeLineage only; production workers remain closed |
| Browser rollback, direct gacha and standalone Daren Linux | dc62a88a / 953c48f8 / d73e2cdf; trusted-local existing transactions | Real isolated consumers; historical Windows-only fixtures are not PASS |
| Stop → Load → full refresh → fresh configured session | 23a5b669; actual console/both browser handlers | No-active leaves GM absent; failed/unknown stop/load/refresh blocks restart; generated gameplay through Load and continuation is not qualified |
| Clipboard/audio/auxiliary/desktop helpers | 499ca652 / 0c526b3c / 65674ee8 / 42812722 and associated handoffs | Synthetic bytes/dummy device/controlled launch; actual desktop/device positives separate |
| Real generated/applied game action and reusable relay | [r3 handoff](relay-gm-bounded-handoff.md), [shared relay](relay-reusable-handoff.md); dc1724af/db847f6a | One clean real action via test relay; not Codex/OpenCode compatibility or universal API support |
| Systemd source/controlled S1 + S2A | [S1](systemd-main-s1-handoff.md), [S2A](systemd-main-s2a-handoff.md) | Actual manager S2 and public S3 deferred; backend OFF; explicit SystemdUser never downgrades |
| Category-based CI | [CI platform checkpoint](ci-platform-selection.md) /20bdeb6c | Hosted result must be observed independently; local metadata PASS is not hosted CI PASS |

No completion percentage: at accepted20bdeb6c vs mainf6dc2a1 the branch contains 21,402 changed paths, including 20,846 spec/evidence paths, not a measurable total of product requirements. Historical open umbrella checkboxes are not a claim that their accepted sub-slices are missing.

## Minimal reviewed change and final selection

Independent actual Sol6.1/xhigh design PASS at20bdeb6c establishes a documentation gap, no blocking runtime defect. Add exact game_session/config.json setup, partial consumed fields preserving arbitrary command/model/args/cwd and disabled inventory. Explain empty input profile refusal and --no-autopaste scope. No runtime/defaults, gameplay, GM-authored schema/prompt/example change is needed. Final execution additionally localized a test preparation defect: seven integration cases fail to resolve source fixtures until the existing BOE_REPO_ROOT is explicit. Independent Sol minimal correction design PASS: pin only owned test child source root in the existing runner, caller environment unchanged; SDK/AppContext specifics are not inferred. Post-edit causal GREEN is required.

| Selected category | Reason | Budget |
|---|---|---:|
| gm-main-linux-guide | Execute real inert Read-GameConfig against documented fields; preserve configured profile and disabled inventory | 1min |
| gm-main-linux-launcher | Installed foreground route after later auxiliary/early-exit/presentation changes since M1 | 3min |
| gm-load-installed-profile | Original stop/load/fresh launch consumes installed archive command/model/args/cwd/profile | 3min |
| gm-load-ordinary-schema | Real console/HTTP canonical schema1 generation and complete refresh bundle after current-session/storage changes | 5min |
| gm-main-affected-managed | Exact finalizers/participating/ambiguity/no-replay consumers after bootstrap/cancel/artifact detection changes | 3min |

PlanOnly establishes exact membership. No relay/systemd/crash/resource/frontend/historic cohort replay: retained relay25 and S2A16 runtime source pins (excluding the deliberately changed catalog/selection metadata) are unchanged; accepted evidence remains reusable. Only required catalog metadata routes the mandatory Linux categories to Ubuntu; portable categories keep the current CI convention without native Windows claims.

## Deferred environment and product boundaries

- Actual systemd S2 requires an already-running accessible user manager; S3 public activation follows qualification. No setup or services here; current Linux stays explicit NativeLineage.
- Native Windows ConPTY/Job/full game is scheduled AFTER merge, not a pre-merge PASS condition. Parent assigns the existing desktop task.
- Physical audio, desktop clipboard/associations and live HTML Audio remain unqualified; capability errors/manual fallback are accepted bounded behavior.
- Codex protected read-only SQLite state and OpenCode CONNECT403 remain recorded blockers; no auth/network workaround or fresh requests. The accepted relay is a separate model-generation transport.
- Public production workers/helpers, arbitrary TUI/live browser, user saves and cold exactly-once are not claimed. Generated game Save/Load/fresh continuation remains separate from neutral Load + one real action.

## Merge visibility (read-only)

Default branch is `main`, current known tip f6dc2a1ce3e73f5e6940f686c97b863c9f7a8173. Branch endpoint says protected=true; its summary shows status contexts empty/enforcement off. Detailed classic protection GET returns403 Resource not accessible by integration. Available rulesets and rules/branches/main return[]; these do NOT establish that review/restriction/admin requirements are absent. CODEOWNERS selects @StanislavSmetaninSSM. Parent/maintainer must confirm inaccessible protection and required reviews before merge. No permission/protection change, no merge here.

## Historical F2 checks — unchanged, no new executions

- `BrowserLocalWriteCoordinatorTests.ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction` — Original Linux timeout before callback; legacy physical fixture; NewExecutions=0.
- `BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction` — Original Linux timeout before callback; legacy physical fixture; NewExecutions=0.
- `BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation` — Original Windows physical creation unavailable on Linux; legacy fixture; NewExecutions=0.
- `BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority` — Original Windows physical creation unavailable on Linux; legacy fixture; NewExecutions=0.

Those historical Windows physical fixtures stay unpassed; subsequent real Linux browser rollback/gacha/Daren causal acceptance is separate, and closes the actual Linux consumer defect. No actor/provenance inference from Git author alone.

## Execution/closure

Current state: actual independent Sol6.1/xhigh design/source PASS at20bdeb6c/0be9c979. Guide causal internal2PASS/1FAIL (adapter accepted0) then3/3GREEN; PlanOnly28estimated cases/5descriptors executed0, fresh unit/integration builds passed. Four runtime categories (27cases) and evidence/metadata review, hosted CI, exact-tip remote/readback/fresh recovery pending. Publish WIP before long checks. Record toolchain, exact commands, planned/executed counts, cleanup, hashes and review source SHA in recovery/final-readiness-qualification.json. Stop at parent handoff without merge, services or model calls.
