import os
os.environ.update(GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
from pathlib import Path
import json,gzip,hashlib,base64,xml.etree.ElementTree as E,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';P='t179pd-hot-custody-red1-absence-hs01-red-20261016';A='9c52f988e8daad0d6061e540b39db1e9bb9fb9b4';S='47364c47f5788427fa6360d99c4babdce8f2d717'
def h(b):return hashlib.sha256(b).hexdigest()
def b(v):return None if v is None else base64.b64decode(v,validate=True)
def j(v):return json.loads(b(v).decode('utf-8-sig'))
idx=json.loads((D/(P+'-artifact-index.json')).read_bytes());assert idx['source_sha']==A and idx['native_exit_code']==1;chains=[]
for a in idx['artifacts']:
 stored=(D/a['path']).read_bytes();raw=gzip.decompress(stored) if a['path'].endswith('.gz') else stored;assert h(stored)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'] and raw==Path(a['raw_path']).read_bytes();chains.append(a)
assert len(chains)==6
s=idx['summary'];assert s==json.loads((D/(P+'-summary.json')).read_bytes());assert s['Revision']['Head']==A and s['Revision']['ChangedFiles']==0 and s['ExitCode']==1
assert s['Tests']=={'Total':1,'Executed':1,'Passed':0,'Failed':1};assert not s['TimedOut'] and not s['DuplicateTests'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
assert s['Selection']=={'PlannedDescriptors':1,'CompletedDescriptors':1,'PlannedCases':1,'CompletedCases':1,'UnderfilledDescriptors':[],'Complete':True};assert s['CategoryResults'][0]['BudgetMinutes']==5 and s['CategoryResults'][0]['Seconds']<300
plan=json.loads((D/(P+'-plan.json')).read_bytes());assert len(plan['Descriptors'])==1 and plan['Descriptors'][0]['EstimatedCases']==1 and plan['Revision']==s['Revision']
x=E.fromstring(gzip.decompress(next(D.glob(P+'-*.trx.gz')).read_bytes()));ns={'t':x.tag.split('}')[0][1:]};cases=x.findall('.//t:UnitTestResult',ns);assert len(cases)==1;c=cases[0];assert c.get('outcome')=='Failed' and c.get('testName')==plan['Descriptors'][0]['ExpectedMethodCounts'][0]['Name']
error=c.findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns);stack=c.findtext('t:Output/t:ErrorInfo/t:StackTrace',namespaces=ns);assert error=='Assert.NotNull() Failure: Value is null' and 'HotSubmissionCustody.cs:line 172' in stack
source=subprocess.check_output(['git','show',S+':BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.HotSubmissionCustody.cs'],cwd=R);assert source.decode().splitlines()[171].strip()=='Assert.NotNull(export);'
rows=[];stdout=c.findtext('t:Output/t:StdOut',namespaces=ns)
for line in stdout.splitlines():
 try:v=json.loads(line)
 except ValueError:continue
 if isinstance(v,dict):rows.append(v)
def one(k):
 found=[v['Observation'] for v in rows if v.get('Boundary')==k];assert len(found)==1,(k,len(found));return found[0]
p=one('fr063_joint_completion_actual_prerequisites');f=one('fr063_hot_custody_genuine_post_original_submission');o=one('fr063_joint_completion_actual_owner_outcome');a=one('fr063_joint_active_tail_actual_refusal');q=one('fr063_hot_custody_existing_owner_oracles_before_missing_result')
assert p['Source']['TurnNumber']==42 and p['Original']['TurnNumber']==43 and p['Source']['SessionId']==p['Original']['SessionId'] and p['Source']['RequestId']!=p['Original']['RequestId'] and p['Source']['SnapshotToken']!=p['Original']['SnapshotToken']
assert f['ActualCaller']==o['ActualCaller']==a['ActualCaller']=='ProcessPlayerTurn' and f['TurnNumber']==43 and f['Generation']==p['generation']==q['Generation']
for lower,upper in [('sessionId','SessionId'),('requestId','RequestId'),('snapshotToken','SnapshotToken')]:assert p['FullRequest']['coordinates'][lower]==p['Original'][upper]==f[upper]
assert p['FullRequest']['coordinates']['sessionGeneration']==f['Generation'] and p['FullRequest']['coordinates']['turn']==43
assert p['ConfirmedHeldBeforeEntry'] and p['NoPrecomposedCommonPlan'] and p['EnergyBefore']==2 and p['HealthBefore']==5
assert o['entered'] and o['liveCallerReturned'] and o['liveWaitEntered'] and o['jointOriginalAuthored'] and o['Failure'] is None and o['ObserverFailure'] is None and o['ActualTurn']==42
assert a['scenario']=='post_original_stats_level' and a['entered'] and not a['handled'] and a['Failure'] is None and a['ObserverFailure'] is None and a['disposition']=='PublicationUncertain' and a['ActualTurn']==42 and not a['hasCommon'] and a['repairReplies']==1 and a['ConfirmedValid'] and a['ConfirmedState']==1 and not a['ConfirmedIssues']
assert len(a['Issues'])==1 and a['Issues'][0]['Code']=='mortal_joint_repair_admission_refused' and a['Issues'][0]['Message']=='Uncovered joint mechanical input is present: game_state/player/status_changes.json'
assert q=={'ActualOriginalTurn':43,'ActualCurrentTurn':42,'Generation':f['Generation'],'HeldRequestFingerprint':p['FullRequest']['requestFingerprint'],'ExistingRefusalPath':'game_state/player/status_changes.json','RepairReplies':1,'NewCommonPublication':False,'ChangedInputsRetained':True}
assert not any(v.get('Boundary')=='fr063_hot_custody_actual_attempt_diagnostic' for v in rows)
assert f['UncoveredOriginalPaths']==['game_state/player/status_changes.json'] and not f['OriginalAbsenceAuthorityClaimed'] and not f['StatAcknowledgmentAuthored'] and not f['PrivateOriginOrAdmissionAuthored'];assert len(f['OriginalInputs'])==len(f['CurrentInputs'])==6
orig=f['OriginalInputs'];cur=f['CurrentInputs'];assert orig['game_state/player/status_changes.json'] is None and j(cur['game_state/player/status_changes.json'])['statsIncreased']==['strength']
xp=j(cur['game_state/player/experience.json']);assert xp['level']==xp['playerLevel']==2 and xp['experience']==xp['currentExperience']==49 and xp['totalExperience']==149 and xp['experienceForNextLevel']==150
assert j(orig['game_state/player/experience.json'])['level']==1 and j(cur['game_state/core/player_status.json'])['level']==2
for path in ['game_state/meta/soul_state.json','game_state/misc/characteristics.json','game_state/player/stat_points.json']:assert orig[path]==cur[path]
assert j(orig['game_state/player/stat_points.json'])['levelUpStatPointsAwardedThroughLevel']==1
for path,value in cur.items():assert a['After'][path]==value
assert len(o['Milestones'])==len(a['Milestones'])==1 and o['Milestones']==a['Milestones'];m=o['Milestones'][0];assert m['Path']=='game_state/control/validation_repair_request.json';images=m['Images'];manifest=j(images['game_state/control/pending_turn_snapshot.json']);auth=j(images['game_state/control/pending_turn_snapshot.authority.json']);payload=b(auth['payloadJsonBase64']);assert h(payload).upper()==auth['payloadSha256'];ap=json.loads(payload)
assert manifest['requestId']==ap['requestId']==f['RequestId'] and manifest['manifestPayloadHash']==ap['manifestPayloadHash']==f['SnapshotToken'] and manifest['turnNumber']==ap['turnNumber']==43
for field in ['files','snapshotFileHashes','clientOwnedValidationHashes','rollbackBackups','rollbackBaselineFiles','sourceLabel']:assert ap[field]==manifest[field]
for path,snapshot in manifest['files'].items():assert h(b(images[snapshot])).upper()==manifest['snapshotFileHashes'][path]
for target,path in ap['rollbackBackups'].items():assert h(b(images[path])).upper()==ap['rollbackBackupHashes'][target]
for path,raw in orig.items():
 if raw is not None:assert images[manifest['files'][path]]==raw
 else:assert path not in manifest['files'] and path not in manifest.get('originalPathPresenceV1',{})
repair=json.loads(m['Raw'].lstrip('\ufeff'));assert repair['requestId']==f['RequestId'] and repair['sessionId']==f['SessionId'] and repair['turnNumber']==43 and repair['fullTurnResubmissionRequired'];assert any(v['code']=='wound_severity_above_opportunity' for v in repair['errors']);packet=[v for v in repair['harnessRepairPackets'] if 'candidateKind'in v];assert len(packet)==1 and packet[0]['snapshotToken']==f['SnapshotToken']
pending=j(a['After']['game_state/control/pending_wound_resolutions.json']);submitted=pending['submittedTreatmentRequests'];assert len(submitted)==1 and submitted[0]['request']==p['FullRequest'] and submitted[0]['requestFingerprint']==p['FullRequest']['requestFingerprint'];assert j(a['After']['game_state/wounds/wound_commands.json'])==p['Command']
assert j(a['After']['output/mortal_wound_decisions.json'])['proposal']['severity']=='II';assert 'game_state/wounds/mortal_qte_decision_publication.json'not in a['After'] and 'game_state/control/validation_repair_ready.json'not in a['After']
repair_owned={'game_state/control/pending_wound_resolutions.json','output/mortal_wound_decisions.json','game_state/control/validation_repair_request.json','game_state/control/validation_repair_ready.json','game_state/control/validation_diagnostic_failure_report.json'};before=dict(a['Before']);before['game_state/control/progression_schedule.json']=a['SignedOriginalSchedule'];before['game_state/control/qte_runtime.json']=a['SignedOriginalQteRuntime'];ledger='game_state/control/gm_trajectory_ledger.jsonl';after={k:v for k,v in a['After'].items() if k!=ledger};assert ledger not in before;assert {k:v for k,v in before.items() if k not in repair_owned}=={k:v for k,v in after.items() if k not in repair_owned}
l=b(a['After'][ledger]).decode('utf-8-sig').splitlines();assert len(l)==1;lr=json.loads(l[0]);assert lr['requestId']==f['RequestId'] and lr['validation']['source']=='обработки хода' and lr['validation']['acceptanceScope']=='correlated_repair_ready' and lr['validation']['fullCanonicalStateAccepted'] is False
resources=j(a['After']['game_state/resources/resource_state.json'])['entries'];rd={v['resourceKey']:v['current'] for v in resources};assert rd['health']==5 and rd['energy']==2
wh=j(a['After']['game_state/wounds/wound_history.json'])['transitions'];assert len(wh)==1 and not any(v['turn']==43 for v in wh)
cleanup=[v for v in rows if 'CleanupOwnedRoot'in v];assert len(cleanup)==1 and cleanup[0]['OwnedFixtureRemoved'] and cleanup[0]['CleanupFailure'] is None and not Path(cleanup[0]['CleanupOwnedRoot']).exists()
ne=json.loads((D/(P+'-native-exit.json')).read_bytes());assert ne['native_exit']==1 and ne['actual_head']==A and ne['compiled_source']==S and ne['binaries_unchanged'] and ne['post_head_unchanged'] and ne['post_git_clean'] and all(not v for v in ne['new_scoped_temp_residue'].values())
for v in json.loads((D/'t179pd-hot-custody-red1-absence-prep-binaries-20261016.json').read_bytes())['binaries']:assert h((R/v['path']).read_bytes())==v['sha256']
result={'verdict':'QUALIFYING_CAUSAL_RED_MISSING_PRIVATE_OWNER_EXPORT_ONLY','actualHead':A,'compiledSource':S,'nativeExit':1,'summary':s,'case':dict(c.attrib),'error':error,'stack':stack,'rawArtifactChainsVerified':chains,'original':p['Original'],'source':p['Source'],'generation':f['Generation'],'snapshotHashesVerified':len(manifest['files']),'rollbackHashesVerified':len(ap['rollbackBackups']),'fullTreatmentRequestBindingVerified':True,'postOriginalAutomaticImagesVerified':6,'canonicalPreservationImagesVerified':len(before)-len([k for k in before if k in repair_owned]),'exactPriorRepairLedgerOnly':True,'energy':2,'health':5,'woundHistoryTransitions':1,'currentTurn':42,'newCommonPublication':False,'readyAbsentAfterRealOwnerChecks':True,'strictCleanup':cleanup[0],'four473BinariesVerified':True,'scope':'Causal absence proof only; new custody/registry receipt not implemented or qualified; no S/B/M3/fullF1'}
q=Path('/tmp/t179pd-hot-custody-hs01-causal-red-independent-semantics-20261016.json');assert not q.exists();q.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'verdict':result['verdict'],'chains':6,'originalSnapshots':len(manifest['files']),'rollback':len(ap['rollbackBackups']),'preservedImages':result['canonicalPreservationImagesVerified'],'strictCleanup':True}))
