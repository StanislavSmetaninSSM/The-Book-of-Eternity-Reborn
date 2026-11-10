from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='23362e5701daf6f01aa989f7fde070242a9167d3';actual='b5d9becea7e1e802dc362dfab78791f25e0889d3';compiled='cf52201ca165d24c971018251ef9723a8e493384'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['close2Corrected'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='d8ed64a23966ba4150f8c6438714ab43332bc19c1f781625575c77d4231e6d37'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['mode']=='close2' and r['expectedCases']==2 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='b29c971342ee772b13cd123a954d7afa8937e1f50a7e5f4c4c14403e4de4c347'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes() and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='66862a2d5556dc5245eff0f4fa0dcd2b75d4012ae43d67db6a8a4dd24fbbfdc8' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=2,Executed=2,Passed=2,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==15 and desc['EstimatedCases']==2 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='2a20dac89741ce9ab3d87dbc22912d06e8c32ef94918f0ce2cdfccfa5917735d' and prep['mode']=='corrective-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==13436 and tick==605604 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline']==''
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='close2' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert len(expected)==2 and set(expected.values())=={1} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'][0]['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e=='0' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==2 and {u.attrib['testName'] for u in us}==set(expected)
definitions={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};assert len(definitions)==2
rawcount=0;embeddedcount=0
def raw(x):
 global rawcount
 if x is None:return None
 b=base64.b64decode(x['Bytes'],validate=True);assert len(b)==x['Length'] and h(b).upper()==x['Sha256'];rawcount+=1;return b
def walk(x):
 global embeddedcount
 if isinstance(x,dict):
  if set(x)=={'Bytes','Length','Sha256'}:raw(x)
  elif {'present','sha256','base64'}<=set(x):
   if x['present']:
    b=base64.b64decode(x['base64'],validate=True);assert h(b).upper()==x['sha256'];embeddedcount+=1
  else:
   for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
cases=[];held=None;outer=None
for u in us:
 assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns) is None
 definition=definitions[u.attrib['testId']];assert definition['className'].split(',')[0]+'.'+definition['name']==u.attrib['testName']
 rows=[json.loads(l) for l in u.find('t:Output/t:StdOut',ns).text.splitlines() if l.startswith('{')];by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
 def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
 pre=one('fr063_joint_completion_actual_prerequisites');original=pre['Original'];assert pre['Source']['TurnNumber']==42 and original['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5 and not pre['nonzeroProgression']
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['ObserverFailure'] is None and outcome['entered'] and outcome['jointOriginalAuthored'] and outcome['liveWaitEntered'] and outcome['ActualCaller']=='ProcessPlayerTurn'
 commits=by['iobs_actual_result_commit'];assert [v['Record']['state'] for v in commits]==['Prepared','Published'] and commits[0]['Record']['resultFingerprint']==commits[1]['Record']['resultFingerprint'];published=commits[1]['Record'];assert published['normalization']=='Changed' and published['issues']==[] and published['binding']['requestId']==original['RequestId']
 assert raw(commits[1]['Interface'])==base64.b64decode(published['afterImage']['base64'])
 clean=next(x for x in rows if 'CleanupOwnedRoot'in x);assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and not Path(clean['CleanupOwnedRoot']).exists() and str(Path(clean['CleanupOwnedRoot']).parent)==cloud['environment']['root']
 if 'InterfaceOwningClose'in u.attrib['testName']:
  state=one('iobs_actual_interface_owning_close_retained');attached=one('iobs_actual_initial_install_close_attached');assert attached['_initialInstalled'] and attached['_initialPaused'] and 'PrepareAndPublishWithOwningReleaseAsync'in attached['ActualOwner']
  assert state['Calls']==1 and state['Published']==published and not state['DataDelivered'] and not state['OwningReleaseConfirmed'] and state['OuterFinalizationIsDistinct'] and raw(state['BeforeClose'])==raw(state['ActualPending']) and raw(state['Interface'])==raw(commits[1]['Interface'])
  diagnostic=state['Diagnostics'];assert diagnostic.count('L01 actual I publication owning release failure.')==1 and diagnostic.count('The source-bound joint repair owner is blocked; outer rollback is suppressed.')==1 and 'InitialInterfaceThrowingClose.Dispose'in diagnostic and 'ReleaseOwnedLeaseAsync'in diagnostic
  assert outcome['Failure'] is None and outcome['ActualTurn']==42 and outcome['liveCallerReturned'] and outcome['EngineDiagnostics']==diagnostic
  member=one('iobs_actual_committed_member');assert member['InterfaceChanged'] and not member['MarkerCommitted'] and not by['iobs_actual_completed_capture'] and not by['fr063_joint_complete_union'] and not by['fr063_joint_completion_inert_repeat']
  direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['scenario']=='InterfaceClose' and 'MortalJointRepairPublicationHoldException'in direct['DirectFailure'] and 'RestorePreTurnBackup'in direct['DirectFailure'] and direct['ActualOuterDisposition'] is None and direct['Before']==direct['After']
  for k,v in direct['Before'].items():assert raw(v)==raw(direct['After'][k]) and Path(k).is_relative_to(clean['CleanupOwnedRoot'])
  correction=one('iobs_actual_public_correction_before_ready');mechanical=correction['Mechanical']
  for k,v in mechanical.items():
   key=str(Path(clean['CleanupOwnedRoot'])/'game_session'/k);assert raw(v)==raw(direct['Before'].get(key))
  held=dict(originalDiagnosticPresentExactlyOnce=True,actualOwningLeaseReleaseCalls=1,leaseExpiredAndAmbientCapabilitiesClearedByUnchangedAssertions=True,publishedAndPendingBytesRetained=True,noFreshData=True,noCommonCaptureOrMechanicalEffects=True,mechanicalPaths=len(mechanical),directRestoreReached=True,directRestoreHold=True,directBeforeAfterPaths=len(direct['Before']),directBeforeAfterBytesEqual=True,outerWrapperInvoked=False,diagnosticSha256=h(diagnostic.encode()))
 else:
  cut=one('iobs_actual_outer_finalization_cut');retain=one('iobs_actual_outer_finalization_retained');ordering=one('iobs_ordering_original_outcome');capture=one('iobs_actual_completed_capture')
  assert not cut['OriginalCloseReturned'] and cut['InterfaceOwningReleaseAlreadyDelivered'] and cut['Turn']==43 and cut['Record']==retain['Record']==published and cut['Marker']==retain['Marker']==ordering['FirstActualMarker']
  assert 'MainOperationContinuationException'in retain['Failure'] and 'L01 actual outer session finalization after committed I/common.'in retain['Failure'] and retain['Outcome']=='Completed' and retain['EstablishedResult'] is None and retain['OriginalClose'] is None and outcome['Failure']==retain['Failure'] and not outcome['liveCallerReturned'] and outcome['ActualTurn']==43
  assert capture['Callback']==capture['InterfaceCommittedCohorts']==ordering['Callback']==ordering['InterfaceAtCapture']==1 and ordering['CompletionObservedBeforeOwnedCleanup'] and ordering['ActualTurn']==43
  members=by['iobs_actual_committed_member'];iw=[x for x in members if x['InterfaceChanged']];commons=[x for x in members if x['MarkerCommitted']];assert len(iw)==1 and len(commons)==2 and iw[0]['Sequence']<commons[0]['Sequence']
  assert raw(ordering['AtCapture'])==raw(ordering['Final'])==raw(retain['Interface'])==raw(cut['Interface'])==raw(capture['InterfaceRaw'])==raw(commits[1]['Interface'])
  final=json.loads(raw(ordering['Final']));option=final['dialogueOptions'];assert len(option)==1 and option[0]['text']=='[NI_CONTROL:second]Продолжить обычный ответ.' and option[0]['inputValue']=='[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.' and final['timestamp']
  marker=ordering['FirstActualMarker'];assert marker['outputBinding']['version']==3 and marker['outputBinding']['publishedInterfaceResult']['origin']=='PreparedResultPublicationV1' and marker['outputBinding']['publishedInterfaceResult']['record']==published and marker['niPublication']['interfaceResultFingerprint']==published['resultFingerprint'] and marker['niPublication']['publishedRecordFingerprint']==published['fingerprint']
  union=one('fr063_joint_complete_union');assert union['OriginalTurn']==union['ActualTurn']==43 and union['FullOwnerCanonicalValidation'] and union['RequestId']==original['RequestId'] and union['SnapshotToken']==original['SnapshotToken'] and union['RequestFingerprint']==published['binding']['requestFingerprint']
  assert union['Create']['Kind']=='create' and union['Treat']['Kind']=='treat' and union['Treat']['Ordinal']==union['Create']['Ordinal']+1 and union['Create']['WoundId']!=union['Treat']['WoundId']
  assert union['spend']['AppliedAmount']==2 and union['spend']['BeforeState']['Current']==2 and union['spend']['AfterState']['Current']==0 and union['damage']['AppliedAmount']==3 and union['damage']['BeforeState']['Current']==5 and union['damage']['AfterState']['Current']==2
  assert union['receipt']['RequestId']==original['RequestId'] and union['receipt']['Turn']==43 and union['receipt']['Decision']=='materialize' and union['ExactStorySuffix']['turn']==43 and union['ExactStorySuffix']['requestId']==original['RequestId']
  repeat=one('fr063_joint_completion_inert_repeat');assert repeat['FullBytesEqual'] and not repeat['repeat'] and repeat['Turn']==43 and repeat['Before']==repeat['After']
  progression=one('fr063_joint_progression_phase_report_arithmetic');assert not progression['nonzeroProgression'] and progression['ReportBefore'] is None and progression['ReportAfterAbsent'] and progression['Unapplied']['phase']=='unapplied' and progression['Applied']['phase']=='applied'
  for k in ['controlFingerprint','preparedAtUtc','sourceGuards','scheduleBefore','reportBefore','scheduleAfter','reportAfter','mutationFingerprint']:assert progression['Unapplied'][k]==progression['Applied'][k]
  assert not by['fr063_joint_applied_cold_parent_outcome'] and not by['iobs_actual_current_resume'] and not by['iobs_actual_new_ordinary_after_joint']
  outer=dict(owningDataPrecedesOuterClose=True,originalOuterFailurePreservedInTypedContinuation=True,establishedOutcome='Completed',outerAwaitSucceeded=False,interfaceCommittedOnceBeforeCommon=True,completedCaptureOnce=True,child3ContainsExactPublishedRecord=True,fullCreateTreatUnion=True,energy='2->0',health='5->2',canonicalValidation=True,exactOneStorySuffix=True,turn43=True,zeroProgressionCorrect=True,actualLateIdleRepeatInert=True,repeatComparedPaths=len(repeat['Before']),coldChildExecuted=False,dedicatedCurrentDataTakeOrLateRefusalScenariosExecuted=False)
 duration=sum(float(x)*m for x,m in zip(u.attrib['duration'].split(':'),[3600,60,1]));cases.append(dict(method=u.attrib['testName'],testId=u.attrib['testId'],outcome='Passed',durationSeconds=duration,diagnosticRows=len(rows),boundaries=dict((k,len(v)) for k,v in by.items() if v),cleanup=clean))
assert held and outer and len({x['cleanup']['CleanupOwnedRoot'] for x in cases})==2
censuses=[]
for ref in info['supplementalAuthoritativeCensuses']:
 b=frozen(ref['path']);assert len(b)==ref['bytes'] and h(b)==ref['sha256'] and b==git(C,'show',carrier+':'+ref['path']);c=json.loads(b)
 assert c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and c['cleanBoundary'] and not any(c[k] for k in ['unknown','confirmedGone','activeTaskGameTestProcesses','ownedRoots']) and c['fixtureTemp']==cloud['environment']['root'] and c['fixtureTempUid']==1000
 assert len(c['pidsEnumerated'])==len(c['entries'])==41 and set(c['pidsEnumerated'])=={x['pid'] for x in c['entries']}
 for x in c['entries']:
  def ids(raw):f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick']) and ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv'in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
  if x['classification']=='exited-nonexecuting':assert x['state'] in {'Z','X','x'} and x['argvCount']==0
 assert all(x['status']=='MATCH' for x in c['knownVisibility']);known=next(x for x in c['entries'] if x['pid']==10433);assert known['startTick']==477854 and known['classification']=='exited-nonexecuting'
 censuses.append(dict(path=ref['path'],sha256=ref['sha256'],startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=41,clean=True,knownVisibility=c['knownVisibility']))
before_gap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();after_gap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds();assert before_gap==6.359668 and after_gap==33.042601
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-23362e57.json');changed=git(R,'diff','--name-only',actual,carrier).decode().splitlines();assert receipt['sha']==carrier and receipt['base']==actual and receipt['changedPathsVerified']==len(changed)==20 and set(changed)=={f['path'] for f in receipt['files']}
for f in receipt['files']:
 b=frozen(f['path']);assert b==git(C,'show',carrier+':'+f['path']) and len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob'] and git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]
for ref in [info['parentInspection']]+info['acceptanceRecoveryAndIndependentPreflight']:
 b=frozen(ref['path']);assert h(b)==ref['sha256'] and len(b)==ref['bytes']
assert cloud['qualifiedCases']['totalDistinctTargets']==40 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
old=cloud['actuals']['close2'];assert h(Path(old['recordPath']).read_bytes())==old['recordSha256']=='44bd4147341b6590ecd5867a0c1719989dd4b02f7480641b1b134f6037a59219' and h(frozen(old['archive']['path']))=='82477104f4dc7b6e2c68b7b56300223b6d44075f811b57f0ffbded33d1e04319'
out=dict(kind='INDEPENDENT_CORRECTED_CLOSE2_EVIDENCE_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,originalStoredIndependentRestoreBytesMatched=True),sourcePins=98,sourcePinDigest=packet['pinDigest'],tests=s['Tests'],selection=s['Selection'],nativeExit=0,wallSeconds=r['wallSeconds'],categorySeconds=category['Seconds'],categoryBudgetSeconds=900,noTimeout=True,noBuild=True,cases=cases,interfaceOwningClose=held,outerFinalization=outer,rawImageLengthShaChecks=rawcount,embeddedImageShaChecks=embeddedcount,binaries=dict(selectedCopies=9,completeOutputFiles=1575,originalBeforeAfterPrepAndCurrentPhysicalExact=True),launch=dict(pid=pid,startTick=tick,argvMatchesController=True,immediateCmdlineEmpty=True),cleanup=dict(perCaseOwnedRootsRemoved=True,nativeRunnerCleanupSucceeded=True,originalExeDependentProcessArraysNotCompleteCensus=True,censuses=censuses,beforeSnapshotToStartSeconds=before_gap,finishToAfterSnapshotSeconds=after_gap,afterGapDecision='Disclosed finite dated boundary gap; accepted with native owned cleanup, per-case cleanup assertions, stable inventories, and parent report of no intervening actual/source/fixture/process action. No instantaneous or continuous absence claim; gap not reconstructed.',noCurrentGlobalCensusPerformed=True),restoredCarrier=dict(paths=20,bytesSha256GitBlobsModesVerified=True),preserved=dict(originalClose2Native1Immutable=True,retained40HistoricalProvenanceUnchanged=True,oldCorrectedCrash='UNKNOWN_UNAVAILABLE',noOldRuntimeReplay=True),findings=[],scope='Only the two corrected close2 methods; this review does not qualify races2, cold child, current-DATA take/refusal families, remaining actual modes, or a whole runtime.')
op=D/'t179pd-ni-iobs-l01-close2-corrected-independent-check-23362e57-20261018.json';op.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(op),sha256=h(op.read_bytes()),bytes=op.stat().st_size,findings=[],rawImageChecks=rawcount,embeddedImageChecks=embeddedcount,directPaths=held['directBeforeAfterPaths'],afterGapSeconds=after_gap)))
