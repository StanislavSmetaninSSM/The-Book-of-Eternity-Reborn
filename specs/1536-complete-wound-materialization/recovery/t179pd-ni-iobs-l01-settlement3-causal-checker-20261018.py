from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='4406e7238ae254f74e516e67dc3d7bec7db9b295';actual='f0bfcda832db851a2768d6af13ce60695c24a9cd';compiled='e2d0a10f6b12cc1947ef9c9f71fd600146fcec2d'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['settlement3'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='dc135269fb73456f61e1e508d3031df0f25793a237398f029649e649bf3d18ab'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==1 and r['mode']=='settlement3' and r['expectedCases']==3 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='f5c13d343de221bc86aaf3ec100869ea6e1cafdaae59b3006baa5642de328e8e'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==git(R,'show',compiled+':'+pin['path'])==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='764ef854f6c66cb6804452954d36c80746efd3cd734068f25791a722ed8e47b2' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[1]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=3,Executed=3,Passed=1,Failed=2) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=3,CompletedCases=3,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==1 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert not category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==15 and desc['EstimatedCases']==3 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='f3791289219de2d3185ea8393a9268c59cea0e770994485d27a8d6b51393d6ce' and prep['mode']=='remaining-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==21363 and tick==1020259 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline'].split()==r['argv']
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='settlement3' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert len(expected)==2 and set(expected.values())=={1,2} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e in {'0','1'} for _,e in exits) and any(e=='1' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==3 and {u.attrib['testName'].split('(fault:')[0] for u in us}==set(expected)
definitions={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};assert len(definitions)==3
rawcount=0;embeddedcount=0
def raw(x):
 global rawcount
 if x is None:return None
 b=base64.b64decode(x['Bytes'],validate=True);assert len(b)==x['Length'] and h(b).upper()==x['Sha256'];rawcount+=1;return b
def walk(x):
 global embeddedcount
 if isinstance(x,dict):
  if set(x)=={'Bytes','Length','Sha256'}:raw(x)
  elif set(x)=={'Exists','Bytes','Sha256'}:journal_image(x)
  elif {'present','sha256','base64'}<=set(x):
   if x['present']:
    b=base64.b64decode(x['base64'],validate=True);assert h(b).upper()==x['sha256'];embeddedcount+=1
  else:
   for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)


journalchecks=0
def journal_image(x):
 global journalchecks
 if not x['Exists']:assert x['Bytes'] is None and x['Sha256'] is None;return None
 b=base64.b64decode(x['Bytes'],validate=True);assert h(b).upper()==x['Sha256'];journalchecks+=1;return b
sourcepaths=['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceSettlement.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceLifecycle.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCloseRetention.cs','BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs','BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient/Core/TrustedLocalFilePublication.cs','BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalJointRepair.cs','BookOfEternityClient/Services/MortalJointRepairPublicationTransaction.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairTerminalRelease.cs','BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepairRelease.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs']
source=[]
for path in sourcepaths:
 b=frozen(path);assert b==git(R,'show',compiled+':'+path)==git(C,'show',carrier+':'+path);source.append(dict(path=path,sha256=h(b),bytes=len(b)))
fixture=frozen(sourcepaths[0]).decode();storage=frozen(sourcepaths[3]).decode();publisher=frozen(sourcepaths[5]).decode()
assert 'private readonly IOException _settlementFailure' in fixture and '_settlementFaults != 0' in fixture and 'throw _settlementFailure;' in fixture
assert 'outcome.Failure is InvalidDataException' in storage and 'outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack' in storage and '!IsTransientFileAccessException(outcome.Failure)' in storage
assert 'attempt.Prepared.Members.All(member => Matches(member.Path, member.Before))' in publisher
cases=[];diagnoses={}
for u in us:
 definition=definitions[u.attrib['testId']];method=definition['className'].split(',')[0]+'.'+definition['name'];assert method==u.attrib['testName'].split('(fault:')[0]
 rows=[json.loads(l)for l in u.find('t:Output/t:StdOut',ns).text.splitlines()if l.startswith('{')];by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
 def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
 pre=one('fr063_joint_completion_actual_prerequisites');assert pre['Source']['TurnNumber']==42 and pre['Original']['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['Failure'] is None and outcome['ObserverFailure'] is None and outcome['entered'] and outcome['liveCallerReturned'] and outcome['liveWaitEntered'] and outcome['jointOriginalAuthored']
 mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem');mechanical=one('iobs_actual_public_correction_before_ready')['Mechanical'];assert len(mechanical)==15
 clean=next(x for x in rows if 'CleanupOwnedRoot'in x);assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and not Path(clean['CleanupOwnedRoot']).exists() and str(Path(clean['CleanupOwnedRoot']).parent)==cloud['environment']['root']
 commits=by['iobs_actual_result_commit'];err=u.find('t:Output/t:ErrorInfo',ns);message=None if err is None else err.find('t:Message',ns).text;stack=None if err is None else err.find('t:StackTrace',ns).text
 scenario='CommonFault' if 'CommonMember' in method else ('TerminalFault' if '(fault: True)' in u.attrib['testName'] else 'Terminal')
 if scenario in {'CommonFault','TerminalFault'}:
  assert u.attrib['outcome']=='Failed';fault=one('iobs_actual_owning_member_fault');journal=fault['Journal'];assert fault['scenario']==scenario and fault['Phase']=='MemberPublished' and fault['Index']==0 and fault['Error']=='L01 actual whole-owner member publication fault.' and not journal['Committed'] and journal['GenerationBefore']==journal['GenerationAfter']
  members={x['Path'].split('/game_session/')[-1]:x for x in journal['Members']};assert len(members)==len(journal['Members'])
  for x in journal['Members']:assert Path(x['Path']).is_relative_to(clean['CleanupOwnedRoot'])
  assert not by['iobs_actual_common_compensation_retained'] and not by['iobs_actual_whole_terminal_settlement']
 if scenario=='CommonFault':
  assert 'Assert.NotNull() Failure' in message and 'JointInterfaceSettlement.cs:line 65' in stack and len(members)==16
  assert [x['Record']['state']for x in commits]==['Prepared','Published'];published=commits[1]['Record'];assert not published['issues'] and published['normalization']=='Changed'
  assert raw(commits[1]['Interface'])==base64.b64decode(published['afterImage']['base64'])
  pending=json.loads(journal_image(members['game_state/control/pending_wound_resolutions.json']['Before']).decode('utf-8-sig'));assert pending['jointInterfacePublication']==published and journal_image(members['game_state/control/pending_wound_resolutions.json']['After']) is None
  marker=json.loads(journal_image(members['game_state/wounds/mortal_qte_decision_publication.json']['After']).decode('utf-8-sig'));assert marker['outputBinding']['version']==3 and marker['outputBinding']['publishedInterfaceResult']['record']==published
  capture=one('iobs_actual_completed_capture');assert capture['Callback']==capture['InterfaceCommittedCohorts']==1 and raw(capture['InterfaceRaw'])==raw(commits[1]['Interface'])
  pubs=by['iobs_actual_committed_member'];assert len(pubs)==3 and pubs[0]['InterfaceChanged'] and not pubs[0]['MarkerCommitted'] and all(x['MarkerCommitted'] and not x['InterfaceChanged'] for x in pubs[1:]) and journal['TransactionId'] not in {x['TransactionId']for x in pubs}
  assert outcome['ActualTurn']==43 and 'completed once after newly published active joint completion' in outcome['EngineDiagnostics'] and not mem['CheckpointPresent'] and not mem['CanonicalRefreshPresent'] and not mem['CorrectionReady'] and not by['iobs_actual_direct_and_outer_restore_retention']
  comparisons=[]
  for path,before in mechanical.items():
   b=raw(before);after=outcome['After'].get(path);same=(b is None and (after is None or not after['Exists'])) or (b is not None and after is not None and after['Exists'] and after['Length']==len(b) and after['Sha256']==h(b).upper());comparisons.append(dict(path=path,unchanged=same))
  assert sum(not x['unchanged']for x in comparisons)==10
  finalI=outcome['After']['output/interface_updates.json'];assert finalI['Sha256']==commits[1]['Interface']['Sha256'] and finalI['Length']==commits[1]['Interface']['Length']
  diagnoses[scenario]=dict(failure=message,stack=stack,faultTransaction=journal['TransactionId'],faultMembers=16,appliedMemberIndex=0,committedInterfaceOnce=True,completedCaptureOnce=True,laterCommittedMarkerTransactions=[x['TransactionId']for x in pubs[1:]],observedRuntimeTurn=43,refreshAndCheckpointCleared=True,finalInterfaceMatchesPublished=True,independentFinalMechanicalDigestComparison=comparisons,reached=['genuine prerequisites','one injected real member fault','one completed capture'],unreached=['non-null refresh','RetryableHeld disposition','retained Published pending/record/exact I assertions','no common marker','turn42','mechanical-before retention foreach','actual Stage direct restore','common-compensation diagnostic emission'],classification='FIXTURE_TRANSIENT_FAULT_RETRIED_BELOW_OWNING_COMPENSATION',sourceExplanation='Known RolledBack IOException is retried with the same frozen member set/current lease. One-shot fault is disarmed; storage commits. Normal common/turn completion clears the refresh at GameEngine.MortalJointStoryTurn.cs108. This is not null-refresh evidence of lost held state.',limits='Raw evidence contains the initial fault journal and later committed marker observations. It does not contain an explicit RolledBack callback/count; rollback/retry linkage is established by the actual storage control flow. The failed test did not execute the ordinary full completion assertions or compensation assertions; no new runtime qualification is inferred.')
 else:
  assert outcome['ActualTurn']==42 and mem['CanonicalDisposition']=='PublicationUncertain' and mem['CanonicalRefreshPresent'] and len(commits)==1 and commits[0]['Record']['state']=='Prepared'
  prepared=commits[0]['Record'];assert any(x['code']=='interface_updates_unknown_field' for x in prepared['issues'])
  held=one('iobs_actual_error_prepared_held');assert held['ActualTurn']==42 and held['Result']==prepared and held['MechanicalBefore']==held['MechanicalAfter']
  direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['Before']==direct['After'] and direct['ActualOuterDisposition']=='MortalJointRepairPublicationHeldBlocked' and len(direct['Before'])==222 and 'MortalJointRepairPublicationHoldException'in direct['DirectFailure']
  if scenario=='TerminalFault':
   assert 'Expected: PublicationUncertain' in message and 'Actual:   TerminalReleased' in message and 'JointInterfaceSettlement.cs:line 115' in stack and len(members)==6
   for path,m in members.items():assert journal_image(m['Before'])==raw(direct['After'].get(m['Path']))
   pending=json.loads(journal_image(members['game_state/control/pending_wound_resolutions.json']['Before']).decode('utf-8-sig'));assert pending['jointInterfacePublication']==prepared
   assert journal_image(members['game_state/control/pending_wound_resolutions.json']['After']) is None
   diagnoses[scenario]=dict(failure=message,stack=stack,faultTransaction=journal['TransactionId'],faultMembers=6,appliedMemberIndex=0,initialErrorPreparedAndHeldTurn42=True,preTerminalDirectAndOuterRestorePaths=222,preTerminalBytesUnchanged=True,observedTerminalReturn='TerminalReleased',reached=['ErrorPrepared exact issue/before-I/mtime/no-common/turn42/mechanical assertions','actual Stage direct and outer restore retention','actual terminal owner invoked with actual backup/checkpoint','non-null settlement and nonempty real member cohort','after capture executed','one injected terminal member fault'],unreached=['expected PublicationUncertain equality','post-terminal before/after key equality','post-terminal full byte equality','post-terminal pending still present','whole-terminal diagnostic emission'],classification='FIXTURE_TRANSIENT_FAULT_RETRIED_BELOW_OWNING_COMPENSATION',sourceExplanation='Initial uncommitted removal recovers to exact before-images, then one-shot IOException qualifies for storage retry. On subsequent success, the actual terminal owner checks complete cohort/original/readback and prepared claim before returning TerminalReleased. Original assertion reports that return; no owning failure reaches its compensation catch.',limits='The generic owner-outcome image snapshot precedes the explicit terminal invocation. The failed assertion prevents emission of its captured post-terminal Before/After and trace. No independent post-terminal raw full-inventory or rollback/compensation success is claimed.')
  else:
   assert u.attrib['outcome']=='Passed' and err is None and not by['iobs_actual_owning_member_fault'];settled=one('iobs_actual_whole_terminal_settlement');assert settled['Disposition']=='TerminalReleased' and settled['Faults']==0 and len(settled['Members'])==6 and len(settled['Before'])==222 and len(settled['After'])==219
   changed={str(Path(clean['CleanupOwnedRoot'])/'game_session'/x['RelativePath']):x for x in settled['Members']}
   for path,m in changed.items():
    before=None if m['Before'] is None else base64.b64decode(m['Before'],validate=True);after=None if m['After'] is None else base64.b64decode(m['After'],validate=True)
    assert before==raw(settled['Before'].get(path)) and after==raw(settled['After'].get(path))
   assert set(settled['Before'])-set(changed)==set(settled['After'])-set(changed)
   for path in set(settled['Before'])-set(changed):assert raw(settled['Before'][path])==raw(settled['After'][path])
   diagnoses[scenario]=dict(nativeOutcome='Passed',independentlyQualifiedAsNewTarget=False,observedTerminalReturn='TerminalReleased',faults=0,terminalMembers=6,beforePaths=222,afterPaths=219,unchangedNonmembersVerified=True,reason='Coherent failed settlement category is frozen; companion PASS is retained only as original diagnostic evidence, not counted independently.')
 cases.append(dict(method=u.attrib['testName'],scenario=scenario,nativeOutcome=u.attrib['outcome'],duration=u.attrib['duration'],diagnosticRows=len(rows),boundaries={k:len(v)for k,v in by.items()if v},cleanup=clean))
assert set(diagnoses)=={'CommonFault','TerminalFault','Terminal'} and len({x['cleanup']['CleanupOwnedRoot']for x in cases})==3
censuses=[]
for n,ref in enumerate(info['supplementalAuthoritativeCensuses']):
 b=frozen(ref['path']);assert h(b)==ref['sha256'] and len(b)==ref['bytes'] and b==git(C,'show',carrier+':'+ref['path']);c=json.loads(b)
 assert c['cleanBoundary'] and c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and not any(c[k]for k in ['unknown','activeTaskGameTestProcesses','ownedRoots','confirmedGone']) and len(c['entries'])==len(c['pidsEnumerated'])==[64,65][n] and all(x['status']=='MATCH'for x in c['knownVisibility'])
 for x in c['entries']:
  def identity(s):return int(s.split(' ',1)[0]),int(s.rsplit(')',1)[1].split()[19])
  assert identity(x['statBefore'])==identity(x['statAfter'])==(x['pid'],x['startTick'])
 censuses.append(dict(path=ref['path'],sha256=ref['sha256'],entries=len(c['entries']),startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],clean=True))
beforegap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();aftergap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds();assert beforegap==2.676426 and aftergap==2.914252
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-4406e723.json');changed=git(R,'diff','--name-only',actual,carrier).decode().splitlines();assert receipt['sha']==carrier and receipt['base']==actual and len(changed)==receipt['changedPathsVerified']==18 and set(changed)=={x['path']for x in receipt['files']} and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localSourceOrObjectReuse']
for f in receipt['files']:
 b=frozen(f['path']);assert b==git(C,'show',carrier+':'+f['path']) and h(b)==f['sha256'] and len(b)==f['bytes'] and blob(b)==f['gitBlob'] and git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]
assert cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and not cloud['oldCorrectedCrash']['recoveryClaim']
out=dict(kind='INDEPENDENT_SETTLEMENT3_FROZEN_FAILURE_CAUSAL_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,originalStoredRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],sourceReadback=source,nativeExit=1,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],noTimeout=True,binaries9Full1575Unchanged=True,jsonDiagnosticParseErrors=0,rawImageLengthShaChecks=rawcount,embeddedImageShaChecks=embeddedcount,journalImageShaChecks=journalchecks,cases=cases,diagnoses=diagnoses,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,cleanup='Native owned-tree/runtime cleanup and three GUID-root cleanup succeeded. Original exe-dependent arrays remain incomplete; saved v2 observations are finite dated boundaries only.',restoredCarrierPaths=18,classification='ONE_SHARED_FIXTURE_FAULT_CLASSIFICATION_ERROR_TWO_DISTINCT_OWNING_PATHS',recommendedCorrection='Change only the one-shot injected _settlementFailure from IOException to InvalidDataException, same message/phase/counters/journal/assertions. Existing storage explicitly excludes this type from retry and both owning catches already handle it. No product or retry-policy change, persistent fault, assertion weakening or fallback.',requiredGates='Fresh SOURCE plus BOTH/catalog, whole16/33/57, corrective settlement1/2/3 and remaining3/3/10 ZERO; independent PREP before a newly named corrective settlement3 actual. Retain44; no accepted-target replay. All actuals remain frozen now.',qualification='No settlement target is independently accepted; Terminal(false) PASS is diagnostic only. Old failures and old corrected crashUNKNOWN remain.',execution='Read-only source/raw/inventory/clone checks and /tmp/review artifacts only. No build/discovery/tests/observer/process intervention or repository edits.')
p=D/'t179pd-ni-iobs-l01-settlement3-failure-independent-check-4406e723-20261018.json';p.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(p),bytes=p.stat().st_size,sha256=h(p.read_bytes()),rawChecks=rawcount,journalChecks=journalchecks,classification=out['classification'])))
