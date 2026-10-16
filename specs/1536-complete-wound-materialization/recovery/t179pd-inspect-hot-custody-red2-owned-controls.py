from pathlib import Path
import json,gzip,hashlib,base64,xml.etree.ElementTree as E,subprocess,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';P='t179pd-hot-custody-red2-owned-controls-red-20261016';C,A=sys.argv[1:];S='d474cdd8546f8157b6ffd71840f268062ee05ed0'
h=lambda b:hashlib.sha256(b).hexdigest()
def b(v):return None if v is None else base64.b64decode(v,validate=True)
def j(v):return json.loads(b(v).decode('utf-8-sig'))
idx=json.loads((D/(P+'-artifact-index.json')).read_text());assert idx['source_sha']==A and idx['native_exit_code']==1
chains=[]
for a in idx['artifacts']:
 stored=(D/a['path']).read_bytes();raw=gzip.decompress(stored) if a['path'].endswith('.gz') else stored
 assert h(stored)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes'] and raw==(R/a['raw_path']).read_bytes()
 if C!='WORKTREE':assert stored==subprocess.check_output(['git','show',C+':'+str((D/a['path']).relative_to(R))])
 chains.append(a)
s=idx['summary'];assert s['Tests']=={'Total':3,'Executed':3,'Passed':2,'Failed':1} and s['ExitCode']==1 and not s['TimedOut'] and not s['DuplicateTests'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
assert s['Revision']['Head']==A and s['Revision']['ChangedFiles']==0 and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=3,CompletedCases=3,UnderfilledDescriptors=[],Complete=True)
p=json.loads((D/(P+'-plan.json')).read_text());assert len(p['Descriptors'])==1 and p['Descriptors'][0]['EstimatedCases']==3 and p['Revision']==s['Revision']
x=E.fromstring(gzip.decompress(next(D.glob(P+'-*.trx.gz')).read_bytes()));ns={'t':x.tag.split('}')[0][1:]};tests=x.findall('.//t:UnitTestResult',ns);assert len(tests)==len({t.get('testId') for t in tests})==len({t.get('executionId') for t in tests})==3
expected={'HotSubmission_LegacyUnchangedReadyHasGenuineOrdinaryStage':(False,False,'Passed'),'HotSubmission_LegacyAcceptedReadyDoesNotProveUnchangedSource':(True,False,'Passed'),'HotSubmission_ReadyReplacedAfterReadCannotIssueConsumedCustody':(True,True,'Failed')}
source_paths={'game_state/player/status_changes.json','game_state/player/experience.json','game_state/core/player_status.json','game_state/meta/soul_state.json','game_state/misc/characteristics.json','game_state/player/stat_points.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','input/turn_request.json','ready/turn_complete.json','output/narrative_response.json','output/interface_updates.json','output/debug_logs.json','game_state/resources/resource_state.json','game_state/resources/resource_history.json','game_state/control/progression_schedule.json','game_state/wounds/wound_history.json'}
cases=[]
for t in tests:
 name=t.get('testName').rsplit('.',1)[-1];replace,require,outcome=expected[name];assert t.get('outcome')==outcome
 stdout=t.findtext('t:Output/t:StdOut',namespaces=ns);rows=[]
 for line in stdout.splitlines():
  try:v=json.loads(line)
  except ValueError:continue
  if isinstance(v,dict):rows.append(v)
 found=[v['Observation'] for v in rows if v.get('Boundary')=='fr063_hot_ready_real_legacy_control_before_new_diagnostic'];assert len(found)==1;o=found[0]
 assert o['replaceReady']==replace and o['requireNewDiagnostic']==require and o['accepted'] and o['replacements']==int(replace)
 assert o['CurrentTurn']==42 and o['Original']['TurnNumber']==43 and o['ReadyAbsentAfterOwnerReturned'] and o['NoOriginalOrPrivateFieldAuthored'] and not o['SemanticAcceptanceClaimed']
 initial=j(o['ObservedReady']);assert initial['requestId']==o['Original']['RequestId'] and initial['sessionId']==o['Original']['SessionId'] and initial['turnNumber']==43
 ready='game_state/control/validation_repair_ready.json';manifest='game_state/control/pending_turn_snapshot.json';assert o['OwnerReadOrder'][0]==ready and o['OwnerReadOrder'].index(manifest)>0
 assert b(o['ReadyBeforeActualDelete'])==b(o['ReplacementReady'] if replace else o['ObservedReady'])
 if replace:assert b(o['ObservedReady'])!=b(o['ReplacementReady']) and j(o['ReplacementReady'])['requestId']=='replacement_after_actual_ready_read'
 else:assert o['ReplacementReady'] is None
 images=o['SourceImages'];assert set(images)==source_paths
 m=j(images[manifest]);request=j(images['input/turn_request.json']);assert m['manifestPayloadHash']==o['Original']['SnapshotToken'] and m['requestId']==request['requestId']==o['Original']['RequestId'] and m['turnNumber']==request['turnNumber']==43
 authority=j(images['game_state/control/pending_turn_snapshot.authority.json']);payload=b(authority['payloadJsonBase64']);assert h(payload).upper()==authority['payloadSha256'];a=json.loads(payload);assert a['manifestPayloadHash']==m['manifestPayloadHash'] and a['requestId']==m['requestId']
 clean=[v for v in rows if 'CleanupOwnedRoot'in v];assert len(clean)==1 and clean[0]['OwnedFixtureRemoved'] and clean[0]['CleanupFailure'] is None and not Path(clean[0]['CleanupOwnedRoot']).exists()
 error=t.find('t:Output/t:ErrorInfo',ns)
 if require:
  message=error.findtext('t:Message',namespaces=ns);stack=error.findtext('t:StackTrace',namespaces=ns);assert message.strip()=='Assert.NotNull() Failure: Value is null' and 'GameEngineTurnLifecycleTests.HotSubmissionReadyDrift.cs:line 228' in stack
 else:assert error is None
 cases.append(dict(name=name,outcome=outcome,replaceReady=replace,missingDiagnosticTarget=require,binding=o['Original'],generation=o['Generation'],readOrder=o['OwnerReadOrder'],observedReadySha256=h(b(o['ObservedReady'])),beforeDeleteSha256=h(b(o['ReadyBeforeActualDelete'])),sourceImageCount=len(images),sourceImageHashes={k:None if v is None else h(b(v)) for k,v in images.items()},cleanup=clean[0],stdoutSha256=h(stdout.encode())))
assert len({a['cleanup']['CleanupOwnedRoot'] for a in cases})==3 and {a['name'] for a in cases}==set(expected)
out=dict(compiled_source=S,actual_head=A,carrier=C,chains=chains,summary=s,plan=p,trxCounters=x.find('.//t:Counters',ns).attrib,cases=cases,qualification='Two legacy controls PASS and one sole missing-export RED after real source/drift checks. No custody implementation or semantic admission. The post-wait 17-image equality and correlated-only-ledger assertions are execution/source evidence; this diagnostic retains before-images, not a second raw afterimage ledger.')
q=Path('/tmp/t179pd-hot-custody-red2-owned-controls-root-inspection-20261016.json');assert not q.exists();q.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(dict(cases=len(cases),chains=len(chains),outcomes=[a['outcome'] for a in cases],strictDistinctRemovedRoots=3,qualifiedCausalCutLine=228)))
