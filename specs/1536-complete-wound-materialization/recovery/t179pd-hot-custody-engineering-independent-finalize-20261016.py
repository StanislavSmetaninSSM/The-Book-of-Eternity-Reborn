import json,hashlib,subprocess,os
from pathlib import Path
root=Path('/workspace/The-Book-of-Eternity-Reborn');tmp=Path('/tmp')
head='132ffd72c67daeccd1c6639472ecca4b585e92ab';source='3fdc16e8a954458f1e56bafa42ca24a66c06d020';base='d99e45102e2fb64088d771eb473de6671617f9b6'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*args):return subprocess.check_output(['git',*args],cwd=root,env=env)
def sha(b):return hashlib.sha256(b).hexdigest()
def ref(p):
 b=Path(p).read_bytes();return {'path':str(p),'bytes':len(b),'sha256':sha(b)}
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
assert not git('diff','--check',base,head)
inspection_path=tmp/'t179pd-hot-custody-engineering-independent-inspection-20261016.json'
inspection=json.loads(inspection_path.read_text())
ranges=[
('BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs',1477,1555),
('BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs',1556,1742),
('BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs',399,592),
('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3340,3569),
('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',3625,3733),
('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',8336,8395),
('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs',18,123),
('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs',1,158),
('BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepair.cs',217,295),
('BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs',58,164),
('BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs',1600,1642),
('BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs',5767,5776),
('BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalJointRepair.cs',28,153),
('BookOfEternityClient/Services/MortalJointRepairPublicationTransaction.cs',15,145),
('BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs',49,220),
('BookOfEternityClient/Core/SessionOperationContext.cs',119,157),
('BookOfEternityClient/Core/SessionOperationContext.cs',214,323),
('BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs',103,122),
('BookOfEternityClient/Services/PendingTurnSnapshotReader.Observed.cs',10,89),
('BookOfEternityClient/Core/FileSystemManager.cs',1520,1546),
('BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs',4129,4184),
('BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepair.cs',134,208)]
rows=[]
for p,a,b in ranges:
 raw=git('show',source+':'+p);text=''.join(raw.decode().splitlines(keepends=True)[a-1:b]);rows.append(dict(commit=source,path=p,start=a,end=b,text=text,sha256=sha(text.encode())))
for p in ['specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md']:
 text=''.join(git('show',head+':'+p).decode().splitlines(keepends=True)[:14]);rows.append(dict(commit=head,path=p,start=1,end=14,text=text,sha256=sha(text.encode())))
proj=tmp/'t179pd-hot-custody-engineering-independent-source-projections-20261016.json';assert not proj.exists();proj.write_text(json.dumps(rows,indent=2)+'\n')
md=tmp/'t179pd-hot-custody-engineering-independent-review-20261016.md';js=md.with_suffix('.json');assert not md.exists() and not js.exists()
text='''# Independent engineering review — T179-Pd.3b HOT custody

**Verdict: ACCEPTED for bounded test-first engineering, with one nonblocking documentation finding. No confirmed material defect remains in the detailed contract.** This accepts the complete proposed custody/attempt/observed-reader integration design. It does not accept an implementation, release any test execution, or qualify full submitted-source admission.

Reviewed candidate `132ffd72c67daeccd1c6639472ecca4b585e92ab`, documentation base `d99e45102e2fb64088d771eb473de6671617f9b6`, frozen production/test source `3fdc16e8a954458f1e56bafa42ca24a66c06d020`. Author: `/root/t179_preseal_design_check`; independent reviewer: `/root/t179pc_independent_review`. Retained coordinator spawn receipt requests and reports `gpt-6-astra`, `xhigh`, `fork_turns=none`; actual serving-backend telemetry is unavailable. No author/reviewer role overlap or new delegation.

## Finding

**HC-D01 — low severity, current-status wording.** `plan.md:11` and `tasks.md:11` say genuine continuation/reservation “can precede this capture.” The detailed design lines165–172 instead requires actual custody capture and irreversible Capturing consumption **before** `CreateJointRepairPreparationAsync`, followed by the existing reservation at its current position. Those status bullets should state that order. The detailed contract and canonical subsection are explicit and consistent, so this does not block bounded design acceptance. The coordinator acknowledged the correction for the review-preservation checkpoint; the reviewed candidate remains unchanged. This report does not assert that the correction is already committed.

## Engineering assessment

1. **Actual origin, not imported authority.** TL1484–1742 and SS399–592 provide genuine Stage invocation, real original producer, final request validation, and return. The proposed private issuer retains actual engine/files/G, produced manifest/authority and completed Stage identity. It cannot be reconstructed from a source label, parsed original, helper context, cold engine or caller-supplied map. Sequential original writes are not claimed atomic. Original/Stage failure and cleanup must invalidate the hot owner while preserving existing physical recovery.

2. **Browser lifetime is feasible with the stated data-only lineage.** TL1549 uses `stagedPreparation with { ... }` under a later actual browser owner. The original Stage reference must remain origin provenance; the later record copy is not a resurrected Stage issuer. `BrowserOriginalOperationScope.Dispose` ends its scope, while `SessionOperationContext` can also fail during finalization/main close. The contract requires current real browser/MainAdmission at use and invalidation on the actual failure path. HS04/HS12 must demonstrate these facts; preserving the old scope or assuming the copy is reference-identical would violate the accepted design. This is an implementation verification obligation, not an additional architecture blocker.

3. **Ready ownership and release are closed.** VR3460–3567 reads/parses Ready, resolves current context, accepts matching tuples, appends trajectory, then deletes. VR8336–8395 and `ReleaseOwnedLeaseAsync:103–122` establish the real read/deletion leases and release outcome. Lr uses one actual snapshot plus the existing BOM-aware decoder; Lc closes before trajectory; Ld rechecks and issues Consumed only after deletion and successful release; Lp obtains fresh live evidence and checks the recorded consumption plus current absence. No lease crosses the wait/context/trajectory boundaries and no Lc evidence is revived. Optional faults cannot become successful absence, and replacement/publication uncertainty retain their existing owner. Worker direct success, worker Ready and both rejected-Ready re-dispatch branches require actual dispatch tags, never tuple-based inference. Existing trajectory failure handling and v2 diagnostic order remain authoritative.

4. **Capture closes before seals; mechanical integration has a real consumer.** CP28–41 has a genuine current intake/rejected capture before continuation construction. WP134–208 reconstructs actual treatment/acquisition and current guards without the later automatic equality/INACTIVE checks, so no new raw reservation family or acceptance shortcut is needed. The design places capture before this factory, consumes it even when custody is unavailable, and retains actual returned continuation/reservation/cache references once. MC's exact old selection, two current-read loops, concurrent-command/equality checks and late continuation validation remain in place. Fresh `ReadObservedCurrent` evidence is checked before mechanical proof creation and retained for same-files/lease/Snapshot rechecks. The six-path custody observation is separate from mechanical selection. Full S/B cannot attach to an already sealed v2 candidate.

5. **No-native-attempt is a distinct, conservative registry result.** AR217–295 uses the existing exclusive slot. The old void abort cannot prove closure. The proposed named operation verifies live exact attempt/continuation/reservation/accepted state, lease/G/MainAdmission, no pending decision, no published fingerprint/restart blocker and no open treatment/joint take receipt before clearing that same slot. It must not fabricate proof when source validation already invalidated ownership. CP marks Delegated before NR96; NR96–108 can take or return uncertainty without a successful native batch. AR publication receipts and the real transaction therefore retain all post-delegation debt/compensation/rearm authority. Clearing an unattempted slot never reopens capture, releases a different owner, undoes earlier repair writes or rebuilds a closed attempt. Overlap, disposal and same-attempt reentry are covered by the proposed retained owner and HS09–13.

6. **The scope boundary is honest.** Custody bytes, occurrence order, presence/decode outcomes and actual Ready consumption prove provenance, not automatic/output validity or full repair freshness. The 73-output-leaf inventory is explicitly unavailable for full S; verifying its source lines is not semantic qualification. No callback/field-seeded issuer, synthetic Frame/GameResponse, source replay, second reduction, new domain ID, clock sample, P/A/M3 emission or new public eligibility is authorized. Existing v2 hash grammar and successful output/mechanics remain unchanged design obligations.

## TDD and finite selection

HS01–16 form a sufficient bounded design matrix: genuine post-original changed submission through the actual repair/Ready owner; unchanged live/late positives; cold/helper/browser/worker distinctions; rejected/repeated Ready; source/physical/foreign-owner faults; capture-after-seal refusal; predelegation versus delegated native uncertainty; failure/overlap/cancellation; detached export mutation; and no new side effects. HS01 is a custody-positive case with unchanged v2 mechanical refusal, not active gameplay acceptance. Its missing-private-result RED is causal only after actual existing held/source/refusal preconditions succeed. Drift and owner/debt REDs must use genuine established owners and existing physical faults, never temporary unsafe issuance or fabricated private objects.

The proposed three category responsibilities and splitting expensive ownership cases at the existing ≤5-minute bound are appropriate. Exact methods/counts, cleanup and measured budgets remain UNRUN and must be reviewed with actual fixtures and discovery. Registry changes require the specifically affected checkpoint/phase-refusal/settlement consumers; old 131 reader cases are not automatically replayed when their implementation is unchanged. Protected Late2 method bodies remain unchanged. Pure state/copy tests cannot substitute for actual origin/lease/reservation proofs.

This design acceptance permits the next authorized test-first source block. It grants **no actual run**: fresh BOTH/catalog/PlanOnly and source/fixture/selection review must precede the first unchanged Late2; frozen separate EVIDENCE must precede separately released causal RED/GREEN singletons. Unexpected fixtures/failures stop and freeze. Complete bounded implementation review remains mandatory.

## Independent evidence verification

Read AGENTS, workflow1.6, testing policy, constitution, task/spec/current canonical subsection and the complete author pair. Author MD SHA-256 `90f0ddc0c1fa3516231e62fcf9a8433e2f21b10b6a05219d6e24e105e5fb329f`; JSON `5429971dec24f96a022c3820c478a23884bf85a1982fde89408216d46e470d08`.

Independently verified 105 Git/blob/SHA pins, 65 exact source projections, 73 historical leaf rows and all116 listed IO lines. The one historical canonical-plan pin remains historical; current canonical plan is separately pinned. Production/tests/catalog/scripts source3fd→candidate132 and base d99→candidate132 diffs are empty. The six changed paths are design/report/status documents. `git diff --check d99… 132…` passed; worktree was clean at initial and final checks. Four exact diff files/argv/hashes and 24 reviewer source projections are retained in /tmp; the JSON report references every supporting artifact.

The new selected recovery directory is exactly candidate132: independently compared **990/990** selected file bytes against writer files and candidate Git blob identities; HEAD matches, worktree is clean and no alternates file exists. Receipt SHA and every file pin are in the independent inspection. GitHub-only transport/remote readback/main `d0241e71e349fbe2020e4a41b6be7c627c81cbb4` are recorded by the coordinator receipt; this reviewer performed no network operation. No restored build/runtime is claimed. Recovery remains selected, not a full checkout qualification.

Only read-only source/Git/hash analysis ran. No build, discovery, tests, application/native execution, fetch/push, repository/OS mutation or delegation. Existing test evidence and all prior reports are unchanged; no runtime PASS is minted by this design review.

## Limits and documentation

The internal client-owned, unactivated, no-emitted-schema/no-new-GM-contract design supports the narrow no-GM-update rationale. A reachable request/Ready/original/control/public diagnostic/GM-authored behavior change during implementation requires synchronized documentation/examples/guards; later active S/B or M3 cannot inherit this exemption.

Full raw/player/automatic/level/time/output semantics, genuine preparation/repair history and freshness/volatile closure, active S→C→B/samecandidate, cold R01–R08/P0/M3, worker hot proof, full Pd/T179/T192/F1/T197, public/new-QTE activation, legacy/platform/Windows/visual/D4/guided-US5/D8 and full task checkboxes remain open. The author’s 73-leaf future inventory and adjacent T197 designs receive no additional qualification here.

FINALSTOP.
'''
md.write_text(text)
support=[ref(inspection_path),ref(proj),ref(tmp/'t179pd-hot-custody-engineering-independent-inspect-20261016.py'),ref(Path(__file__))]+[ref(d['path']) for d in inspection['diffs']]
report={
 'task':'T179-Pd.3b independent bounded HOT custody engineering DESIGN review',
 'verdict':'ACCEPTED_BOUNDED_ENGINEERING_DESIGN_WITH_NONBLOCKING_DOCUMENTATION_FINDING',
 'candidate':head,'documentation_base':base,'frozen_production_source':source,'observed_head':head,
 'configuration':{'requested_model':'gpt-6-astra','requested_reasoning_effort':'xhigh','actual_configuration_evidence':'Retained coordinator spawn receipt supplied model=gpt-6-astra, reasoning_effort=xhigh, fork_turns=none; canonical reviewer task returned.','backend_telemetry':'unavailable','author':'/root/t179_preseal_design_check','reviewer':'/root/t179pc_independent_review'},
 'material_findings':[],
 'nonblocking_findings':[{'id':'HC-D01','severity':'low','paths':['specs/1536-complete-wound-materialization/plan.md:11','specs/1536-complete-wound-materialization/tasks.md:11'],'issue':'Earlier current-status bullets say continuation/reservation can precede capture, contrary to detailed design lines165–172.','required_correction':'State capture -> consume Capturing -> CreateJointRepairPreparation -> existing reservation.','disposition':'Coordinator acknowledged correction in review-preservation checkpoint; not yet changed at reviewed candidate. Detailed engineering contract accepted.'}],
 'verified_counts':inspection['counts'],'source_pins':inspection['source_pins'],'current_governance_and_extra_pins':inspection['extra_pins'],'diffs':inspection['diffs'],
 'supporting_artifacts':support,'report_markdown':ref(md),
 'recovery':{k:v for k,v in inspection['recovery'].items() if k!='file_pins'},
 'source_projection_artifact':ref(proj),
 'design_assessment':{
 'actual_stage_original_issuer':True,'ready_actual_read_capture_trajectory_delete_release_fresh_publication_chain':True,
 'no_cross_lease_live_authority':True,'capture_before_continuation_and_all_seals':True,'separate_custody_and_mechanical_observations':True,
 'actual_observed_reader_consumer':True,'same_existing_exclusive_reservation_family':True,'registry_owned_current_call_unattempted_close':True,
 'delegation_precedes_normalizer_take_call':True,'postdelegation_transaction_debt_preserved':True,'optional_hot_unavailability_preserves_v2':True,
 'browser_origin_reference_distinct_from_later_record_copy':True,'full_semantic_admission_available':False,'semantic_output_leaf_qualification':False},
 'coverage':{'future_ids':[f'HS{i:02}' for i in range(1,17)],'status':'ALL_UNRUN','design_matrix_sufficient_for_bounded_unit':True,'exact_selectors_counts_budgets':'Deferred to actual fixture SOURCE/discovery review, not accepted runtime evidence.',
 'proposed_categories':['fr063-hot-joint-submission-custody','fr063-hot-joint-attempt-ownership','fr063-hot-joint-origin-compatibility'],'max_category_budget_minutes':5,
 'protected_late_bodies_unchanged_required':True,'additional_affected_existing_consumers':'Late/live/active-tail; exact checkpoint/phase-refusal/settlement consumers where actual registry/checkpoint diff requires them.',
 'old_reader_131_not_automatically_rerun':True},
 'release':{'authorized_next_step':'Bounded test-first source/fixture implementation under existing staged user authorization.','actual_tests_released':[],'runtime_release':False,'success_logic_before_causal_red':False,'required_gates':['fresh BOTH/catalog/PlanOnly','SOURCE/FIXTURE/SELECTION','first unchanged Late2 only after SOURCE release','frozen separate EVIDENCE','individually released causal RED/GREEN packets','final bounded implementation review']},
 'limitations':['No implementation/runtime evidence acceptance','No full source S/automatic/output admission','No postseal S/B attachment','No actual engine preparation/cold history proof','No active samecandidate/M3/P0/F1/T197/public QTE activation','No worker-direct hot proof','No legacy branch/Windows/visual/D4/guidedUS5/D8/full task closure','Remote transport readback is coordinator evidence, locally verified selected file identities only'],
 'no_gm_update_rationale':'Internal client-owned unactivated custody/diagnostics with unchanged v2 emitted bytes; any reachable schema/GM/public behavior change requires same-change synchronization.',
 'checks_run':[{'argv':['python3','/tmp/t179pd-hot-custody-engineering-independent-inspect-20261016.py'],'outcome':'PASS; static files/Git/hash analysis only'},{'argv':['git','diff','--check',base,head],'outcome':'PASS; no output'},{'argv':['git','status','--porcelain'],'outcome':'empty'}],
 'runtime_executed':0,'builds_executed':0,'discovery_executed':0,'repo_writes':0,'network_operations':0,'delegation':0,
 'prior_reports_modified':False,'finalstop':True}
js.write_text(json.dumps(report,indent=2)+'\n')
assert git('rev-parse','HEAD').decode().strip()==head and not git('status','--porcelain')
print(json.dumps({'markdown':ref(md),'json':ref(js),'supporting_artifacts':support},indent=2))
