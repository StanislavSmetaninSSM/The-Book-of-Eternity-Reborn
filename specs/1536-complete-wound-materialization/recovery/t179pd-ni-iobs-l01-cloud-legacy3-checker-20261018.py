from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='61e712e319e8f480b77755aebae4c2db69e1423c';actual='9acc5708e6f2e752fd43ae917b8357ed9ce98e1f';compiled='6ab43f6add261d90628fc07d6aacf42dfbae0561'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def frozen(path):return git(R,'show',carrier+':'+path)
observedHeads={'working':git(R,'rev-parse','HEAD').decode().strip(),'restore':git(C,'rev-parse','HEAD').decode().strip(),'remote':git(R,'ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012').decode().split()[0]}
for root,rev in [(R,observedHeads['working']),(C,observedHeads['restore']),(R,observedHeads['remote'])]:git(root,'merge-base','--is-ancestor',carrier,rev)
assert not git(R,'diff','--name-only',compiled,observedHeads['working'],'--','.',':!specs/1536-complete-wound-materialization')
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));a0=cloud['actuals']['legacy3'];packet=read(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json'))
assert len(packet['sourcePins'])==98 and h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
for p in packet['sourcePins']:
 b=(R/p['path']).read_bytes();assert b==(C/p['path']).read_bytes() and len(b)==p['bytes'] and h(b)==p['sha256'] and blob(b)==p['gitBlob']
 assert git(C,'ls-tree',carrier,'--',p['path']).split(b'\t')[0].decode().split()==[p['mode'],'blob',p['gitBlob']]
assert h((R/packet['ledger']['path']).read_bytes())==packet['ledger']['sha256']
afile=R/a0['archive']['path'];a=read(afile);assert h(afile.read_bytes())==a0['archive']['sha256']=='9858024f7ba343a82f0667c65bd8d33079e709ec6463b5575e02fbad4fec57c8'
assert a['rawChainCount']==len(a['files'])==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
assert a['helperText']==(R/(REC+'t179pd-ni-iobs-l01-archive-whole.py')).read_text()
paths=set();bytes_verified=0
for f in a['files']:
 assert f['rawPath'] not in paths;paths.add(f['rawPath']);s=afile.parent/f['storedPath'];b=s.read_bytes();assert b==(C/s.relative_to(R)).read_bytes() and len(b)==f['storedBytes'] and h(b)==f['storedSha256']
 raw=gzip.decompress(b);assert len(raw)==f['bytes'] and h(raw)==f['sha256'] and raw==Path(f['rawPath']).read_bytes();bytes_verified+=len(raw)
p=Path(a0['recordPath']);r=read(p);assert h(p.read_bytes())==a0['recordSha256']=='7f4607ca051df9d79c7a7b990bd26f0b323f675013838878943845524a2d9a59'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['statusAfter']=='' and r['expectedCases']==3 and r['mode']=='legacy3'
assert r['helperSource']==(R/(REC+'t179pd-ni-iobs-l01-process-first-run.py')).read_text() and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
assert r['namespaceBefore']==r['namespaceAfter']=={'ownedRoots':[]} and r['processesBefore']==r['processesAfter']==[]
assert r['sourcePinCount']==98 and r['sourcePinDigest']==packet['pinDigest'] and r['sourceBindingSha256']==h((p.parent/'source-binding.json').read_bytes())
assert (p.parent/'source-binding.json').read_bytes()==(R/(REC+'t179pd-ni-iobs-l01-source-packet-20261018.json')).read_bytes()
assert (p.parent/'ledger-context.json').read_bytes()==(R/packet['ledger']['path']).read_bytes()
assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256'];prep=read(r['prepRecord']);assert prep['actualTestsExecuted']==prep['nativeExit']==0 and prep['actualSource']==prep['compiledSource']==compiled
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256']
proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc'];ident=proc['identity'];pid=ident['pid'];tick=int(ident['stat'].rsplit(')',1)[1].split()[19]);assert tick>0 and '(pwsh)' in ident['stat'] and 'pwsh' in ident['cmdline']
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='legacy3' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert '-'+str(pid)+'-' in str(rd)
expected={str(q) for q in p.parent.iterdir() if q.is_file()}|{str(q) for q in rd.rglob('*') if q.is_file()}|{r['firstSentinel']};assert paths==expected
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Tests']==dict(Total=3,Executed=3,Passed=3,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=3,CompletedCases=3,UnderfilledDescriptors=[],Complete=True)
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0
category=s['CategoryResults'][0];assert len(s['CategoryResults'])==1 and category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
assert len(plan['Descriptors'])==1;desc=plan['Descriptors'][0];assert desc['BudgetMinutes']==15 and desc['EstimatedCases']==3 and desc['Adapter']=='integration' and desc['Exclusive'] and not desc['FrontendBuild']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in read(R/'tests/selection.json')['selections'] if x['category']==desc['Category']])
expected_methods={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};prior_desc=next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])
assert expected_methods=={x['Name']:x['Cases'] for x in prior_desc['ExpectedMethodCounts']} and len(expected_methods)==3 and set(expected_methods.values())=={1}
assert set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected_methods}
log=(rd/'dotnet-test.log').read_text();phase_exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',log,re.M);assert phase_exits and all(e=='0' for _,e in phase_exits) and not any(n.startswith('Build-') for n,_ in phase_exits)
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['binaryAfter'])==9 and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for row in r['allOutputInventory']:
 b=(R/row['path']).read_bytes();assert len(b)==row['bytes'] and h(b)==row['sha256']
# Inspect original TRX diagnostics and exact semantic assertions, not parent counters alone.
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};results=t.findall('.//t:UnitTestResult',ns);assert len(results)==3 and {u.attrib['testName'] for u in results}==set(expected_methods)
assert len({u.attrib['testId'] for u in results})==3
raw_count=0
def rawbytes(x):
 global raw_count
 if x is None:return None
 b=base64.b64decode(x['Bytes'],validate=True);assert len(b)==x['Length'] and h(b).upper()==x['Sha256'];raw_count+=1;return b
def walkraw(v):
 if isinstance(v,dict):
  if set(v)=={'Bytes','Length','Sha256'}:rawbytes(v)
  else:
   for x in v.values():walkraw(x)
 elif isinstance(v,list):
  for x in v:walkraw(x)
def jsonbytes(x):return json.loads(x.decode('utf-8-sig'))
def rows(stdout):
 parsed=[]
 for line in stdout.splitlines():
  if line.startswith('{'):parsed.append(json.loads(line))
 return parsed
cases=[];timings=[];cold_detail=None
for u in results:
 assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns) is None
 j=rows(u.find('t:Output/t:StdOut',ns).text or '');by=collections.defaultdict(list)
 for x in j:
  walkraw(x)
  if 'Boundary' in x:by[x['Boundary']].append(x['Observation'])
 def one(n):assert len(by[n])==1,(n,len(by[n]));return by[n][0]
 name=u.attrib['testName'];pre=one('fr063_joint_completion_actual_prerequisites');original=pre['Original'];assert pre['Source']['TurnNumber']==42 and original['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5 and not pre['nonzeroProgression']
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['Failure'] is None and outcome['ObserverFailure'] is None and outcome['entered'] and outcome['jointOriginalAuthored']
 capture=one('iobs_actual_completed_capture');assert capture['Callback']==1 and capture['InterfaceCommittedCohorts']==0
 progression=one('fr063_joint_progression_phase_report_arithmetic');assert not progression['nonzeroProgression'] and progression['ActualOriginalControl']['worldCyclesAlreadyPendingBeforeTurn']==0 and progression['ReportBefore'] is None and progression['ReportAfterAbsent']
 assert progression['Unapplied']['phase']=='unapplied' and progression['Applied']['phase']=='applied'
 for k in ['controlFingerprint','preparedAtUtc','sourceGuards','scheduleBefore','reportBefore','scheduleAfter','reportAfter','mutationFingerprint']:assert progression['Unapplied'][k]==progression['Applied'][k]
 cleanup=[x for x in j if 'CleanupOwnedRoot' in x];assert len(cleanup)==1 and cleanup[0]['OwnedFixtureRemoved'] and cleanup[0]['CleanupFailure'] is None and not Path(cleanup[0]['CleanupOwnedRoot']).exists()
 assert Path(cleanup[0]['CleanupOwnedRoot']).parent==Path(cloud['environment']['root'])
 semantic=[]
 if 'CommittedCold' not in name:
  assert outcome['ActualCaller']=='ProcessPlayerTurn' and outcome['ActualTurn']==43 and outcome['liveCallerReturned'] and outcome['liveWaitEntered']
  union=one('fr063_joint_complete_union');assert union['OriginalTurn']==union['ActualTurn']==43 and union['FullOwnerCanonicalValidation'] and union['RequestId']==original['RequestId'] and union['SnapshotToken']==original['SnapshotToken']
  assert union['Create']['Kind']=='create' and union['Treat']['Kind']=='treat' and union['Treat']['Ordinal']==union['Create']['Ordinal']+1 and union['Create']['WoundId']!=union['Treat']['WoundId']
  assert union['spend']['AppliedAmount']==2 and union['spend']['BeforeState']['Current']==2 and union['spend']['AfterState']['Current']==0 and union['damage']['AppliedAmount']==3 and union['damage']['BeforeState']['Current']==5 and union['damage']['AfterState']['Current']==2
  assert union['receipt']['RequestId']==original['RequestId'] and union['receipt']['Turn']==43 and union['receipt']['Decision']=='materialize' and union['ExactStorySuffix']['turn']==43 and union['ExactStorySuffix']['requestId']==original['RequestId']
  repeat=one('fr063_joint_completion_inert_repeat');assert repeat['FullBytesEqual'] and not repeat['repeat'] and repeat['Turn']==43 and repeat['Before']==repeat['After']
  semantic.append('Actual live repair owner, complete create+treat union, energy2to0/health5to2, canonical validators, exact one story suffix, progression and inert-repeat assertions passed')
 if 'NewOrdinary' in name:
  new=one('iobs_actual_new_ordinary_after_joint');assert new['OriginalTurn']==44 and new['Returned']==[];marker=jsonbytes(rawbytes(new['PriorMarker']));before=jsonbytes(rawbytes(new['PublicBefore']));after=jsonbytes(rawbytes(new['PublicAfter']))
  assert marker['turn']==43 and marker['outputBinding']['version']==3 and marker['outputBinding']['publishedInterfaceResult']['origin']=='PreparedResultPublicationV1'
  opt=after['dialogueOptions'];assert len(opt)==1 and opt[0]['text']=='[NI_CONTROL:second]Продолжить обычный ответ.' and opt[0]['inputValue']=='[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.' and bool(after['timestamp']) and before!=after
  commits=by['iobs_actual_result_commit'];assert [x['Record']['state'] for x in commits]==['Prepared','Published'] and commits[0]['Record']['resultFingerprint']==commits[1]['Record']['resultFingerprint'] and all(x['Record']['normalization']=='NoWrite' for x in commits)
  semantic.append('Actual Stage44 signed prior marker coverage and non-null old cache precondition; public generic-slot I normalized from two tags to one with retained inputValue; prior marker unchanged by source assertion')
 else:
  legacy=one('iobs_actual_legacy_completion');factory=one('iobs_actual_legacy_factory_installed_readback');pending=jsonbytes(rawbytes(legacy['Pending']));assert factory['Checkpoint']['schemaVersion']==pending['jointRepairCheckpoint']['schemaVersion']==1 and pending['jointRepairCheckpoint']==factory['Checkpoint']
  assert rawbytes(factory['Pending'])==rawbytes(legacy['Pending']) and legacy['Version']==1 and len(pending['repairPackets'])>0 and len(pending['repairReceipts'])>0 and len(pending['submittedTreatmentRequests'])==1
  assert 'niPublication' not in pending['jointRepairCheckpoint'] and legacy['Common']['outputBinding']['version']==1 and 'publishedInterfaceResult' not in legacy['Common']['outputBinding'] and not by['iobs_actual_result_commit'] and rawbytes(legacy['Submitted'])==rawbytes(legacy['Final'])
  assert not any(x['InterfaceChanged'] for x in legacy['Trace']);marker=legacy['Common']
  semantic.append('Genuine existing CaptureAsync schema1 producer, exact installed/readback pending graph and original-backed reconstruction; child1 live/cold consumption without schema upgrade, result publication or I rewrite')
  if 'CommittedCold' in name:
   co=one('fr063_joint_applied_cold_parent_outcome');child=json.loads(co['Child']);ctx=co['Context'];assert co['Failure'] is None and co['Transport'] is None and co['InitialAuthority'] is None and co['NewRootAfter'] is None
   assert ctx['refusalScenario'] is None and ctx['expectedPhase']=='applied' and not ctx['newPhysicalRoot'] and ctx['expectedWorldCycles']==0 and child['root']==ctx['expectedRoot']==cleanup[0]['CleanupOwnedRoot']
   assert child['processId']!=co['ParentProcessId']==ctx['parentProcessId'] and child['actualOuterCallerInvoked'] and child['actualCallerEntered'] and child['completed'] and not child['refusalVerified'] and not child['handled'] and not child['secondIdle'] and child['actualTurn']==43 and child['after']==child['repeated']
   assert child['binaryPins']==ctx['binaryPins']==child['dependencyContext']['sha256']
   for k,path in child['dependencyContext']['paths'].items():assert h(Path(path).read_bytes()).upper()==child['binaryPins'][k]
   for dep in child['loadedDependencies']:assert dep['isDefaultContext'] and h(Path(dep['path']).read_bytes()).upper()==dep['sha256']
   nested=rows(co['FullChildLog']);native=next(x['Observation'] for x in nested if x.get('Boundary')=='cold_child_native_result');assert native['ExitCode']==0 and native['HasExited'] and not native['TimedOut'] and native['Failure'] is None and native['StdoutEofObserved'] and native['StderrEofObserved'] and native['ChildProcessId']==child['processId'] and native['ParentProcessId']==co['ParentProcessId']
   entry=next(x['Observation'] for x in nested if x.get('Boundary')=='fr063_joint_applied_child_before_actual_owner');assert entry['FreshCommonCacheEmpty'] and entry['OriginalFromOfficialReader'] and entry['TurnNumber']==43
   aux=next(x for x in nested if 'CleanupOwnedRoot' in x);assert aux['CleanupOwnedRoot']==child['auxiliaryFactoryRoot'] and aux['OwnedFixtureRemoved'] and aux['CleanupFailure'] is None and not Path(aux['CleanupOwnedRoot']).exists()
   assert outcome['ActualTurn']==42 and outcome['HandledByActualLateCaller'] and not outcome['liveCallerReturned'] and not outcome['liveWaitEntered']
   cold_detail={'parentPid':co['ParentProcessId'],'childPid':child['processId'],'nativeExit':0,'bothEof':True,'freshCommonCacheEmpty':True,'actualOfficialOriginalReader':True,'completedTurn':43,'afterEqualsRepeatedEntries':len(child['after']),'dependencyHashesMatched':True,'auxiliaryCleanupVerified':True,'childExitWaitSeconds':45,'childWaitLimitEnforcedAt':'GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerArchive.cs:596 / BrowserInputRecovery.cs:219'}
   semantic.append('Separate actual native child with empty common cache and official original reader; exact dependency bytes, exit0/join/bothEOF; old finalized mechanics unchanged, exact story+turn43 and inert-repeat; owned auxiliary removed')
 duration=sum(float(x)*m for x,m in zip(u.attrib['duration'].split(':'),[3600,60,1]));end=dt.datetime.fromisoformat(u.attrib['endTime']);approx=end-dt.timedelta(seconds=duration);prepared=dt.datetime.fromisoformat(marker['progression']['preparedAtUtc']);correction=dt.datetime.fromisoformat(one('iobs_actual_public_correction_before_ready')['Mtime'])
 timings.append({'method':name,'durationSeconds':duration,'reportedEndTime':u.attrib['endTime'],'reportedStartTime':u.attrib['startTime'],'inferredBeginFromResultEventMinusDuration':approx.isoformat(),'correctedPublicIMtime':correction.isoformat(),'commonProgressionPreparedUtc':prepared.isoformat(),'inferredBeginToCorrectionSeconds':(correction-approx).total_seconds(),'inferredBeginToCommonPreparationSeconds':(prepared-approx).total_seconds(),'commonPreparationToResultEventSeconds':(end-prepared).total_seconds()})
 cases.append({'method':name,'testId':u.attrib['testId'],'outcome':'Passed','durationSeconds':duration,'diagnosticJsonRows':len(j),'semanticChecks':semantic,'ownedCleanup':cleanup[0]})
assert len({x['ownedCleanup']['CleanupOwnedRoot'] for x in cases})==3
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-61e712e3.json');changed=git(R,'diff','--name-only',actual,carrier).decode().splitlines();assert receipt['sha']==carrier and receipt['base']==actual and len(changed)==receipt['changedPathsVerified']==16 and set(changed)=={x['path'] for x in receipt['files']}
for f in receipt['files']:
 b=git(C,'show',carrier+':'+f['path']);assert b==frozen(f['path']) and len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob']
 assert git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]
out={'kind':'INDEPENDENT_NEW_CLOUD_LEGACY3_ORIGINAL_EVIDENCE_CHECK','checkedUtc':dt.datetime.now(dt.timezone.utc).isoformat(),'carrier':carrier,'actualSource':actual,'compiledSource':compiled,'recordSha256':h(p.read_bytes()),'archive':{'sha256':h(afile.read_bytes()),'gzipRawChains':11,'rawBytes':bytes_verified,'completeOriginalCoverage':True,'restoredAndOriginalBytesMatch':True},'sourcePins':98,'sourcePinDigest':packet['pinDigest'],'selection':s['Selection'],'tests':s['Tests'],'nativeExit':0,'wallSeconds':r['wallSeconds'],'categorySeconds':category['Seconds'],'categoryBudgetSeconds':900,'noBuild':True,'cases':cases,'rawImageLengthShaChecks':raw_count,'cold':cold_detail,'binaries':{'nineCopiesAndFull1575PathsBytesHashesUnchangedFromFreshPreparationAndActualBeforeAfter':True},'cleanup':{'originalLegacyBoundariesEmpty':True,'perCaseOwnedRootsRemoved':True,'runnerCleanupSucceeded':True,'noCurrentGlobalCensus':'close2 may be running; no current process/namespace emptiness assertion or intervention'},'restoredCarrier':{'changedPaths':16,'sha256GitBlobsBytesVerified':True,'independentCloneCarrierObject':carrier,'observedHeads':observedHeads,'remoteDescendsFromCarrier':True},'timingObservations':timings,'timingLimitation':'TRX startTime/endTime are result events with nearly zero delta despite nonzero duration; inferred begins subtract duration from end event, consistent with sequential neighboring case completion but not direct stopwatch phase measurements. PreparedAt is actual plan timestamp, not publication completion time. No extrapolated runtime PASS or hard timing bound.','findings':[]}
op=D/'t179pd-ni-iobs-l01-cloud-legacy3-independent-check-61e712e3-20261018.json';op.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(str(op));print(h(op.read_bytes()));print(json.dumps({'cases':len(cases),'rawImageChecks':raw_count,'archiveChains':11,'cold':cold_detail,'timings':timings},ensure_ascii=False))
