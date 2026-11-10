# Independent reviewer: audited root read-only verifier, output redirected only.
# Semantic assertions are preserved; source and raw provenance independently checked separately.
import os
os.environ.update(GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
from pathlib import Path
import json,gzip,hashlib,base64,xml.etree.ElementTree as E,subprocess
assert not Path('/tmp/t179pd-hot-custody-red1-absence-late-independent-semantic-20261016.json').exists()
R=Path('/workspace/The-Book-of-Eternity-Reborn');REC=R/'specs/1536-complete-wound-materialization/recovery';P='t179pd-hot-custody-red1-absence-first-late-20261016';C=__import__('sys').argv[1];S='47364c47f5788427fa6360d99c4babdce8f2d717';A=__import__('sys').argv[2]
def h(b):return hashlib.sha256(b).hexdigest()
def b64(v):return None if v is None else base64.b64decode(v,validate=True)
def jb(v):return json.loads(b64(v).decode('utf-8-sig'))
def digest(b):return {'Exists':b is not None,'Length':len(b) if b is not None else 0,'Sha256':h(b).upper() if b is not None else None}
def rows(s):
 rr=[]
 for l in s.splitlines():
  try:v=json.loads(l)
  except ValueError:continue
  if isinstance(v,dict):rr.append(v)
 return rr
def one(rr,key):
 x=[v['Observation'] for v in rr if v.get('Boundary')==key];assert len(x)==1,(key,len(x));return x[0]
def image(im):
 b=b64(im['Bytes']);assert im['Exists']==(b is not None) and im['Sha256']==(None if b is None else h(b).upper());return b
idx=json.loads((REC/(P+'-artifact-index.json')).read_bytes());chains=[]
assert idx['source_sha']==A and idx['native_exit_code']==0
for a in idx['artifacts']:
 stored=(REC/a['path']).read_bytes();raw=gzip.decompress(stored) if a['path'].endswith('.gz') else stored
 assert h(stored)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'] and raw==(R/a['raw_path']).read_bytes()
 if C != 'WORKTREE':assert stored==subprocess.check_output(['git','show',C+':'+str((REC/a['path']).relative_to(R))],cwd=R)
 chains.append({**a,'matchesRawLocalAndCarrier':True})
s=json.loads((REC/(P+'-summary.json')).read_bytes());assert s==idx['summary'];assert s['Revision']['Head']==A and s['Revision']['ChangedFiles']==0
assert s['Tests']=={'Total':2,'Executed':2,'Passed':2,'Failed':0} and s['ExitCode']==0 and not s['TimedOut'] and not s['DuplicateTests'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
assert s['Selection']=={'PlannedDescriptors':1,'CompletedDescriptors':1,'PlannedCases':2,'CompletedCases':2,'UnderfilledDescriptors':[],'Complete':True}
plan=json.loads((REC/(P+'-plan.json')).read_bytes());assert len(plan['Descriptors'])==1 and plan['Descriptors'][0]['EstimatedCases']==2 and plan['Revision']==s['Revision']
x=E.fromstring(gzip.decompress(next(REC.glob(P+'-*.trx.gz')).read_bytes()));ns={'t':x.tag.split('}')[0][1:]};cc=x.findall('.//t:UnitTestResult',ns)
assert len(cc)==len({c.get('testId') for c in cc})==len({c.get('executionId') for c in cc})==2 and all(c.get('outcome')=='Passed' for c in cc)
assert {c.get('testName') for c in cc}=={m['Name'] for m in plan['Descriptors'][0]['ExpectedMethodCounts']}
cases=[]
for c in cc:
 stdout=c.findtext('t:Output/t:StdOut',namespaces=ns);rr=rows(stdout)
 p=one(rr,'fr063_joint_completion_actual_prerequisites');o=one(rr,'fr063_joint_completion_actual_owner_outcome');u=one(rr,'fr063_joint_complete_union');a=one(rr,'fr063_joint_progression_phase_report_arithmetic');re=one(rr,'fr063_joint_completion_inert_repeat');original=p['Original'];nz=p['nonzeroProgression']
 assert p['Source']['TurnNumber']==42 and original['TurnNumber']==43 and p['Source']['SessionId']==original['SessionId'] and p['Source']['RequestId']!=original['RequestId'] and p['Source']['SnapshotToken']!=original['SnapshotToken']
 assert p['ConfirmedHeldBeforeEntry'] and p['NoPrecomposedCommonPlan'] and p['EnergyBefore']==2 and p['HealthBefore']==5 and p['Draft']['proposal']['severity']=='III'
 source=one(rr,'qte_story_actual_engine_result');assert source['ActionObserved'] and source['Failure'] is None
 reader=one(rr,'fr063_joint_original_progression_reader_before_owner');assert reader['Success'] and not reader['Issues'] and reader['Expected']==reader['Actual']
 for v in rr:
  if v.get('Boundary')=='fr063_joint_natural_stage43_image_before_assert':
   n=v['Observation'];assert n['ExpectedPresent']==n['ActualPresent'] and n['ExpectedLength']==n['ActualLength'] and n['ExpectedSha256']==n['ActualSha256']
  if v.get('Boundary')=='fr063_joint_original_inventory_structural_absence_before_assert':
   n=v['Observation'];assert not any(n[k] for k in ['OriginalCovered','InManifestFiles','InSnapshotFileHashes','InRollbackBackups','InRollbackBaselineFiles'])
 assert o['ActualCaller']=='ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync' and o['entered'] and not o['HandledByActualLateCaller'] and o['jointOriginalAuthored'] and o['Failure'] is None and o['ObserverFailure'] is None and o['ActualTurn']==43
 assert u['FullOwnerCanonicalValidation'] and u['ActualTurn']==43 and u['OriginalTurn']==43
 assert (u['Create']['Kind'],u['Create']['Ordinal'],u['Treat']['Kind'],u['Treat']['Ordinal'])==('create',2,'treat',3) and u['Create']['WoundId']!=u['Treat']['WoundId']
 assert u['Create']['Turn']==u['Treat']['Turn']==43 and u['Treat']['SourceFingerprint']==u['RequestFingerprint']
 receipt=u['receipt'];assert receipt['Decision']=='materialize' and receipt['WoundId']==u['Create']['WoundId'] and receipt['TransitionId']==u['Create']['TransitionId'] and receipt['OperationKey']==u['Create']['OperationKey']
 for k in ['SessionId','RequestId','SnapshotToken']:assert receipt[k]==u[k]==original[k]
 assert receipt['SourceRequestId']==p['Source']['RequestId'] and receipt['SourceSnapshotToken']==p['Source']['SnapshotToken'] and receipt['SourceTurn']==42
 assert u['spend']['AppliedAmount']==2 and u['spend']['BeforeState']['Current']==2 and u['spend']['AfterState']['Current']==0 and u['spend']['SourceEvidence']['SourceKind']=='registered_system_outcome'
 assert u['damage']['AppliedAmount']==3 and u['damage']['BeforeState']['Current']==5 and u['damage']['AfterState']['Current']==2 and u['damage']['SourceEvidence']['SourceKind']=='effect_component'
 assert u['damage']['ExecutionSequence']<u['spend']['ExecutionSequence']
 milestones=o['Milestones'];assert len(milestones)==3;dispatch=milestones[0];repair=json.loads(dispatch['Raw'].lstrip('\ufeff'));di=dispatch['Images'];assert repair['sessionId']==original['SessionId'] and repair['requestId']==original['RequestId'] and repair['turnNumber']==43 and repair['fullTurnResubmissionRequired'];assert any(e['code']=='wound_severity_above_opportunity' for e in repair['errors'])
 packets=[r for r in repair['harnessRepairPackets'] if 'candidateKind'in r];assert len(packets)==1 and packets[0]['candidateKind']=='construct_wound' and packets[0]['snapshotToken']==original['SnapshotToken']
 for k in ['input/turn_request.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/resources/resource_definitions.json','game_state/resources/resource_state.json','game_state/resources/resource_history.json','game_state/player/wounds.json','game_state/wounds/wound_identity_index.json','game_state/wounds/wound_history.json','game_state/control/pending_mortal_wound_occurrences.json','game_state/wounds/wound_opportunity_receipts.json']:
  assert digest(b64(di[k]))==p['Before'][k],k
 manifest=jb(di['game_state/control/pending_turn_snapshot.json']);request=jb(di['input/turn_request.json']);assert manifest['requestId']==request['requestId']==original['RequestId'] and manifest['progressionControl']==request['progressionControl']==a['ActualOriginalControl']
 assert manifest['turnNumber']==request['turnNumber']==43
 # Every manifest snapshot raw body agrees with its recorded exact hash.
 for path,snapshot in manifest['files'].items():assert h(b64(di[snapshot])).upper()==manifest['snapshotFileHashes'][path],path
 initial=milestones[1];final=milestones[2];mi=json.loads(initial['Raw'].lstrip('\ufeff'));mf=json.loads(final['Raw'].lstrip('\ufeff'));fi=final['Images'];ii=initial['Images']
 assert mi['schemaVersion']==mf['schemaVersion']==2 and mi['generation']==mf['generation']==p['generation']
 assert mi['manifestHash']==h(b64(di['game_state/control/pending_turn_snapshot.json'])).upper() and mi['authorityHash']==h(b64(di['game_state/control/pending_turn_snapshot.authority.json'])).upper()
 for k in ['sessionId','requestId','snapshotToken','turn','inputFingerprint','originalQuartet','publishedFootprint','outputBinding','jointTreatment','consumedCommands','consumedPrivatePaths']:assert mi[k]==mf[k]
 assert mf==u['Marker'] and mf['requestId']==original['RequestId'] and mf['turn']==43
 phaseOnly=json.loads(json.dumps(mi));phaseOnly['progression']['phase']='applied';phaseOnly.pop('fingerprint');other=json.loads(json.dumps(mf));other.pop('fingerprint');assert phaseOnly==other
 assert digest(final['Raw'].encode())==re['After']['game_state/wounds/mortal_qte_decision_publication.json']
 for k,v in mi['originalQuartet'].items():assert v==p['Before'][k]['Sha256'],k
 for m,ims in [(mi,ii),(mf,fi)]:
  for k,v in m['publishedFootprint'].items():
   if k in ims:assert v==('absent' if ims[k] is None else h(b64(ims[k])).upper()),k
   else:assert v=='absent' and k not in re['After'],k
  binding=m['outputBinding'];assert h(b64(binding['narrativeBase64'])).upper()==binding['narrativeHash'];assert binding['narrativeBase64']==ims['output/narrative_response.json'];assert jb(binding['narrativeBase64'])['response']==p['Scene']
 rawhist=jb(fi['game_state/resources/resource_history.json'])['entries'];new=[e for e in rawhist if e['turn']==43];assert len(new)==2 and {e['transitionId'] for e in new}=={u['spend']['TransitionId'],u['damage']['TransitionId']}
 resources=jb(fi['game_state/resources/resource_state.json'])['entries'];assert {e['resourceKey']:e['current'] for e in resources}['energy']==0 and {e['resourceKey']:e['current'] for e in resources}['health']==2
 wh=jb(fi['game_state/wounds/wound_history.json']);print('WHKEYS',list(wh)) if not cases else None
 hist=wh.get('transitions',wh.get('entries'));assert len([e for e in hist if e['turn']==43])==2
 for k,typed in [('create',u['Create']),('treat',u['Treat'])]:
  row=[e for e in hist if e['transitionId']==typed['TransitionId']];assert len(row)==1 and row[0]['kind']==k and row[0]['ordinal']==typed['Ordinal']
 wounds=jb(fi['game_state/player/wounds.json'])['activeWounds'];assert len(wounds)==2
 assert next(w for w in wounds if w['woundId']==u['Treat']['WoundId'])['care']['state']=='stabilized'
 assert next(w for w in wounds if w['woundId']==u['Create']['WoundId'])['severity']['value']=='II'
 assert jb(fi['game_state/control/pending_mortal_wound_occurrences.json'])['occurrences']==[]
 # Progression is the same frozen mutation before and after phase acknowledgement.
 assert a['nonzeroProgression']==nz and a['OriginalTurn']==43 and a['RequestId']==original['RequestId'] and a['ReportAfterAbsent']
 ap=a['Applied'];un=a['Unapplied'];assert un['phase']=='unapplied' and ap['phase']=='applied';assert un==mi['progression'] and ap==mf['progression']
 for k in ['sourceGuards','scheduleBefore','reportBefore','scheduleAfter','reportAfter','mutationFingerprint','preparedAtUtc']:assert un[k]==ap[k]
 ctrl=a['ActualOriginalControl'];assert ctrl['worldCyclesAlreadyPendingBeforeTurn']==int(nz) and ctrl['factionCyclesAlreadyPendingBeforeTurn']==0
 expected=jb(a['SignedOriginalScheduleBefore']);expected.update(currentWorldTimeInMinutes=ctrl['currentWorldTimeInMinutes'],lastWorldSimulationTimeInMinutes=ctrl['lastWorldSimulationTimeInMinutes']+int(nz)*ctrl['worldCycleMinutes'],lastFactionSimulationTimeInMinutes=ctrl['lastFactionSimulationTimeInMinutes'],pendingWorldCycles=0,pendingFactionCycles=0,lastUpdatedUtc=ap['preparedAtUtc']);assert expected==jb(a['ExactScheduleAfter'])
 schedule='game_state/control/progression_schedule.json';report='game_state/control/progression_report.json'
 assert ii[schedule]==a['SignedOriginalScheduleBefore']==ap['scheduleBefore']['bytes'] and fi[schedule]==a['ExactScheduleAfter']==ap['scheduleAfter']['bytes']
 assert ii[report]==a['ReportBefore'] and fi[report] is None and not ap['reportAfter']['exists']
 tx=o['ReportRemovalHistory'];assert [t['Role'] for t in tx]==(['original_baseline_restore','progression_consumption'] if nz else [])
 for t in tx:
  members=t['Members'];assert len({m['Path'] for m in members})==len(members)
  for m in members:image(m['Before']);image(m['After'])
  rm=[m for m in members if m['Path'].endswith('/'+report)];assert len(rm)==1 and b64(rm[0]['Before']['Bytes'])==b64(a['ReportBefore']) and not rm[0]['After']['Exists']
 if nz:
  assert len({t['TransactionId'] for t in tx})==2 and len(tx[1]['Members'])==3
  rep=jb(a['ReportBefore'])['progressionProcessingReport'];assert rep['requestId']==original['RequestId'] and rep['turnNumber']==43 and rep['worldCyclesProcessed']==1
  suffixes={m['Path'].split('/game_session/',1)[1] for m in tx[1]['Members']};assert suffixes=={schedule,report,'game_state/wounds/mortal_qte_decision_publication.json'}
  for m in tx[1]['Members']:
   k=m['Path'].split('/game_session/',1)[1]
   if k=='game_state/wounds/mortal_qte_decision_publication.json':assert b64(m['Before']['Bytes'])==initial['Raw'].encode() and b64(m['After']['Bytes'])==final['Raw'].encode()
   else:assert m['Before']['Bytes']==ii[k] and m['After']['Bytes']==fi[k]
 else:assert a['ReportBefore'] is None
 # Reconstruct exact final story append from retained raw prefix and serialized observed row.
 story='stories/mortal_life_2.jsonl';row=u['ExactStorySuffix'];assert row['requestId']==original['RequestId'] and row['sessionGeneration']==p['generation'] and row['turn']==43 and row['timestamp']==ap['preparedAtUtc'] and row['narrative']==p['Scene'] and row['player']=='Осмотреть ушиб.'
 finalstory=b64(fi[story])+json.dumps({k:v for k,v in row.items() if v is not None},ensure_ascii=False,separators=(',',':')).encode()+b'\n';assert digest(finalstory)==o['After'][story]
 cleanupPaths=set(manifest['files'].values())|set(manifest['rollbackBackups'].values())|{'game_state/wounds/wound_commands.json','game_state/control/pending_wound_resolutions.json','output/mortal_wound_decisions.json','input/turn_request.json','ready/turn_complete.json','game_state/control/validation_repair_request.json','game_state/control/validation_repair_ready.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json',report}
 for k in cleanupPaths:
  assert k not in re['After'],k
  if k in o['After']:assert not o['After'][k]['Exists'],k
 assert not re['repeat'] and re['Turn']==43 and re['FullBytesEqual'] and re['Before']==re['After'] and re['Before']['$sessionGeneration']==p['Before']['$sessionGeneration']
 for k,v in re['After'].items():
  if k!='$sessionGeneration' and k in o['After']:assert v==o['After'][k],k
 clean=[v for v in rr if 'CleanupOwnedRoot'in v];assert len(clean)==1 and clean[0]['OwnedFixtureRemoved'] and clean[0]['CleanupFailure'] is None and not Path(clean[0]['CleanupOwnedRoot']).exists()
 cases.append({'case':dict(c.attrib),'source':p['Source'],'original':original,'generation':p['generation'],'nonzeroProgression':nz,'actualTurn':43,'handled':False,'create':{'ordinal':2,'transitionId':u['Create']['TransitionId'],'woundId':u['Create']['WoundId']},'treat':{'ordinal':3,'transitionId':u['Treat']['TransitionId'],'woundId':u['Treat']['WoundId']},'charge':{'amount':2,'before':2,'after':0},'damage':{'amount':3,'before':5,'after':2},'publishedFootprintCount':len(mf['publishedFootprint']),'rawMilestoneImageCounts':[len(m['Images']) for m in milestones],'manifestSnapshotHashesVerified':len(manifest['files']),'cleanupPathsVerified':len(cleanupPaths),'inertRepeatImages':len(re['After']),'storyReconstructedBytes':len(finalstory),'storySha256':h(finalstory),'namedTransactions':[{'role':t['Role'],'id':t['TransactionId'],'memberCount':len(t['Members'])} for t in tx],'cleanup':clean[0],'stdoutSha256':h(stdout.encode()),'progressionControl':ctrl,'phaseFingerprint':ap['mutationFingerprint']})
assert {c['nonzeroProgression'] for c in cases}=={False,True} and len({c['cleanup']['CleanupOwnedRoot'] for c in cases})==2
result={'source':S,'actualHead':A,'carrier':C,'index':idx,'verifiedChains':chains,'summary':s,'plan':plan,'trxCounters':x.find('.//t:Counters',ns).attrib,'cases':cases}
Path('/tmp/t179pd-hot-custody-red1-absence-late-independent-semantic-20261016.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
print(json.dumps({'caseCount':len(cases),'allChecksPassed':True,'output':'/tmp/t179pd-hot-custody-red1-absence-late-independent-semantic-20261016.json'}))
