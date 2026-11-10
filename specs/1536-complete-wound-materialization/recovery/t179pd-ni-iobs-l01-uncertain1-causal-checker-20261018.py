from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='4dbdb144c1862941f48df53136ae78cbf4a8f25c';actual='90fbb068817257a670c0ac99f6d1bbfd6cbfc19b';compiled='7dbfbddc74b9fe55fcf80bb08d85bf26d245b38c'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['uncertain1'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='dd4eed9571317b93eea673c5c36855afb7c00d1084ba608c6ace5a2c7a3598be'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==1 and r['mode']=='uncertain1' and r['expectedCases']==1 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='304a7e8beb3174246ac3db792c6aa63319fa8ead9f3bab62f91346d4f204248d'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==git(R,'show',compiled+':'+pin['path'])==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='849e8ec81489c66948b6837bad04f92bead79b1decef8b1229315815883acd6e' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[1]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=1,Executed=1,Passed=0,Failed=1) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=1,CompletedCases=1,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==1 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert not category['Completed'] and category['BudgetMinutes']==10 and category['Seconds']<600
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==10 and desc['EstimatedCases']==1 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='2536796c17257853b72940736ad92763443c7727597e9915a32f28ae9b87fa5f' and prep['mode']=='remaining-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==31474 and tick==1452180 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline']==''
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='uncertain1' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert len(expected)==1 and set(expected.values())=={1} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e in {'0','1'}for _,e in exits) and any(e=='1'for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==1 and {u.attrib['testName'].split('(fault:')[0] for u in us}==set(expected)
definitions={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};assert len(definitions)==1
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
assert len(packet['productDelta'])==21 and sorted(packet['productDelta'])==git(R,'diff','--name-only',packet['productBase'],compiled,'--','BookOfEternityClient').decode().splitlines()
u=us[0];assert u.attrib['outcome']=='Failed' and u.attrib['testName']=='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_NewCurrentOwnerSupersedesOnlyObsoleteSelectedUncertainDiagnostic'
error=u.find('t:Output/t:ErrorInfo',ns);message=error.find('t:Message',ns).text;stack=error.find('t:StackTrace',ns).text
assert message=='Assert.NotNull() Failure: Value is null' and 'JointInterfaceUncertainResume.cs:line 67'in stack
rows=[json.loads(l)for l in u.find('t:Output/t:StdOut',ns).text.splitlines()if l.startswith('{')];by=collections.defaultdict(list)
for x in rows:
 walk(x)
 if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
pre=one('fr063_joint_completion_actual_prerequisites');assert pre['Source']['TurnNumber']==42 and pre['Original']['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5
outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['ActualTurn']==42 and outcome['entered'] and outcome['liveCallerReturned'] and outcome['liveWaitEntered'] and outcome['jointOriginalAuthored'] and outcome['ActualCaller']=='ProcessPlayerTurn' and outcome['Failure']is None and outcome['ObserverFailure']is None
mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem');assert mem['CanonicalDisposition']=='PublicationUncertain' and mem['IssueCount']==mem['ReportedIssueCount']==1 and not mem['IssueRowsTruncated']
issue=mem['Issues'][0];assert issue['Code']['Value']=='mortal_joint_repair_admission_refused' and issue['Message']['Value']=='L01 one-shot existing completed capture checkpoint; preparation not entered.'
for k in ['Code','FilePath','Message']:
 assert not issue[k]['Truncated'] and h(issue[k]['Value'].encode()).upper()==issue[k]['FullUtf8Sha256']
assert issue['Message']['Value']in outcome['EngineDiagnostics'] and 'canonical-write.lock'not in outcome['EngineDiagnostics']
commits=by['iobs_actual_result_commit'];assert [x['Record']['state']for x in commits]==['Prepared','Published'];published=commits[1]['Record'];assert not published['issues']
pubs=by['iobs_actual_committed_member'];assert len(pubs)==1 and pubs[0]['InterfaceChanged'] and not pubs[0]['MarkerCommitted']
capture=one('iobs_actual_completed_capture');assert capture['Callback']==capture['InterfaceCommittedCohorts']==1 and raw(capture['InterfaceRaw'])==raw(commits[1]['Interface'])==base64.b64decode(published['afterImage']['base64'])
direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['scenario']=='CutResume' and direct['ActualOuterDisposition']is None and 'MortalJointRepairPublicationHoldException'in direct['DirectFailure'] and direct['Before']==direct['After'] and len(direct['Before'])==222
clean=next(x for x in rows if 'CleanupOwnedRoot'in x);assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure']is None and str(Path(clean['CleanupOwnedRoot']).parent)==cloud['environment']['root']
full=lambda rel:str(Path(clean['CleanupOwnedRoot'])/'game_session'/rel)
mechanical=one('iobs_actual_public_correction_before_ready')['Mechanical'];assert len(mechanical)==15
for path,b in mechanical.items():assert raw(b)==raw(direct['Before'].get(full(path)))
for path,b in direct['Before'].items():assert raw(b)==raw(direct['After'][path]) and Path(path).is_relative_to(clean['CleanupOwnedRoot'])
pending=json.loads(raw(direct['Before'][full('game_state/control/pending_wound_resolutions.json')]).decode('utf-8-sig'));assert pending['jointInterfacePublication']==published
assert raw(direct['Before'][full('output/interface_updates.json')])==raw(commits[1]['Interface']) and full('game_state/wounds/mortal_qte_decision_publication.json')not in direct['Before']
assert not by['iobs_same_engine_new_current_fullproof_before_cleanup'] and not by['iobs_same_engine_uncertain_diagnostic_current_resume'] and not by['fr063_joint_complete_union'] and not by['fr063_joint_completion_inert_repeat']
sourcepaths=['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceLifecycle.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCloseRetention.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfaceResume.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs']
source=[]
for path in sourcepaths:
 b=frozen(path);assert b==git(R,'show',compiled+':'+path)==git(C,'show',carrier+':'+path);source.append(dict(path=path,sha256=h(b),bytes=len(b)))
fixture=frozen(sourcepaths[0]).decode();assert fixture.splitlines()[66].strip().startswith('Assert.NotNull(_currentResumedProof);')
assert fixture.index('var handled = await InvokePrivateAsync<bool>(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");')<fixture.index('Assert.NotNull(_currentResumedProof); Assert.Equal(43,')
assert fixture.index('Assert.False(oldAttempt.Live); Assert.False(oldAttempt.Delegated);')<fixture.index('var handled = await') and fixture.index('Assert.Null(oldAttempt.Continuation); Assert.Null(oldAttempt.Reservation);')<fixture.index('var handled = await')
assert 'await AssertActualRetainingOwnersAsync(engine, invokeWrapper: false);'in fixture
# Census/readback copied from the completed independent integrity checker, bound to this frozen original.

censuses=[]
for ref in info['supplementalAuthoritativeCensuses']:
 b=frozen(ref['path']);assert b==git(C,'show',carrier+':'+ref['path']) and h(b)==ref['sha256'] and len(b)==ref['bytes'];c=json.loads(b)
 assert c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and c['fixtureTemp']==cloud['environment']['root']
 assert c['cleanBoundary'] and not any(c[k]for k in ['unknown','activeTaskGameTestProcesses','ownedRoots','confirmedGone'])
 assert len(c['entries'])==len(c['pidsEnumerated']) and {x['pid']for x in c['entries']}==set(c['pidsEnumerated'])
 for x in c['entries']:
  def ids(raw):
   f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick']) and ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv'in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
 assert all(x['status']=='MATCH'for x in c['knownVisibility'])
 censuses.append(dict(path=ref['path'],sha256=h(b),entries=len(c['entries']),startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],clean=True,knownVisibility=c['knownVisibility']))
assert [x['entries']for x in censuses]==[85,87]
beforegap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();aftergap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds();assert beforegap>=0 and aftergap==0.785627
rp=Path('/tmp/1536-cloud-tools/carrier-recovery-4dbdb144.json');rc=read(rp);assert rc['sha']==carrier and rc['base']==actual and rc['changedPathsVerified']==18 and rc['remoteShaVerified'] and rc['clean'] and not rc['localSourceOrObjectReuse']
assert set(git(R,'diff','--name-only',actual,carrier).decode().splitlines())=={x['path']for x in rc['files']} and git(R,'ls-tree','-r',carrier)==git(C,'ls-tree','-r',carrier)
for x in rc['files']:
 b=frozen(x['path']);assert b==git(C,'show',carrier+':'+x['path']) and h(b)==x['sha256'] and len(b)==x['bytes'] and blob(b)==x['gitBlob']
 assert git(C,'ls-tree',carrier,'--',x['path']).split(b'\t')[0].decode().split()==[x['mode'],'blob',x['gitBlob']]
assert cloud['qualifiedCases']['totalDistinctTargets']==47 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability']=='UNAVAILABLE'
oldcloud=json.loads(git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for key,val in oldcloud['actuals'].items():
 current=dict(cloud['actuals'][key]);prior=dict(val)
 if key=='integrity4':
  assert current.pop('status')=='ORIGINAL_NATIVE_4PASS_IMMUTABLE_INDEPENDENT_EVIDENCE_HELD_CURRENT_INTERFACE_LOCK_VACUITY'
  assert prior.pop('status')=='ORIGINAL_NATIVE_PASS_INDEPENDENT_EVIDENCE_PENDING'
  assert 'two canonical-write.lock IOExceptions' in current.pop('materialReviewConcern')
 assert current==prior

assert cloud['oldCorrectedCrash']==oldcloud['oldCorrectedCrash'] and cloud['holds']==oldcloud['holds']
scopes=dict(reached=['genuine confirmed held42/natural original43 without precomposed common plan','one completed capture after one I-changing publication','initial PublicationUncertain with exact original injected capture-failure issue','expired old attempt: not Live or Delegated, Continuation and Reservation null (unchanged assertions before failing line)','exact Published pending/I, no common marker and held42','all15 mechanical paths unchanged','actual Stage direct restore blocked with all222 captured paths byte-identical','real late caller awaited and returned before failing current-proof assertion','native/fixture cleanup'],unreachedOrUnproven=['non-null currentResumedProof and successful pre-cleanup full DATA checkpoint','subsequent turn43 and exact resumed-result assertions','current resumed mechanical outcome energy/health/wound checks','terminal seven-artifact cleanup assertions','new after-current full image capture and inert-repeat assertions','post-resume logger snapshot or direct raw nested-lock exception for this case'])
result=dict(kind='INDEPENDENT_UNCERTAIN1_FROZEN_POSITIVE_RESUME_FAILURE_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,storedRawRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],productDeltaPaths=21,nativeExit=1,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],noTimeout=True,nineAndFull1575OutputsExact=True,testName=u.attrib['testName'],testDuration=u.attrib['duration'],error=message,stack=stack,initialHeldState=dict(disposition='PublicationUncertain',issueProjectionSha256=mem['CompletePublicIssueProjectionSha256'],issue=issue,interfaceChangingCohorts=1,captureCallbacks=1,commonMarkerCommits=0,mechanicalPathsRetained=15,directRestoreImagePathsRetained=222),assertionScope=scopes,sourceReadback=source,rawImageShaChecks=rawcount,embeddedImageShaChecks=embeddedcount,journalImageShaChecks=journalchecks,jsonDiagnosticParseErrors=0,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=18,bytesHashesBlobsModesVerified=True,completeSavedGitTreeMatchesIndependentClone=True),qualifiedCases=47,additionalQualifiedTargets=0,causalLimit='Direct raw evidence proves the positive current-resume proof was not reached. The shared selected-resume source has the independently demonstrated nested canonical-lease blocker from integrity CurrentInterface. The test fails before its final diagnostic emission; no post-current logger or full snapshot is available here, so do not claim this uncertain1 raw directly contains the lock exception or reconstruct its final state.',findings=[dict(id='L01-CURRENT-RESUME-POSITIVE-001',severity='blocking',fact='Real same-engine late owner returns without the required new current full proof; unchanged Assert.NotNull fails at line67.',relation='Consistent with and source-dependent on the same nested canonical-lease path; independent CurrentInterface raw supplies direct lock-failure evidence.')],execution='Read-only original/source/Git/archive/build-output/saved-census checks. No native/build/discovery/controller/observer execution, current fixture root/process inspection/control, repository edit or retry.')
q=D/'t179pd-ni-iobs-l01-uncertain1-failure-independent-check-4dbdb144-20261018.json';q.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps(dict(path=str(q),bytes=q.stat().st_size,sha256=h(q.read_bytes()),beforeGap=beforegap,findings=result['findings'])))
