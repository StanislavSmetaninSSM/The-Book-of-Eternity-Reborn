from pathlib import Path
import json,hashlib,gzip,base64,subprocess,xml.etree.ElementTree as E,datetime,collections
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/';carrier='f647853f41051025969d83a5fa251d3b592a23c0';actual='61e712e319e8f480b77755aebae4c2db69e1423c';compiled='6ab43f6add261d90628fc07d6aacf42dfbae0561'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['close2'];p=Path(info['recordPath']);r=read(p);assert h(p.read_bytes())==info['recordSha256']=='44bd4147341b6590ecd5867a0c1719989dd4b02f7480641b1b134f6037a59219'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==1 and r['mode']=='close2' and r['expectedCases']==2 and r['statusAfter']=='' and '-NoBuild' in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest'] and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='82477104f4dc7b6e2c68b7b56300223b6d44075f811b57f0ffbded33d1e04319' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[1]
paths=set()
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath'])
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=2,Executed=2,Passed=1,Failed=1) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==1 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256'];assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
t=E.parse(next(rd.rglob('*.trx'))).getroot();ns={'t':t.tag.split('}')[0][1:]};u=t.findall('.//t:UnitTestResult',ns);assert len(u)==2;assert {x.attrib['testName'] for x in u}=={v['Name'] for v in plan['Descriptors'][0]['ExpectedMethodCounts']}
failed=next(x for x in u if x.attrib['outcome']=='Failed');passed=next(x for x in u if x.attrib['outcome']=='Passed');assert failed.attrib['testName'].endswith('GuaranteedTreatment_InterfaceOwningCloseRetainsPublishedButCannotDeliverFreshResult') and passed.attrib['testName'].endswith('GuaranteedTreatment_LaterOuterFinalizationDoesNotUnemitCommittedInterfaceOrReplay')
message=failed.find('t:Output/t:ErrorInfo/t:Message',ns).text;stack=failed.find('t:Output/t:ErrorInfo/t:StackTrace',ns).text;assert 'Assert.Contains() Failure' in message and 'JointInterfaceCloseRetention.cs:line 105' in stack
stdout=failed.find('t:Output/t:StdOut',ns).text;rows=[json.loads(l) for l in stdout.splitlines() if l.startswith('{')];by=collections.defaultdict(list)
for row in rows:
 if 'Boundary' in row:by[row['Boundary']].append(row['Observation'])
def one(n):assert len(by[n])==1;return by[n][0]
def raw(o):
 b=base64.b64decode(o['Bytes'],validate=True);assert len(b)==o['Length'] and h(b).upper()==o['Sha256'];return b
attached=one('iobs_actual_initial_install_close_attached');assert attached['_initialInstalled'] and attached['_initialPaused'] and 'PrepareAndPublishWithOwningReleaseAsync' in attached['ActualOwner']
state=one('iobs_actual_interface_owning_close_retained');assert state['Calls']==1 and state['Published']['state']=='Published' and state['Published']['issues']==[] and not state['DataDelivered'] and not state['OwningReleaseConfirmed'] and state['OuterFinalizationIsDistinct']
assert raw(state['BeforeClose'])==raw(state['ActualPending']) and raw(state['Interface'])==base64.b64decode(state['Published']['afterImage']['base64'])
outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['Failure'] is None and outcome['ObserverFailure'] is None and outcome['ActualTurn']==42 and outcome['liveCallerReturned'] and outcome['EngineDiagnostics']==state['Diagnostics'];assert state['Diagnostics']=='Engine diagnostics:\nCritical accepted-turn raw state corruption after обработки хода: 1 errors'
assert 'L01 actual I publication owning release' not in stdout and 'source-bound joint repair owner is blocked' not in stdout and len(state['Diagnostics'])<2000
assert not by['iobs_actual_direct_and_outer_restore_retention'];assert len(by['iobs_actual_result_commit'])==2 and [v['Record']['state'] for v in by['iobs_actual_result_commit']]==['Prepared','Published'];assert len(by['iobs_actual_committed_member'])==1 and by['iobs_actual_committed_member'][0]['InterfaceChanged'] and not by['iobs_actual_committed_member'][0]['MarkerCommitted'] and not by['iobs_actual_completed_capture']
cleans=[]
for result in u:
 j=[json.loads(l) for l in result.find('t:Output/t:StdOut',ns).text.splitlines() if l.startswith('{')];cl=next(v for v in j if 'CleanupOwnedRoot' in v);assert cl['OwnedFixtureRemoved'] and cl['CleanupFailure'] is None and not Path(cl['CleanupOwnedRoot']).exists();cleans.append(cl)
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-f647853f.json');assert receipt['sha']==carrier and receipt['base']==actual and receipt['changedPathsVerified']==16
for f in receipt['files']:
 b=frozen(f['path']);assert b==git(C,'show',carrier+':'+f['path']) and len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob']
# Source anchors prove why original logging sites are not reached; no runtime simulation.
sourcepaths=['BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfacePublication.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfaceResume.cs','BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs','BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCloseRetention.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceConsumerRefusals.cs']
sources=[{'path':q,'sha256':h(frozen(q))} for q in sourcepaths]
out={'kind':'INDEPENDENT_CLOSE2_FROZEN_FAILURE_CAUSAL_EVIDENCE_CHECK','checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'carrier':carrier,'actualSource':actual,'compiledSource':compiled,'nativeExit':1,'recordSha256':h(p.read_bytes()),'archiveSha256':h(af.read_bytes()),'archiveOriginalRawChains':11,'completeRawCoverage':True,'source98Unchanged':True,'binaries9AndAll1575Unchanged':True,'summaryTests':s['Tests'],'selection':s['Selection'],'timedOut':False,'seconds':r['wallSeconds'],'failedMethod':failed.attrib['testName'],'failedDuration':failed.attrib['duration'],'firstFailure':{'message':message,'stack':stack},'observedPassingSibling':{'method':passed.attrib['testName'],'duration':passed.attrib['duration'],'independentlyQualifiedHere':False},'actualOwnerLease':attached['ActualOwner'],'publishedRetainedBeforeDiagnosticFailure':{'pendingBytesExact':True,'interfaceMatchesPublishedAfterImage':True,'releaseCalls':1,'noFreshDataDelivered':True,'noCommonOrCapture':True,'turn42':True,'diagnostics':state['Diagnostics']},'downstreamDirectRestoreOracleReached':False,'cleanup':cleans,'restoredCarrierPaths':16,'sourceAnchors':sources,'attribution':'PRODUCT_DIAGNOSTIC_LOSS_WHEN_DESTRUCTIVE_REFUSAL_GUARD_REPLACES_ORIGINAL_CLOSE_EXCEPTION','runtimeReplayed':False}
op=D/'t179pd-ni-iobs-l01-cloud-close2-failure-independent-check-20261018.json';op.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(op,h(op.read_bytes()))
