# T287 — resolved ordinary-read admission seam

Baseline e8e4601; source consultation gpt-6-astra/high read-only, no runtime/edits. This is the design gate before product code.

## Rejected higher-level shortcuts

Preparation's explicit graph contains7 snapshot readers: checkpoint original; accepted-state export; two reads in rejected QTE adapter; two in corrected QTE adapter; final continuation. References: WoundAcceptedTurnPlanner.MortalJointRepair.cs154–219; MortalJointRepairCheckpoint.cs108/162/186/263; MortalWoundTreatmentAcceptedStateAuthority.cs434; MortalWoundOpportunityAdapter.cs49/110/210–214; continuation117. This is a conditional source count, not T279 runtime measurement; later common-plan64+ is separate.

Whole reconstruction cannot be reused: ExportCurrent invalidates procedure/critical/resource claims on invalid/mismatched candidate (AcceptedTurnAuthorityRegistry.cs1963–1984), and resets/restores claims2011–2019. Final continuation checks before-images and ConfirmedHeld ownership. Adapter's second physical authentication and baseline duplicate reads detect intervening changes; replacing them with memoized bytes is not justified. None is removed here.

## Selected focused reduction

Ordinary reads currently perform overlapping target-path namespace walks through ResolvePath/EnsureCanonicalPathStillSafe and TrustedLocalFileScope.ValidateFile. Public ResolvePath lexical normalization/containment is at FileSystemManager.cs684–727; physical reparse walk755–780. TrustedLocalFileScope.ValidateFile133–139 freshly validates the grant, all ancestors from filesystem root305–316, non-following leaf176–183/322–364; it subsumes that narrower walk and rejects wrong kinds as well. GameSessionPath derives readonly_basePath.

Extract the exact lexical part into private ResolveCanonicalPathLexically(string relativePath). Public ResolvePath retains its existing physical reparse walk and external contract. Only OrdinaryReads target-byte readers switch to lexical resolution and a separate lexical identity check; global EnsureCanonicalPathStillSafe and UsesTrustedLocalWriter routing stay unchanged. Keep every existing scope.ValidateFile at pre-open/post-hook/completion and all lease/current-owner/generation/registry/recovery/cancellation/data catch/disposal checks.

Preserve initial target physical admission OUTSIDE the retry loop with scope.ValidateFile(expectedPath), so converting initial ResolvePath does not move target metadata failures into retries. This additional full ordinary-kind admission may fail earlier on stable invalid kinds; it still fails closed with InvalidDataException, without accepting null/data absence. Existing nullable data-only catch boundaries stay unchanged. A concurrent hostile machine owner is explicitly outside TrustedLocalFileScope's guarantee; consolidation removes adjacent overlapping observations, not all possible interleavings. No hook or await is crossed by physical-result reuse, and no physical result/byte/authority cache is added.

Files: Core/FileSystemManager.cs (lexical helper, XML and passive DEBUG reparse-completion observation); Core/FileSystemManager.OrdinaryReads.cs (two byte-reader entrypaths and shared completion only); new unit CanonicalOrdinaryReadAdmissionTests.cs; tests/categories.json/selection.json. Do not optimize OrdinaryFileExistsCore or diagnostic status here. Shared completion also serves OrdinaryReadStreams.cs113 and TrustedLocalStorage.cs400, so their focused consumers must be checked.

## Causal first tests and evidence

Add only passive DEBUG FileSystemManagerHooks.AfterCanonicalPathReparseValidation(Action<string>), invoked after an existing completed public ResolvePath physical reparse pass. Null disables it; observer failures are ignored and it must not perform I/O/mutate state. It counts completed walks, not OS/disk operations and never grants authority. Write tests first, add the observational scaffold without the optimization, then require actual excess-walk RED on real reads (not compilation failure). A target read should need at most its existing routing walk; fresh TrustedLocalFileScope admissions remain independent and mandatory.

New category crash5-ordinary-read-admission,2min, unit only: four actual reader families (leased async bytes, leased sync text/bytes, World nullable sync), exact bytes plus target reparse-walk count≤1; post-open wrong-kind mutation must throw rather than become absence; async pre-open wrong-parent refusal; second call rechecks changed namespace; stale/foreign lease refusal; lexical invalid paths. New helpers receive multiline XML. Existing portable-ordinary-readers/consumers/windows, relevant world nullable-reader and pending-snapshot validation controls cover shared consumers; choose exact memberships from catalog before run, not all categories.

After RED→GREEN, fresh XML build and category evidence, a new spec14 variant of the genuine spec13 owned30s/120s component compares signed1/17rollback maps. Require unchanged5/21content callbacks/outputs/tamper rejection while recording the separate reparse-walk counts and per-call timing. Spec13 guards/raw remain immutable; no unchanged runtime retry. Only accepted source plus actual improvement permits source42 regeneration/binding and one full cut0. If timing/work does not improve or negatives fail, no expensive rerun follows.
