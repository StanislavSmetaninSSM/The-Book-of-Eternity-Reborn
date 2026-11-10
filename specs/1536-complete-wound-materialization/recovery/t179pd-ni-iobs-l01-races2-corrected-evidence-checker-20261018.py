from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='f0bfcda832db851a2768d6af13ce60695c24a9cd';actual='1e2bdb75d51bd7ee549ed399d5a744b366beceff';compiled='e2d0a10f6b12cc1947ef9c9f71fd600146fcec2d'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['races2Corrected'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='59d772bb17a915158a1140ae4c41434ca179e5db463e93f369bb75f66295510a'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['mode']=='races2' and r['expectedCases']==2 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='f5c13d343de221bc86aaf3ec100869ea6e1cafdaae59b3006baa5642de328e8e'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==(C/pin['path']).read_bytes() and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='e6717c30e673e77d02b0d634e1b24a776d432b3d9ad6b2f4c36ab0713ec9a4a9' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=2,Executed=2,Passed=2,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==15 and desc['EstimatedCases']==2 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='24d642d31bc8bcb8debb366ae353b3c6c51bb7588f2b03a4e401f451ff85bd8c' and prep['mode']=='corrective-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==20448 and tick==966261 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline']==''
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='races2' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
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

# These assertions only read saved originals and current build outputs. No runtime, observer or process control is performed.
assert len(packet['productDelta'])==21
assert sorted(packet['productDelta'])==git(R,'diff','--name-only',packet['productBase'],compiled,'--','BookOfEternityClient').decode().splitlines()
unchanged=[]
for pin in packet['sourcePins']:
 if pin['path'].startswith(('BookOfEternityClient.IntegrationTests/','BookOfEternityClient.Tests/','BookOfEternityClient.TestSupport/')) or pin['path'] in ['tests/categories.json','tests/selection.json']:
  assert frozen(pin['path'])==git(R,'show','8f94b4b2b16d67d7862ea106cf6a9fb33eb86c15:'+pin['path']);unchanged.append(pin['path'])
assert len([x for x in r['binaryAfter'] if x['path'].endswith('/BookOfEternityClient.dll') and x['sha256']=='eb0b35ec8993dc65d5d6f280663415e97d9067f275de693b17867309e9387d98'])==4
assert len({x['sha256'] for x in r['binaryAfter'] if x['path'].endswith('/BookOfEternityClient.TestSupport.dll')})==1
cases=[];semantics={}
for u in us:
 assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns) is None
 definition=definitions[u.attrib['testId']];assert definition['className'].split(',')[0]+'.'+definition['name']==u.attrib['testName']
 rows=[json.loads(l) for l in u.find('t:Output/t:StdOut',ns).text.splitlines() if l.startswith('{')];by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary' in x:by[x['Boundary']].append(x['Observation'])
 def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
 pre=one('fr063_joint_completion_actual_prerequisites');original=pre['Original'];assert pre['Source']['TurnNumber']==42 and original['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5 and not pre['nonzeroProgression']
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['Failure'] is None and outcome['ObserverFailure'] is None and outcome['ActualTurn']==42 and outcome['liveCallerReturned'] and outcome['entered'] and outcome['jointOriginalAuthored'] and outcome['liveWaitEntered'] and outcome['ActualCaller']=='ProcessPlayerTurn'
 clean=next(x for x in rows if 'CleanupOwnedRoot' in x);assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and not Path(clean['CleanupOwnedRoot']).exists() and str(Path(clean['CleanupOwnedRoot']).parent)==cloud['environment']['root']
 state=one('iobs_actual_source_race_held_no_repair');scenario=state['scenario'];assert state['Mutations']==1 and state['Diagnostics']==outcome['EngineDiagnostics'];diagnostic=state['Diagnostics']
 direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['scenario']==scenario and 'MortalJointRepairPublicationHoldException' in direct['DirectFailure'] and 'RestorePreTurnBackup' in direct['DirectFailure'] and direct['Before']==direct['After']
 for k,v in direct['Before'].items():assert raw(v)==raw(direct['After'][k]) and Path(k).is_relative_to(clean['CleanupOwnedRoot'])
 mechanical=one('iobs_actual_public_correction_before_ready')['Mechanical'];assert len(mechanical)==15
 assert not by['iobs_actual_completed_capture'] and not by['fr063_joint_complete_union'] and not by['fr063_joint_completion_inert_repeat'] and not any(x['MarkerCommitted'] for x in state['Trace'])
 pending=json.loads(raw(state['Pending']).decode('utf-8-sig'));mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem')
 if scenario=='ReadRace':
  assert u.attrib['testName'].endswith('GuaranteedTreatment_ActualReturnedReadCannotHideChangedWriterPreimage')
  omitted=one('iobs_actual_original_omits_interface_snapshot');assert not omitted['InterfaceSnapshotDeclared'] and 'output/interface_updates.json' not in omitted['DeclaredNames'] and omitted['RequestId']==original['RequestId']
  fault=one('iobs_actual_returned_read_physical_preimage_fault');assert fault['path']=='output/interface_updates.json' and raw(fault['After'])==raw(fault['Before'])+b' ' and fault['FreshConsumed'] and fault['ReadyDeleteReturned'] and fault['ReadyReleaseReturned']
  assert 'jointInterfacePublication' not in pending and raw(state['Pending'])==raw(fault['Pending']) and raw(state['Foreign'])==raw(state['Interface'])==raw(fault['After']) and not state['ResultCommits'] and not state['Trace'] and not by['iobs_actual_result_commit'] and not by['iobs_actual_committed_member']
  reason='joint_interface_consumed_read_preimage_changed';assert diagnostic.count(reason)==1 and 'System.IO.InvalidDataException' in diagnostic and diagnostic.count('The source-bound joint repair owner is blocked; outer rollback is suppressed.')==1 and not mem['CanonicalRefreshPresent']
  assert direct['ActualOuterDisposition']=='MortalJointRepairPublicationHeldBlocked' and len(direct['Before'])==220
  extra=dict(originalInterfaceSnapshotAbsent=True,actualReturnedReadAfterLeasedPreimage=True,readyDeleteAndReleaseReturned=True,freshConsumed=True,physicalInterfaceWhitespaceFaultRetained=True,interfaceMtimeUnchangedByReachedAssertion=True,noPreparedOrPublishedRecord=True,noFreshDataAndPreparationHeldByReachedAssertions=True,outerWrapperInvoked=True,outerWrapperDisposition=direct['ActualOuterDisposition'])
 elif scenario=='ReadyDrift':
  assert u.attrib['testName'].endswith('GuaranteedTreatment_PublishedInterfaceCannotExcuseOtherImmutableReadyDrift')
  fault=one('iobs_actual_published_ready_noninterface_drift');assert fault['Path']=='game_state/player/experience.json' and raw(fault['After'])==raw(fault['Before'])+b' ' and raw(state['Foreign'])==raw(fault['After'])
  commits=by['iobs_actual_result_commit'];assert [x['Record']['state'] for x in commits]==['Prepared','Published'];published=commits[1]['Record'];assert fault['Record']==published==pending['jointInterfacePublication'] and state['ResultCommits']==[x['Record'] for x in commits]
  assert commits[0]['Record']['resultFingerprint']==published['resultFingerprint'] and published['normalization']=='Changed' and published['issues']==[] and published['binding']['requestId']==original['RequestId']
  assert raw(state['Interface'])==raw(commits[1]['Interface'])==base64.b64decode(published['afterImage']['base64'])
  member=one('iobs_actual_committed_member');assert member['InterfaceChanged'] and not member['MarkerCommitted'] and state['Trace']==[member]
  reason='joint_interface_ready_submitted_source_changed';assert diagnostic.count(reason)==1 and 'System.IO.InvalidDataException: '+reason+': game_state/player/experience.json' in diagnostic and diagnostic.count('Joint canonical publication retained PublicationUncertain after an exception.')==1
  assert mem['CanonicalRefreshPresent'] and mem['CanonicalDisposition']=='PublicationUncertain' and mem['IssueCount']==mem['ReportedIssueCount']==1 and not mem['IssueRowsTruncated'] and mem['CompletePublicIssueProjectionSha256']=='17CBEA752EE6CFF7EAB8EE65DC9CC56E22082B545156F0758DF71BE8A70D7888'
  issue=mem['Issues'][0];assert issue['Message']['Value']==reason+': game_state/player/experience.json' and issue['Code']['Value']=='mortal_joint_repair_admission_refused'
  for k in ['Code','Message','FilePath']:
   field=issue[k];assert not field['Truncated'] and len(field['Value'])==field['OriginalLength'] and h(field['Value'].encode()).upper()==field['FullUtf8Sha256']
  assert direct['ActualOuterDisposition'] is None and len(direct['Before'])==222
  extra=dict(realPreparedThenPublished=True,exactPublishedPendingAndInterfaceRetained=True,onlyInterfaceOwnedTransitionPermitted=True,foreignExperienceWhitespaceRetained=True,readySourceOriginalNotReseededByReachedAssertion=True,attemptNotLiveOrDelegatedAndNoContinuationReservationStagesByReachedAssertions=True,capabilitiesExpiredByReachedAssertion=True,originalExceptionLoggedExactlyOnce=True,retainedIssueUnchangedFromOriginalFailure=True,inMemoryDisposition=mem['CanonicalDisposition'],outerWrapperInvoked=False)
 else:raise AssertionError(scenario)
 for path,before in mechanical.items():
  want=fault['After'] if scenario=='ReadyDrift' and path==fault['Path'] else before;data=raw(want);key=str(Path(clean['CleanupOwnedRoot'])/'game_session'/path)
  assert data==raw(direct['Before'].get(key))==raw(direct['After'].get(key))
  final_image=outcome['After'].get(path)
  if data is None:assert final_image is None or not final_image['Exists']
  else:assert final_image['Exists'] and final_image['Length']==len(data) and final_image['Sha256']==h(data).upper()
 semantics[scenario]=dict(**extra,physicalMutationCount=1,heldAtTurn42=True,noCompletedCaptureOrCommonMarker=True,finalMechanicalForeachReached=True,mechanicalPaths=15,mechanicalBytesEqualExpectedOriginalOrInjectedFault=True,directActualStageBackupRestoreReached=True,directRestoreHold=True,directBeforeAfterPaths=len(direct['Before']),directBeforeAfterBytesEqual=True,diagnosticSha256=h(diagnostic.encode()),diagnosticReason=reason)
 duration=sum(float(x)*m for x,m in zip(u.attrib['duration'].split(':'),[3600,60,1]));cases.append(dict(method=u.attrib['testName'],testId=u.attrib['testId'],outcome='Passed',durationSeconds=duration,diagnosticRows=len(rows),boundaries=dict((k,len(v)) for k,v in by.items() if v),cleanup=clean))
assert set(semantics)=={'ReadRace','ReadyDrift'} and len({x['cleanup']['CleanupOwnedRoot'] for x in cases})==2
censuses=[]
for ref in info['supplementalAuthoritativeCensuses']:
 b=frozen(ref['path']);assert len(b)==ref['bytes'] and h(b)==ref['sha256'] and b==git(C,'show',carrier+':'+ref['path']);c=json.loads(b)
 assert c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and c['cleanBoundary'] and not any(c[k] for k in ['unknown','confirmedGone','activeTaskGameTestProcesses','ownedRoots']) and c['fixtureTemp']==cloud['environment']['root'] and c['fixtureTempUid']==1000
 assert len(c['pidsEnumerated'])==len(c['entries'])==62 and set(c['pidsEnumerated'])=={x['pid'] for x in c['entries']}
 for x in c['entries']:
  def ids(raw):f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick']) and ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv'in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
  if x['classification']=='exited-nonexecuting':assert x['state'] in {'Z','X','x'} and x['argvCount']==0
 assert all(x['status']=='MATCH' for x in c['knownVisibility']);known=next(x for x in c['entries'] if x['pid']==10433);assert known['startTick']==477854 and known['classification']=='exited-nonexecuting'
 censuses.append(dict(path=ref['path'],sha256=ref['sha256'],startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=62,clean=True,knownVisibility=c['knownVisibility']))
before_gap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();after_gap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds();assert before_gap==47.661597 and after_gap==160.682168
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-f0bfcda8.json');changed=git(R,'diff','--name-only',actual,carrier).decode().splitlines();assert receipt['sha']==carrier and receipt['base']==actual and receipt['changedPathsVerified']==len(changed)==19 and set(changed)=={f['path'] for f in receipt['files']}
for f in receipt['files']:
 b=frozen(f['path']);assert b==git(C,'show',carrier+':'+f['path']) and len(b)==f['bytes'] and h(b)==f['sha256'] and blob(b)==f['gitBlob'] and git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]

for ref in [info['parentInspection'],info['preflightReadback']]:
 b=frozen(ref['path']);assert h(b)==ref['sha256'] and ('bytes' not in ref or len(b)==ref['bytes'])
assert receipt['remoteShaVerified'] is True and receipt['clean'] and not receipt['localSourceOrObjectReuse'] and receipt['sourcePinsVerified']==98 and receipt['sourcePinDigest']==packet['pinDigest']
assert cloud['qualifiedCases']['totalDistinctTargets']==42 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
old=cloud['actuals']['races2'];assert h(Path(old['recordPath']).read_bytes())==old['recordSha256']=='359cdd08bf17c7b19b8292e946a0be9d964ac0cc42a54e86bf18652fbb0c5dfb' and h(frozen(old['archive']['path']))=='12c6c0e7c9d09593a5ee809dadd2439b0136934fe4a7f87c5d3263a69ff0066e'
assert read(old['recordPath'])['nativeExit']==1
out=dict(kind='INDEPENDENT_CORRECTED_RACES2_EVIDENCE_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,originalStoredIndependentRestoreBytesMatched=True),sourcePins=98,sourcePinDigest=packet['pinDigest'],productDelta=21,unchangedTestFixtureSupportCatalogSelectionPins=unchanged,tests=s['Tests'],selection=s['Selection'],nativeExit=0,wallSeconds=r['wallSeconds'],categorySeconds=category['Seconds'],categoryBudgetSeconds=900,noTimeout=True,noBuild=True,cases=cases,semanticOracles=semantics,rawImageLengthShaChecks=rawcount,embeddedImageShaChecks=embeddedcount,binaries=dict(selectedCopies=9,completeOutputFiles=1575,originalBeforeAfterPrepAndCurrentPhysicalExact=True,clientSha256='eb0b35ec8993dc65d5d6f280663415e97d9067f275de693b17867309e9387d98'),launch=dict(pid=pid,startTick=tick,argvMatchesController=True,immediateCmdlineEmpty=True),cleanup=dict(perCaseOwnedRootsRemoved=True,nativeRunnerCleanupSucceeded=True,originalExeDependentProcessArraysNotCompleteCensus=True,censuses=censuses,beforeSnapshotToStartSeconds=before_gap,finishToAfterSnapshotSeconds=after_gap,afterGapDecision='Disclosed departure from planned immediate capture. Finite dated boundary evidence accepted with native own-tree/runtime cleanup, reached per-case cleanup assertions and stable inventories. Parent reports no intervening actual or process/root action; this is not independently continuous observation. No instantaneous or continuous absence claim and no gap reconstruction. No concrete settlement3 gate blocker from delay alone; settlement requires its own explicit release and clean fresh preflight.',noCurrentGlobalCensusPerformed=True),restoredCarrier=dict(paths=19,bytesSha256GitBlobsModesVerified=True),preserved=dict(originalRaces2Native1Immutable=True,retained42HistoricalProvenanceUnchanged=True,oldCorrectedCrash='UNKNOWN_UNAVAILABLE',noOldRuntimeReplay=True),findings=[],scope='Only the two corrected races2 methods. Adds two distinct qualified cases to retained42 after saving acceptance, yielding44/57. Does not qualify settlement3 or other remaining13, cold child/common completion, or whole runtime.')
op=D/'t179pd-ni-iobs-l01-races2-corrected-independent-check-f0bfcda8-20261018.json';op.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(op),sha256=h(op.read_bytes()),bytes=op.stat().st_size,findings=[],rawImageChecks=rawcount,embeddedImageChecks=embeddedcount,directPaths={k:v['directBeforeAfterPaths'] for k,v in semantics.items()},afterGapSeconds=after_gap)))
