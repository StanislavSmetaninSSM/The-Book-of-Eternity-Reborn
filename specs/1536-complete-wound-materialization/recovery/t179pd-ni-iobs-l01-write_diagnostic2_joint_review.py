#!/usr/bin/env python3
"""Persist independent bounded conclusion from already verified immutable inputs."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

B = Path('/tmp/1536-cloud-tools/review')
R = Path('/workspace/The-Book-of-Eternity-Reborn')
def ref(p):
    p = Path(p); data = p.read_bytes()
    return {'path': str(p), 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}
def load(name): return json.loads((B / name).read_text())
on = 't179pd-ni-iobs-l01-diagnostic2-outcome-independent-check-d65035c3-20261018.json'
an = 't179pd-ni-iobs-l01-diagnostic2-accounting-scope-corrected-independent-check-d65035c3-20261018.json'
mn = 't179pd-ni-iobs-l01-diagnostic2-joint-metrics-independent-check-20261018.json'
o, a, m = load(on), load(an), load(mn)
assert all(not d['findings'] for d in (o, a, m))
assert o['nativeExit'] == 1 and o['summary']['Tests']['Failed'] == 1
assert o['descriptor']['BudgetMinutes'] == 15 and not o['summary']['TimedOut']
assert len(o['streamedDiagnostics'][0]['events']) == 20
assert len(a['identities']) == 2 and m['coverageRemains'] == 52
assert not m['sampledSharedCgroup']['targetGovernanceEstablished']
source = {
    'fixture': 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs',
    'diagnostic': 'BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrashDiagnostic.cs',
    'publication': 'BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs',
    'storyOwner': 'BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs'}
history = 't179pd-ni-iobs-l01-diagnostic2-original-reader-history-20261018.json'
result = {
 'kind': 'INDEPENDENT_DIAGNOSTIC2_ORIGINAL_CAUSAL_AND_JOINT_REVIEW',
 'reviewedUtc': datetime.now(timezone.utc).isoformat(),
 'reviewer': 'Independent SOURCE/PREP/EVIDENCE reviewer; requested Astra XHigh role',
 'verdict': 'JOINT_BOUNDED_BLOCKER',
 'evidenceDisposition': 'Original diagnostic failure and accounting evidence accepted as authentic observations; no target qualification.',
 'carrier': o['carrier'], 'actualSource': o['actualSource'], 'compiledSource': o['compiledSource'],
 'recordSha256': o['recordSha256'],
 'binding': {'sourcePins': 101, 'productPaths': 23, 'pinDigest': o['pinDigest'],
             'outputs': o['outputInventory'], 'originalStrongAssertionsAndBudgetsUnchanged': True},
 'checkedEvidence': {'outcomeCheck': ref(B/on), 'accountingCheck': ref(B/an),
                     'finiteMetricsCheck': ref(B/mn), 'nativeArchive': o['archive'],
                     'accountingArchive': o['externalAccounting']['archive'], 'recovery': o['recovery'],
                     'checksPassedByOutcomeReader': o['checksPassed'],
                     'scope': '11 native and 3 accounting gzip/raw/original chains, exact saved GitHub/clean-clone carrier, 101 source pins/product23, nine copies/full1575 inventory, exact one method/case and tool closure checked. No native or live process/fixture observation performed by reviewer.'},
 'originalOutcome': {
   'nativeExit': 1, 'controllerWallSeconds': o['wallSeconds'],
   'nativeWallTime': o['summary']['WallTime'], 'trxDuration': o['trxResults'][0]['duration'],
   'tests': o['summary']['Tests'], 'selection': o['summary']['Selection'],
   'case': o['descriptor']['ExpectedMethodCounts'][0],
   'primaryError': 'System.TimeoutException: The operation has timed out.',
   'origin': source['fixture'] + ':129 (WaitForExitAsync().WaitAsync(220 seconds)), propagated through lines153 and65.',
   'categoryTimedOut': False, 'categoryBudgetSeconds': 900, 'childBudgetSeconds': 220,
   'harnessEnvelopeMinutes': o['summary']['TimeoutMinutes'],
   'classification': 'The resumer exceeded its child wait. This is not a category timeout or a failed positive completion assertion.',
   'producer': {'pid': 68330, 'kernelBirthTick': 2967858, 'exit': 73, 'eof': True, 'joined': True},
   'resumer': {'pid': 68412, 'kernelBirthTick': 2979925, 'exit': 137, 'eof': True, 'joined': True,
               'exitMeaning': 'Existing owning fixture timeout-finally terminated its own child tree and joined/EOF; exit137 is not independent evidence of OOM or spontaneous child failure.'},
   'wrapperOriginalExitPreserved': True, 'wrapperObservationErrors': [],
   'cleanup': {'ownedRootRemovalRows': 2, 'ownedTreeComplete': True, 'runtimeComplete': True,
               'unresolvedChildren': False, 'currentNamespaceRootsAfter': [],
               'frozenPriorFiles': 237, 'frozenSeparation': o['frozenSeparation'],
               'censuses': o['censuses'],
               'limits': 'Dated boundaries only: before gap4.593755s and after gap1.267196s. No continuous absence or gap reconstruction.'}},
 'semanticReach': {
   'exactCut': o['cut'],
   'directlyReached': [
     'Genuine producer MemberPublished/index1 cut, exit73, parent-confirmed PID+kernel birth/nonce/containment and cut journal; original turn42 and zero common callbacks.',
     'Current child factory/filesystem/engine initialization, real preflight lease acquisition, recovered original snapshot turn43, valid selected checkpoint/result binding, retained Prepared I preimage, runtime refresh to42, and before-image capture all returned.',
     'Real preflight lease disposal returned, actual late caller entered, LateTerminalAndIdleOperationBound returned.',
     'MortalJointCustodyCaptureCompletedBeforePreparation callback returned at15304ms; all20 scalars bind to68412:2979925, exact owned root/cut1/nonce with monotonic elapsed and UTC.'],
   'notReachedOrNotProved': [
     'No AcceptedOutcomeValidatedBeforeMaterialization event, completed-proof lease/full current DATA proof, late-first return, after-images, or resume-finally/result was emitted.',
     'Post-return Published exact-I/NI normalization, turn43, create/treat/once-only mechanics, common/origin child3, owned terminal/Ready cleanup, no double I and inert repeat assertions were not reached.',
     'Parent result/exit0/current identity/completion and strong resumed assertions after fixture line65 were not reached. No partial target PASS is inferred.'],
   'lastScalar': m['lastCustodyToLastSample']['lastScalar'],
   'sourceBoundary': {
     'afterLastHook': source['publication'] + ':49',
     'possibleUnobservedWork': 'Lines50-110 contain preparation, exclusive reservation, prepared/effects/final validation, anchors/mechanical admission, baseline/resources/progression/common, preflight, delegated publication and completed witness; later validator/story-owner work precedes the accepted checkpoint. The trace has no intermediate stage returns in this span.',
     'laterPositiveBoundary': source['storyOwner'] + ':85-91',
     'claimLimit': 'A returned hook is not proof that the immediately following await alone blocked or that subsequent stages completed. No unique managed operation, loop, lock owner or product defect is identified.'}},
 'discriminatingObservations': {
   'predeclaredDiscriminator': o['externalAccounting']['status']['tools']['discriminators'],
   'processes': m['identities'],
   'unclosedCustodySuffix': m['lastCustodyToLastSample'],
   'activeVersusWaiting': 'All190 eligible >=30-second resumer windows meet the predeclared active-CPU heuristic (about0.984-1.320cores); zero meet low-CPU. In203 consecutive valid intervals after custody,214.38CPU seconds accrue over203.747383endpoint-wall seconds. This excludes wholly idle process-wide waiting over those windows, but not one blocked managed thread while other threads compute/spin/GC. Sleeping/futex snapshots do not identify managed lock ownership.',
   'readActivity': 'The same bounded suffix records158333623 rchar and57587 read syscalls, with zero read_bytes/write_bytes deltas. These counters do not name files, prove useful progress, or locate the CPU hotspot.',
   'cgroup': m['sampledSharedCgroup'],
   'environmentFinding': 'Sampled shared node shows582.207288CPU seconds,21.569954throttled seconds/161of4058periods and zero memory.events deltas; memory.current6.848-8.055GB below its16GiB limit. Raw mount root/.. prevents proven child governance. Neither child resource exhaustion nor quota causality is established; throttle time cannot be subtracted from child wall time.',
   'observerPerturbation': m['scanOverhead'],
   'overheadLimit': 'Measured scan CPU31.685627s, sampler total38.541609s and maximum scan5.331520s are visible overhead. Neither the unobserved counterfactual without sampling nor an exact delay correction can be recovered.',
   'spacingScope': 'Process endpoint times use the checked reader monotonicEnd anchor. Resumer219/219 intervals are eligible, maximum2.858862s; producer113/114 eligible, maximum5.963974s. Ineligible windows and edge time are excluded from classification; endpoint totals are descriptive, not exact launch-to-exit measurements.',
   'rawScanGaps': 0},
 'comparisonLimits': {
   'priorDiagnostic1': ref(B/'t179pd-ni-iobs-l01-diagnostic1-outcome-causal-independent-review-20261018.json'),
   'observed': 'Prior isolated cut1 diagnostic passed once: controller296.522s, child final scalar214269ms, custody9857ms to AcceptedOutcome194742ms. New run fails220-second resumer wait and has longer producer duration despite unchanged pinned C#/assertions.',
   'limits': 'Prior pass refutes an unconditional deadlock across all such executions, not intermittent blocking or timing sensitivity. No paired CPU/accounting exists for that prior run. Instrumentation, workload and environment effects cannot be separated causally; none establishes cause of the older crash5 timeouts.'},
 'jointDecision': {
   'selectedAlternative': 'HONEST_BOUNDED_BLOCKER',
   'provedNarrowDefectAndFix': False, 'justifiedNewEnvironmentBudget': False,
   'reason': 'Current resume fails the unchanged220-second limit while consuming CPU after custody; no managed operation/progress attribution or finite remaining-work/completion bound exists. Neither a safe product fix nor an evidence-based larger timeout/semantic split follows.',
   'qualificationBlocker': 'Five original crash identities remain unqualified. Diagnostic1 PASS and diagnostic2 FAIL are outside the57-case union; there is no successful original-five current qualification.',
   'concreteDistinctFutureDecision': [
     'End this one-run diagnostic block and preserve the blocker checkpoint. Any further work requires a separately scoped decision; no new run is released by this report.',
     'For a narrow fix, require managed operation/call-stack or operation-count evidence tied to the custody-to-accepted span that identifies repeated validation/hashing, a nonprogress loop or an actual wait owner and bounds the affected path. Process CPU/wchan cannot supply that missing attribution.',
     'For an environment budget/semantic-selection proposal, require completed-work timing and a finite remainder bound under a proven target allocation with observer cost accounted for, plus exact five-case identity/bijection and a separate performance hold. Current unfinished220-second sample provides no upper bound. Semantic grouping cannot hide a hang or silently raise900/220.'],
   'release': 'NONE', 'additionalDiagnosticOrRetry': False, 'sourceRemedyAuthorizedByReview': False,
   'oneRunPermitConsumed': True, 'budgetsPreserved': {'childSeconds': 220, 'categorySeconds': 900}},
 'historyAndCoverage': {
   'qualified': 52, 'totalTargetCases': 57, 'diagnosticAddsTargetCases': 0,
   'oldCrash66529': 'UNKNOWN/unavailable; never reconstructed or relabeled.',
   'originalNewCrash5': 'native124, direct cut1/cut2 child timeouts, incomplete/noTRX; all five unqualified. Frozen237files remain untouched and isolated.',
   'priorFailures': 'All earlier fixture, diagnostic, preparation and semantic originals remain immutable. No retained52 replay/reclassification.',
   'broaderHolds': 'FullNI/HOT/F1/M3/T197 incomplete; A3 NO-PROCEED; Windows403 blocked/no retry; CI prohibited.'},
 'readerHistory': {
   'manifest': ref(B/history),
   'unchangedOutcomeRetry': 'Initial read-only GitHub ls-remote HTTP503 was preserved; the same reader retried successfully. No native retry or result reclassification.',
   'derivedCorrection': 'Initial accounting reader normalized mount root/.. to/. Original source/output preserved; corrected reader rejects that mapping. Arithmetic unchanged, final causal scope uses only corrected mapping output.',
   'positiveSemanticReader': 'Not executed: original timeout and absent late return make a positive completion reader inapplicable.'},
 'sourceReferences': {k: ref(R/v) for k,v in source.items()},
 'reviewLimits': ['No live process/root inspection, native/build/test/discovery, observer execution or repo edits.',
                  'No final completion proof, useful-work identity, unseen old diagnostics or managed stack is invented.',
                  'Observed native failure remains a failure even though all archive/provenance checks succeeded.']}
name = 't179pd-ni-iobs-l01-diagnostic2-outcome-causal-independent-review-20261018'
jp, mp = B/(name+'.json'), B/(name+'.md')
assert not jp.exists() and not mp.exists(), 'Preserve existing review artifacts'
jp.write_text(json.dumps(result, indent=2) + '\n')
md = f'''Joint verdict: **BOUNDED BLOCKER — 52/57 remain qualified; release NONE.**

The one permitted diagnostic2 failed the unchanged **220-second resumer wait** (`JointInterfaceCrash.cs:129`, propagated through153/65). Native1, exact one failed TRX case,400.779s controller; the900-second category did not time out. Producer68330:2967858 exited73; resumer68412:2979925 exited137 after the existing owning timeout cleanup. Both joined with EOF, no cleanup failure. All11 native+3 accounting archive chains,101pins/product23,9copies/full1575outputs and24-path GitHub recovery match saved carrier `{o['carrier']}`.

The actual cold lease/recovery/checkpoint/I-preimage/runtime42/before-images and actual late entry returned. The last of20 exact child scalars is custody-capture checkpoint return at15304ms. No accepted-outcome checkpoint, full current proof, late return, turn43/mechanics/terminal cleanup or inert-repeat assertions followed. Source after that checkpoint contains many preparation/authority/validation/common/publication operations; this trace does not isolate one await or a narrow defect.

All190 valid >=30-second resumer windows show active process CPU (0.984–1.320cores), not process-wide idle waiting. After custody,203 consecutive eligible intervals contain214.38CPU seconds over203.747s and158.33MB of read-character traffic. **Activity does not establish useful progress, managed lock ownership or remaining work.** Sleeping/futex thread snapshots cannot fill that gap.

Sampled shared cgroup counters show21.570s throttling and zero memory-event increments. Raw mount root `/..` prevents proven child governance; no target resource-exhaustion cause follows. Sampler scan CPU31.686s (total38.542s; maximum scan5.332s) is visible perturbation. The earlier isolated diagnostic PASS and this FAIL have no controlled timing comparison. Neither establishes the old crash5 cause or an evidence-based larger completion budget.

The one-run permit is consumed. Preserve220/900, all original failures, frozen237files, old66529UNKNOWN, and all five unqualified crash identities. No source remedy, retry, further diagnostic or target-count increment is admitted. A distinct future decision would need either **managed operation/progress attribution within custody→accepted** supporting a narrow fix, or **completed-work/remainder evidence under a proven allocation with observer cost accounted for** supporting a separately reviewed budget/semantic-selection proposal and performance hold. Current data supplies neither; this block ends here.

Before/after censuses are clean dated observations (gaps4.593755s/1.267196s), not continuous absence. The read-only HTTP503 retry and accounting mapping correction are preserved; the original reader normalized `/..` incorrectly, and only the corrected scope is used here. No native retry occurred.

Evidence: native archive `{o['archive']['sha256']}`, accounting archive `{o['externalAccounting']['archive']['sha256']}`, raw record `{o['recordSha256']}`. [Full review]({jp}) records exact source/trace/recovery references; [finite accounting]({B/mn}) and [reader history]({B/history}) retain all limits.
'''
mp.write_text(md)
deliver = [jp, mp, B/on, B/an, B/mn, B/history,
           B/'check_l01_diagnostic2_joint_metrics.py', Path(__file__),
           B/'check_l01_diagnostic2_outcome.py', B/'check_l01_diagnostic2_accounting.py',
           B/'check_l01_diagnostic2_accounting-v1-normalized-mount-root.py',
           B/'check_l01_diagnostic2_accounting_scope_corrected.py',
           B/'t179pd-ni-iobs-l01-diagnostic2-accounting-independent-check-d65035c3-20261018.json',
           B/'t179pd-ni-iobs-l01-diagnostic2-outcome-reader-failure-20261018T174439928989Z.json']
dp = B/'t179pd-ni-iobs-l01-diagnostic2-joint-review-deliverables-20261018.json'
assert not dp.exists()
dp.write_text(json.dumps({'verdict': result['verdict'], 'carrier': o['carrier'], 'files': [ref(p) for p in deliver]}, indent=2)+'\n')
print(json.dumps({'report':ref(jp), 'markdown':ref(mp), 'deliverables':ref(dp), 'verdict':result['verdict']},indent=2))
