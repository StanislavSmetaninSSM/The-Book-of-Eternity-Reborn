# GM Reading and Preservation Contract — Draft

**Source Issue:** [#1557](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1557). **Status:** new external draft, 2026-10-13. Approved v1 requirements remain authoritative; this contract is proposed engineering detail, not implemented behavior.

## C1. Authoritative payload and owners

The ten afterlife and seven Block_5 pairs are exactly those named in the approved spec and [plan](../plan.md). Rules and examples remain separate flat files. Whole numbered examples preserve all explanatory text, invalid markers and ordered fragments. Named contracts keep their headings/IDs and complete fence bodies. A scenario spanning topics has one owner plus dependencies; it is not copied across modules.

Common core contains complete existing shared obligations, not summaries replacing rules. Original matrix/example/Block_5 paths become short navigation surfaces. Original example numbers and suffixes are stable. Module headers explicitly identify `Read when`, `Prerequisites`, `Author here`, `Never author`, and worked examples without granting any new permissions.

## C2. Complete and reversible content proof

The implementation records `content-map.json` and `navigation-edits.json` under the future #1557 feature. Before moving, freeze source bytes/hash and a gap-free, nonoverlapping partition of every scoped source byte. Each payload block has one destination segment. The verifier must:

1. Validate all original baseline hashes and interval boundaries.
2. Locate exactly one destination for each block; reject missing/duplicate/overlapping ownership and unlisted authoritative copies.
3. Remove only inventoried new wrapper/navigation segments and reverse only exact declared NavigationEdits, with unique offsets and old/new bytes.
4. Restore original block order per source file; concatenate exact bytes; compare byte length and SHA256 to baseline.
5. Independently compare stable IDs, snippet occurrence/order, valid/invalid classification and complete coverage semantics so a mapping mistake cannot be hidden by copied unused text.

An approved path substitution changes only the declared doc reference/anchor, not schema keys, permissions or gameplay prose. Approved conditional-reading replacements are separately inventoried old/new sentences. Their reverse mapping is required even though they intentionally change navigation. D01 is separately gated and cannot be hidden as a path edit. No newline/whitespace normalization, broad regex exclusion or regenerated source copy is accepted as preservation proof.

## C3. Situation routing

| Situation | Required reading contract |
| --- | --- |
| Bootstrap | Existing context-pack manifest and README scope only; copying modules does not authorize or require opening all bodies. |
| Ordinary afterlife | Current request, applicable compact templates/core, realm mode and exact relevant modules; unrelated topic bodies are not blanket mandatory. |
| Scheduler due/catch-up | Scheduler plus modules required by actual impacted actors/pending contracts; combined examples stay whole. |
| Several active contracts | Union of each contract’s prerequisites and exact examples; no first-match choice discarding a second pending contract. |
| Receipt-only task | Existing narrow packet/receipt restrictions; no new ordinary scene/actor/scheduler obligation. |
| Spiritual wound frontier | Exact issued source/decision/dependent frontier, wound module and specific conflict/cost prerequisites; no A/B reorder or extra mutation authority. |
| Validation repair | Current trusted repair packet, named contract/example and actual permitted read/write scope; routing never expands it. |

`CLI_Rules_Index.md` is the main router. Retained matrix/example paths give precise topic destinations, stable numbers and old-heading anchors. Existing Markdown anchors are preserved explicitly or all live references map to an explicit retained alias. Negative checks distinguish a file existing from its exact anchor existing. Ordinary no-special-task navigation must not require reading the bodies of all ten modules; route tests assert both inclusion and exclusion sets.

## C4. Session delivery is required behavior

`BookOfEternityClient.csproj` packages the flat files through existing recursive patterns. Daemon `Write-GmContextPack` copies an explicit `docSpecs` list via `Copy-GmContextPackFile`; it does not follow links. Every module/core/router advertised as session-local must have an explicit source/role/destination and manifest entry, with exact source→package→session bytes checked. Old routers and necessary shared prerequisites remain delivered.

Update actual launcher generator/template, daemon initial and rebased directives, context-pack README, task guide and affected repair/worker refs. Full dependency closure for an advertised session route must resolve under its allowed roots; no fallback to another worktree or source checkout. Global index references to unrelated repository-only material must be clearly scoped, not falsely advertised as available session-local files. Do not introduce a generic recursive doc crawler or new runtime selection engine.

Missing expected source/copy, silent null copy, broken anchor or escaped session link fails the relevant check. The test’s expected module set comes from approved inventory/content map, not solely the daemon list under test. Merely asserting that docSpecs contains strings does not prove actual delivery.

## C5. Example extraction and manifest

`ExampleSnippetExtractor.ExtractAll` uses TopDirectoryOnly. New example paths stay flat, preserving current discovery behavior without introducing recursive crawling. `InferExpectedFromLines` uses 12 preceding lines; XML classification uses the enclosing example’s type and nearby text. Preserve context or explicitly inventory equivalent context wrappers and verify unchanged classification.

Named-fence consumers locate `##`/`### contractId` and stop at the next same/higher heading. Do not insert a new heading between dependent fences, lower a boundary so it absorbs the next example, or lose a companion input. Existing runtime/exemption entries retain IDs, runner, validationKind, requiredText, input/expected behaviors and truthful coverageLimit. Update only File/companion locators. The canonical manifest is not split in this phase.

Guard migration must inspect actual module bodies. A test concatenating only routers or a filename filter that now yields zero snippets is not preserved coverage. Reconcile expected/actual occurrence inventories and fail on underfill. Existing explicit exemptions stay exemptions; relocation adds no new runtime or native-process claim.

## C6. Scope and failure handling

Do not modify game formulas, schemas, canonical roots, accepted evidence or issue lifecycle. If moved content reveals a pre-existing contradiction, record source quotes and impact, preserve its before-image and obtain a scoped coordinator disposition before applying that semantic edit. D01’s proposed resolution is in research.md; it is not already authorized by structure approval.

A failures-first check preserves its raw output before correction. First/changed source SHA and selected planned/completed counts remain separate from the later evidence carrier. Underfill, timeout, missing delivery or skipped platform cases cannot be labelled full acceptance. Linux tests do not qualify Windows `powershell.exe` paths. Afterlife acceptance precedes Block_5; no broad gameplay/provider/full-suite test compensates for a missing preservation or delivery oracle.

## C7. Finite negative obligations

Mutation fixtures must independently detect: one lost source block; one duplicate owner/copy; unlisted prose edit; reversed fence order; missing INVALID context; duplicate example number/contract occurrence; wrong Block_5 ancestor assignment; dropped companion mapping; undiscovered nested example; missing daemon copied module; broken anchor/path traversal outside delivery; every-turn-all-modules directive; omission of a second pending contract; ordinary-turn instructions injected into receipt-only/bootstrap scope. Use independent temp copies/in-memory fixtures, not shared mutable production docs. Each negative must fail its intended preservation/routing/delivery check, not a unrelated setup error.
