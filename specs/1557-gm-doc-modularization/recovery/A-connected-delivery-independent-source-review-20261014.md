# #1557 connected delivery/AST independent SOURCE review

**Verdict: BLOCKED — three confirmed Important findings; no Critical finding.**

Reviewed candidate `b7639f1a05bc4e10957cbc8d3f6405fd5ab1aafa`, base `4948671e2878f70bd2aa66b0ec17feb3d2089aa6`, in `/workspace/1557-gm-doc-modularization-cloud-20261013`. This is the independent retained reviewer assigned as Astra XHigh; no model/effort substitution or delegation was requested or performed. The tools expose no independent model/effort telemetry.

This was read-only source/fixture/selection review. No Git, project/build/catalog/discovery/PowerShell parsing/test/runtime/fault/provider operation occurred. Only this external report was written. All new execution remains UNRUN.

## 1. Important — cleanup exceeds the verified process scope

`BookOfEternityClient.TestSupport/GmDocumentationDeliveryScenario.cs:210–238, 315–340`.

`Census` inserts every unexpected direct child of the retained pwsh into the signalling inventory as `unexpected-owned-descendant` (330–333). Cleanup subsequently signals every retained entry and uses `Kill(entireProcessTree: true)` (221), after checking only that entry's birth/argv. The recursive kill selects additional descendants without the fixture's exact helper signature or individual identity checks. Even the already-detected unexpected child is deliberately signalled. Adding an issue does not prevent signalling or root deletion.

Concrete failure scenario: while the owned pwsh is still alive, census detects a child that is not the exact package-DLL/`--gm-main-operation`/unique-root helper. The code records an unknown-scope issue, kills that process/tree anyway, and may delete both package/session roots after exit. This contradicts the stated exact pwsh/helper scope and retention on unknown ownership. A second branch has the same retention problem: if child creation succeeds but its initial identity read fails, `original` remains null; later child exit and null pipe tasks can satisfy lines229–230 without an established helper census.

**Required bounded correction:** distinguish observed unknown processes from identities authorized for signalling; never recursively delegate descendant selection. Signal only individually revalidated admissible identities, retain an unresolved-ownership failure, and forbid root removal when the initial ownership/pipe/census proof was never established or an unknown descendant remains unresolved. Preserve the existing absolute deadline and diagnostic retention. This does not require an orphan-survival fault suite or a new process supervisor.

## 2. Important — full-source oracles do not prove the promised final dispatch scope

`BookOfEternityClient.TestSupport/GmDocumentationDeliveryRules.cs:18–25, 42–80`; actual daemon source at192, 2663–2669, 5763–5775, 5883–5904.

Two source-level counterexamples follow directly from the predicates; neither was executed:

- Appending a root `$script:GmSourceFallbackDirective = ""` after the startup writer is exempted by lines23–25. The legitimate early empty initializer and a late destructive reset are indistinguishable. The authoritative definition and message variable references remain present, so this guard would not report the lost effective ban.
- Appending `$message += $script:GmDocPathDirective` after `Build-QteEffectResolutionDispatchMessage -Request $request` at daemon5763 injects ordinary documentation into the actual receipt dispatch. The builder remains pristine and the receipt function still contains all three required substrings. Lines65–80 therefore do not diagnose that injection: the actual qte-effect dispatch/argument/assignment chain is absent from the checked dispatch loop.

The first-Mortal branch similarly relies on a qualifying builder return somewhere, while its assignment is filtered out of ordinary initializer validation; that alone is not a connection to the actual dispatch argument. These are gaps in the explicitly claimed finite source responsibility, not evidence of a current production prompt mutation.

**Required bounded correction:** admit only the known early empty initializer before the real writer, reject later effective directive resets, and bind the existing bootstrap and receipt message builders to their actual dispatch argument with their intervening writes and existing receipt eligibility checks. Use the closed shapes of these named functions rather than a general PowerShell analyser. Add targeted source-mutation controls for the late-empty reset and post-builder receipt-message injection, with a valid seed reaching the intended guards. Preserve current production bytes and keep source mutation evidence distinct from real dispatch execution.

## 3. Important — fenced examples can counterfeit a required heading/alias

`BookOfEternityClient.TestSupport/GmDocumentationDeliveryRules.cs:124–140`.

`Anchors` scans the complete raw Markdown for ATX headings and `<a id=...>` without excluding fenced examples. A target whose only `# Only fenced` line occurs inside a fenced Markdown example supplies `only-fenced` to the accepted anchor set, although there is no rendered heading. The same applies to an alias shown as code. A required link to that fragment consequently passes the current verifier. The existing broken-anchor case only supplies an entirely absent fragment and does not cover this false positive.

**Required bounded correction:** exclude fenced example contents when extracting actual headings/aliases, retaining the supported real heading/alias and duplicate-slug rules. Add a narrow owned-memory control whose requested anchor exists only inside a fenced example. No recursive document crawler or new production Markdown framework is needed.

## Unaffected source and selection assessment

The actual package source is chosen from the current test configuration; its app DLL is compared with the loaded GameEngine assembly, the daemon is compared with source, and all package files are pinned. The original production launcher selects the sibling DLL and calls the real `--gm-main-operation --root` entry. The existing directory initializer supplies an empty unique session. The AST driver selects the unique root EndBlock two-command startup with a complete predecessor and exact successor, derives the original UTF-8/BOM slice from UTF-16 offsets, and reparses that complete prefix. No fabricated copy substitutes for the writer in the real delivery case. The 36-document role/path inventory is independent of daemon `docSpecs` and the resulting manifest.

The real Fact places producer/exit/cleanup assertions before its missing-module issue oracle. The current unimplemented 22 new delivered documents and current source/router defects can yield intended RED only after actual package/API/ownership prerequisites pass. Any earlier failure must be classified separately. The five AST mutation cases use a separate nonexecuting source seed; memory/link controls do not claim real writer success.

Independently read all four new executable sources and the selector/catalog entries. Static inventory is **6 methods / 12 cases / 2 integration descriptors**: real writer1/1 and source/closure5/11, category budgets2+2/default9. The catalog contains731 nodes;11584 methods remains provisional, not discovery. The selected responsibilities are appropriately narrow; pure44 and primary FR/J1 cohorts are excluded. Corrections must update declared case counts if controls are added. The physical symlink receipt's `CleanupOwnedRoot`/`Removed` shape must be interpreted as emitted, not as another fixture's receipt schema.

Independently recomputed the author-report hash `43e445e9a4b66914d1b9a7f76c3a617f159318e439058290ee42aa5bdd0a9907` and **all23 source/protected pins**, with zero mismatches. Key source hashes: Scenario `84812e8824bff5b4c2bd2d957722d233d2de93d55cd767a90347930692b15f2f`; Rules `4dc077c5faab21b607caae01bee219979dfe96e8633ff82016989c90b493d692`; fixture `c14f46a3bed6e42568a7df163a280dd392168398c57a12560869df90d573ea92`; driver `556b1ee94cc7c799ff90c9dc91f6cdc956874cfd8809eebce855c14a79987bbe`. Active plan/tasks sections agree. ROOT's published readback records the exact nine-path scope, retained old729 nodes and clean remote/recovery; this review performed no Git operation to repeat that provenance check.

Metadata correctly retains UNRUN and no full-A acceptance, but its claims of exact cleanup ownership and full source-flow coverage must be reconciled with these findings. Prior pure evidence and primary J1 evidence are unaffected. **No preparation or actual execution is released by this blocked gate.** After focused source corrections/review, only the resulting exact finite preparation/catalog/first actual selection is eligible; content moves, production/daemon changes, provider execution, Block_5 and full A remain outside this gate.

FINAL STOPPED.
