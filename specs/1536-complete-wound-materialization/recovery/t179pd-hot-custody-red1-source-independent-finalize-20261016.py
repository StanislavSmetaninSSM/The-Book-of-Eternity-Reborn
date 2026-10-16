import json,hashlib,subprocess,os,gzip,xml.etree.ElementTree as ET
from pathlib import Path
root=Path('/workspace/The-Book-of-Eternity-Reborn');tmp=Path('/tmp');head='e0ed69e2b96f41437e8465ae41182b111dfc1150';source='e182b6745817bd34ff5247c34f8d469bb71754f1';base='d149d37577a3367ae6ca249640353a194934300e';env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*a):return subprocess.check_output(['git',*a],cwd=root,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def ref(p):
 b=Path(p).read_bytes();return dict(path=str(p),bytes=len(b),sha256=sha(b))
def pin(p):
 b=git('show',head+':'+p);return dict(path=p,commit=head,bytes=len(b),sha256=sha(b),git_blob=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest())
inspectionpath=tmp/'t179pd-hot-custody-red1-source-independent-inspection-20261016.json';j=json.loads(inspectionpath.read_text())
trx='specs/1536-complete-wound-materialization/recovery/t179pd-observed-green-default-fixture-first-late-20261016-fr063-joint-late-completion-integration-001.trx.gz'
stored=git('show',head+':'+trx);assert stored==(root/trx).read_bytes();raw=gzip.decompress(stored);xml=ET.fromstring(raw);observations=[]
for result in xml.iter():
 if result.tag.endswith('UnitTestResult'):
  for element in result.iter():
   if element.tag.endswith('StdOut'):
    for line in (element.text or '').splitlines():
     if 'fr063_joint_actual_prior42_inactive_tail' in line:
      row=json.loads(line[line.find('{'):])['Observation'];assert row['ActualLifecycleImages']['game_state/player/status_changes.json'] is None
      observations.append(dict(test_name=result.attrib['testName'],test_id=result.attrib['testId'],boundary='fr063_joint_actual_prior42_inactive_tail',session_id=row['SessionId'],request_id=row['RequestId'],prior_turn=row['PriorTurn'],before_joint_original_turn=row['BeforeJointOriginalTurn'],status_changes_original_before_stage=None))
assert len(observations)==2
extras=['BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs','BookOfEternityClient/Services/MortalItemCanonicalProjectionPlanner.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInactiveTail.cs','BookOfEternityClient/Services/PendingTurnSnapshotReader.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QteStoryAppend.cs','BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.PendingWave.cs',trx]
findingp=tmp/'t179pd-hot-custody-red1-hs01-refusal-finding-20261016.json';assert not findingp.exists();findingp.write_text(json.dumps(dict(id='HS01-F01',source=source,candidate=head,confirmed=True,
 scenario='Signed original status_changes absent; fixture authors it after original; first actual mechanical mismatch is absent-to-present.',
 expected_by_test='Concurrent joint mechanical input changed: game_state/player/status_changes.json',
 actual_source_branch='Uncovered joint mechanical input is present: game_state/player/status_changes.json',
 test_expected_message_line=109,test_call_path='HotSubmissionCustody.cs:125–131 -> JointCompletionActiveTail.cs:109,117',
 production_path='GameEngine.MortalJointRepairMechanicalAdmission.cs:122–126',historical_support=dict(trx_pin=pin(trx),raw_sha256=sha(raw),raw_bytes=len(raw),observations=observations,qualification='Prior unchanged genuine baseline support only; no new HS01 runtime executed.'),
 source_reasoning=['TreatmentOrdinaryGuardPaths is composed from effect/source/item paths and fixed roots; none of the three new authored inputs appear there.','MortalJointInactiveTailPaths starts with status_changes.json; first authored match is therefore status_changes.','The genuine prior42 lifecycle leaves status_changes absent; Stage43 signs absence and its retained original is not rewritten by this fixture.','AuthorHotJointSubmissionAfterOriginalAsync accepts absent optional original and creates statsIncreased under the live lease.','ReadCurrent CoveredLogicalPaths contains present selected bytes, not absent paths.','MC checks uncovered-present before semantic changed equality, so the exact issue-message assertion prevents the missing export assertion.'],
 minimal_correction='Keep genuine absence. Explicitly assert absent original and newly present submission and expect the exact uncovered-present error for this known branch; alternatively derive one exact expected message from independently retained original coverage, without accepting either message or changing production.',
 extra_pins=[pin(p) for p in extras]),indent=2)+'\n')
md=tmp/'t179pd-hot-custody-red1-source-independent-review-20261016.md';js=md.with_suffix('.json');assert not md.exists() and not js.exists()
body=f'''# Independent SOURCE/FIXTURE/SELECTION review — Pd.3b HS01 initial RED

**Verdict: BLOCKED by HS01-F01. No actual category is released, including protected Late2.** Fresh preparation is valid and zero-execution, but the new fixture expects the wrong existing refusal branch. Correct this before the costly first run, then preserve the source correction and obtain fresh preparation/affected SOURCE review.

Candidate `{head}`; compiled/test source `{source}`; engineering baseline `{base}`. Retained requested/configured reviewer `gpt-6-astra`, `xhigh`, separate from root/author; serving-backend telemetry unavailable. Root remains sole repository writer.

## Confirmed material fixture finding: HS01-F01

`HotSubmissionCustody.cs:125–131` derives the first changed path as `game_state/player/status_changes.json`. `JointCompletionActiveTail.cs:109,117` then requires the exact message `Concurrent joint mechanical input changed: game_state/player/status_changes.json`.

The actual genuine baseline has **no original status_changes file**. The optional-original branch in the new fixture expressly allows this absence and then creates the public stats file after Stage43. `TreatmentOrdinaryGuardPaths` contains none of these three new authored inputs; the appended inactive-tail list begins with status_changes. In `GameEngine.MortalJointRepairMechanicalAdmission.cs:122–126`, an uncovered original path with a present current file throws **`Uncovered joint mechanical input is present: game_state/player/status_changes.json`**, before the later current-vs-original JSON equality branch. Therefore the existing-refusal assertion fails before the intended missing-export `Assert.NotNull(export)` at new-test line158.

This is a fixture expectation defect, not a production guard defect. Static source and both retained genuine prior Late2 TRX baseline observations independently establish the absent path. Those historical observations support the unchanged fixture baseline only; no HS01 execution or RED is claimed.

Minimum correction: preserve the genuine absent original. Explicitly assert that absent→present setup and expect the exact uncovered-present refusal, or derive a single exact expected branch from independently retained original coverage. Do not seed/rewrite an original, accept either error message permissively, change the production guard, or skip the old refusal assertions. Keep the missing-method assertion after the complete real-owner preconditions. See the separately pinned finding evidence JSON for exact historical case IDs/source anchors.

## Remaining bounded source assessment

The five runtime-source/catalog paths are three integration test files plus `tests/categories.json` and `tests/selection.json` (283 insertions/49 deletions). Production, unit-test and runner source diffs are empty. Compiled e182→carrier e0ed has no product/test/catalog/selection/script drift. Both protected Late2 Fact declarations/bodies are byte-identical to d149; the new helper switch is trailing/default false. Existing refusal helper defaults retain late-caller handling and the old trajectory source string. No broad passing-family rerun is justified by this test-only initial block.

The fixture otherwise follows the accepted test-first boundary: actual prior42 and real ProcessPlayerTurn/Stage43; the existing TerminalWaitStarted callback authors public inputs only after signed original; genuine original/current binding and original level/stat-watermark are checked; only stats/experience/player-status are changed, with soul/characteristics/stat-points unchanged; public output collection includes the three new inputs; the existing native repair observer resubmits them, corrects severity III→II and writes correlated Ready last. No new production hook, private issuer/field seed, original rewrite, fake Frame or accepted flag is introduced. The existing `statsIncreased` string-array shape is accepted by its source validator.

The pre-missing-export checks require actual ProcessPlayerTurn return, no observer/owner exception, one correlated repair, exact typed held/refusal outcome, confirmed unchanged request, full current-file preservation with the existing explicitly qualified repair/trajectory differences, unchanged turn42, no common plan/marker, unchanged care/history, exact input/request/manifest/authority/G, and consumed Ready absence. The live `handled=false` value is only a local diagnostic; actual completion is independently required by `liveCallerReturned`, so it is not used as sole owner evidence. Strict `CleanupOwnedFixture` disposal runs on assertion failure and emits/removal-checks the real root; cleanup failure must remain unexpected and cannot qualify RED.

The future diagnostic assertions specify actual Stage/current binding, consumed Ready, source recheck, closed capture, no delegation/S/B/M3, exact original/current/output bytes and detached exports. Those remain future assertions: neither a missing method nor reflected diagnostic JSON independently proves issuance. Later implementation must satisfy the accepted private-owner contract and all other HS cases. No additional confirmed source/fixture issue was found in this bounded inspection.

## Preparation and selection evidence

Independently checked **12/12 raw-local/stored/committed artifact chains**, native argv, source revision/cleanliness, full build tails, catalog/plan summaries and all **four current DLL hashes**. Fresh BOTH/catalog succeeded with native0,132.0831829s,11719 methods/773 categories, no unmapped/stale selectors; integration104.9483518s and unit16.4619954s completed with zero compiler errors. PlanOnly succeeded with native0,4.7608232s. Both summaries record zero executed tests, empty category results and successful owned-tree/runtime cleanup. Build warnings are retained; no warning-free claim.

Exact discovery is two integration descriptors/three distinct methods and cases: protected `fr063-joint-late-completion`2 and new `fr063-hot-joint-attempt-ownership`1. Both categories are exclusive with5-minute budgets. The initial selection is sufficient for the intended single HS01 prerequisite once its expectation is corrected; HS02–16 remain future. PlanOnly sorts HS01 first in its descriptor output; that is discovery order only. Never execute aggregate SelectionFile to obtain the required order. The future actual order remains singleton Late2 first, freeze/push/readback/recovery, separate EVIDENCE, then only an explicitly released HS01 causal RED.

The absent export is confirmed in current production source. The only intended later RED is `Assert.NotNull(export)` line158 **after every old oracle passes and strict cleanup completes**. A wrong refusal, setup/bootstrap/repair failure, unexpected PASS, timeout, cleanup failure or incomplete evidence stops/freezes and does not license success logic.

New selected recovery independently verifies candidate e0ed: **1028/1028** file bytes equal writer and pinned Git blob identities; clone HEAD matches, worktree is clean, no alternates. The older1010-file source-e182 receipt in the preparation carrier remains historical. GitHub-only transport/remote/main readback are coordinator receipt evidence; reviewer performed no network or restored runtime. All40 primary pins,7 finding-source pins, four exact diffs, ten projections,12 artifact chains, binary hashes, recovery file pins and exact argv are retained in the paired/support JSON.

## Gate and qualification limits

No actual is released at this source gate. After the narrow fixture fix: new clean source checkpoint, fresh BOTH/catalog/PlanOnly/DLLs and affected SOURCE review; only that accepted gate may release first protected Late2. Separate frozen EVIDENCE remains mandatory before HS01. No production success logic before preserved causal RED. Do not replay the prior accepted131 reader cases without a relevant cause.

This is initial test-only HS01 review, not acceptance of custody implementation, all HS cases, full submitted semantic S/B, active admission/samecandidate/M3/P-A/coldP0/F1/T197/public QTE activation or D4/guidedUS5/visual/Windows/D8. No GM schema or production contract changes here; narrow no-GM-update rationale remains valid. Plan/tasks accurately retain new checks as UNRUN and later gates open. Existing author/reviewer reports remain unchanged.

Reviewer ran source/Git/hash/XML analysis only; no builds, discovery, tests, application/native execution, network, repository/OS mutation or delegation.

FINALSTOP.
'''
md.write_text(body)
support=[ref(inspectionpath),ref(findingp),ref(j['projections']['path']),ref(tmp/'t179pd-hot-custody-red1-source-independent-inspect-20261016.py'),ref(Path(__file__))]+[ref(d['path']) for d in j['diffs']]
report=dict(task='Pd.3b HS01 initial test-only SOURCE/FIXTURE/SELECTION gate',verdict='BLOCKED_HS01_F01_WRONG_EXISTING_REFUSAL_BRANCH',candidate=head,compiled_source=source,engineering_base=base,
 configuration={'requested_and_retained_configured_model':'gpt-6-astra','reasoning_effort':'xhigh','backend_telemetry':'unavailable','reviewer':'/root/t179pc_independent_review'},
 findings=[dict(id='HS01-F01',severity='material fixture blocker',expected=j['expected_red_line'],evidence=ref(findingp),summary='Original status_changes is absent; expected Concurrent-changed message conflicts with earlier actual Uncovered-present branch, preventing causal missing-method RED.')],
 source_pins=j['pins'],diffs=j['diffs'],protected_methods=j['protected_methods'],projections=j['projections'],source_paths=j['source_paths'],
 preparation={'valid':True,'runtime_executions':0,'chains':j['chains'],'natives':j['natives'],'summaries':j['summaries'],'binaries':j['binaries']},
 selection={'design_sufficient_after_fixture_fix':True,'categories':['fr063-joint-late-completion','fr063-hot-joint-attempt-ownership'],'descriptors':2,'methods':3,'cases':3,'max_category_minutes':5,'plan':j['plan'],'alphabetic_plan_is_not_actual_order':True},
 recovery={k:v for k,v in j['recovery'].items() if k!='files'},supporting_artifacts=support,report_markdown=ref(md),
 release={'actual_categories':[],'protected_late2_released':False,'hs01_red_released':False,'production_success_logic_released':False,'next':'Correct exact fixture branch; fresh source checkpoint/BOTH/catalog/PlanOnly and affected SOURCE review. Then only first Late2, separate EVIDENCE before HS01.'},
 expected_causal_red={'method':'GuaranteedTreatment_HotCustodyBindsActualPostOriginalSubmissionAndConsumedReady','line':158,'assertion':'Assert.NotNull(export)','absent_method':'ExportMortalJointPublicationAttemptDiagnostic','status':'NOT_RUN; currently blocked by earlier precondition mismatch'},
 broader_stage_accepted=False,full_F1_S_M3_activation=False,prior_reports_modified=False,reviewer_runtime_build_discovery_network_delegation=0,repo_writes=0,finalstop=True)
js.write_text(json.dumps(report,indent=2)+'\n')
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
print(json.dumps(dict(verdict=report['verdict'],markdown=ref(md),json=ref(js),finding_evidence=ref(findingp)),indent=2))
