#!/usr/bin/env python3
# Read-only review utility; never invokes tests, observers or process control.
from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re,argparse
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
ap=argparse.ArgumentParser(description='Read-only original EVIDENCE checker. Use only after archived carrier recovery; never admits runtime automatically.')
ap.add_argument('mode',choices=['integrity4','uncertain1','crash5']);ap.add_argument('carrier');ap.add_argument('recovery_receipt')
cli_args=ap.parse_args();mode=cli_args.mode;carrier=cli_args.carrier;compiled='3ed6a8f3c0b944777ef05f2a0095431ec63f6664'
assert re.fullmatch('[0-9a-f]{40}',carrier)
configs={
 'integrity4':('integrity4BorrowedLease',4,15,'corrective-plan','11492d33c30fb4031bbd4b3dc1ed2b55c5ffa2b700c8cf3af844f1566120f471','fr063-joint-interface-current-integrity','GuaranteedTreatment_CurrentResumeRefusesPartialSelectedIntegrityWithoutNormalizerFallback'),
 'uncertain1':('uncertain1BorrowedLease',1,10,'corrective-plan','11492d33c30fb4031bbd4b3dc1ed2b55c5ffa2b700c8cf3af844f1566120f471','fr063-joint-interface-uncertain-current','GuaranteedTreatment_NewCurrentOwnerSupersedesOnlyObsoleteSelectedUncertainDiagnostic'),
 'crash5':('crash5NewCloudBorrowedLease',5,15,'remaining-plan','4d884c2fea1153ef68e8d6d33a2d0eb3fdcee7f9939c344c606c72793c6e2326','fr063-joint-interface-crash-current','GuaranteedTreatment_ActualProcessLossReentersThroughCurrentOwnerWithoutReprojection')}
key,ncases,budget,prep_mode,prep_hash,category_name,method_name=configs[mode]
method_name='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.'+method_name
expected_label='t179pd-ni-iobs-l01-process-first-'+mode+'-borrowed-lease-cloud-813f1057' 
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals'][key];p=Path(info['recordPath']);r=read(p);actual=r['actualSource']
assert h(p.read_bytes())==info['recordSha256']
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==1 and r['mode']==mode and r['expectedCases']==ncases and r['label']==expected_label and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='b4e01729c1a4df935f79e7407e6aafe52bf9394d4f6e1ddab6f83a40893f02d5'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==git(R,'show',compiled+':'+pin['path'])==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
assert info['archive']['path']==REC+'t179pd-ni-iobs-l01-cloud-borrowed-lease-'+mode+'-813f1057-files-20261018/archive.json'
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256'] and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[1]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=ncases,Executed=ncases,Passed=0,Failed=ncases) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=ncases,CompletedCases=ncases,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==1 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert not category['Completed'] and category['BudgetMinutes']==budget and category['Seconds']<budget*60
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==budget and desc['EstimatedCases']==ncases and desc['Category']==category_name and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']==prep_hash and prep['mode']==prep_mode and prep['actualSource']==compiled and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid>0 and tick>0 and '(pwsh)' in proc['identity']['stat']
sentinel=read(r['firstSentinel']);assert sentinel['mode']==mode and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert expected=={method_name:ncases} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e in {'0','1'} for _,e in exits) and any(e=='1' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==ncases and all(u.attrib['testName'].split('(',1)[0]==method_name for u in us)
definitions={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};assert len(definitions)==ncases
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
assert len(packet['productDelta'])==22 and sorted(packet['productDelta'])==git(R,'diff','--name-only',packet['productBase'],compiled,'--','BookOfEternityClient').decode().splitlines()
assert len([x for x in r['binaryAfter']if x['path'].endswith('/BookOfEternityClient.dll') and x['sha256']=='56253ece2b2bd986dbc3217bc14fa1bc008d94010b1e88244417d84443cc1192'])==4
sourcepaths=['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceLifecycle.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerArchive.cs','BookOfEternityClient.TestSupport/NativeHostScenarioDriver.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfaceResume.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs','BookOfEternityClient/Services/MortalJointInterfacePublication.cs','BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs']
source=[]
for path in sourcepaths:
 b=frozen(path);assert b==git(R,'show',compiled+':'+path)==git(C,'show',carrier+':'+path);source.append(dict(path=path,sha256=h(b),bytes=len(b)))
crash=frozen(sourcepaths[0]).decode();resume=frozen(sourcepaths[4]).decode();upstream=frozen(sourcepaths[5]).decode();items=frozen(sourcepaths[6]).decode();logger=frozen(sourcepaths[9]).decode()
assert 'Environment.Exit(73);'in crash and 'TimeSpan.FromSeconds(220)'in crash and 'fields[19]'in crash and 'start.Environment["TMPDIR"] = Path.Combine(own, "tmp")'in crash
assert 'HasUnsettledChildren' in crash and 'OwnedFixtureRetainedForUnresolvedChild = true'in crash
assert resume.index('var lease = await _fs.AcquireCanonicalWriteLeaseAsync(); Exception? failure = null;')<resume.index('var upstream = await CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(lease);')<resume.index('current = await MortalJointInterfacePublication.CommitPublishedAsync')
assert 'await ValidateAcceptedTurnUpstreamRawItemsAsync()' in upstream and 'CollectAcceptedTurnUpstreamRawStateIssuesCoreAsync(null)' in upstream
assert 'private async Task<UpstreamAcceptedTurnValidation> ValidateAcceptedTurnUpstreamRawItemsAsync()\n    {\n        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();'in items
assert '_messages.Enqueue(message.Length <= 2000 ? message : message[..2000]);'in logger
inventory={str(R/x['path']):x for x in r['allOutputInventory']}
def deps(d):
 assert set(d['paths'])==set(d['sha256'])=={'integration','support','runtimeconfig','depsfile','immutable'}
 for k,path in d['paths'].items():assert path in inventory and d['sha256'][k]==inventory[path]['sha256'].upper()
 assert d['immutableAssemblyIdentity']=='System.Collections.Immutable, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
def loaded(rows,d):
 assert len(rows)==3 and {x['Path']for x in rows}=={d['paths'][k]for k in ['integration','support','immutable']}
 for x in rows:assert x['IsDefaultContext'] and x['LoadContext']=='Default' and x['Sha256']==inventory[x['Path']]['sha256'].upper()
plainbase64checks=0

def b64(s):
 global plainbase64checks
 if s is None:return None
 b=base64.b64decode(s,validate=True);plainbase64checks+=1;return b

def jbytes(b):return json.loads(b.decode('utf-8-sig'))
def lines(s):return [json.loads(l)for l in s.splitlines()if l.startswith('{')]
def snap(mapping):return {k:b64(v)for k,v in mapping.items()}
def digest_snap(mapping):return h(json.dumps({k:None if v is None else h(v)for k,v in sorted(mapping.items())},sort_keys=True,separators=(',',':')).encode())

REASON='joint_interface_actual_publication_preimage_or_readback_changed'
TERMINAL=['game_state/wounds/wound_commands.json','game_state/control/pending_wound_resolutions.json','output/mortal_wound_decisions.json','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','input/turn_request.json','ready/turn_complete.json']
def boundaries(rows):
 out=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary' in x:out[x['Boundary']].append(x.get('Observation',x))
 return out
def unique(by,name):
 assert len(by[name])==1,(name,len(by[name]));return by[name][0]
def actual_mechanics(rows,binding):
 by=boundaries(rows);m=unique(by,'iobs_actual_resumed_once_mechanics')
 spend=m['spend'];damage=m['damage']
 assert spend['Turn']==damage['Turn']==43 and spend['RequestedAmount']==spend['AppliedAmount']==2
 assert spend['BeforeState']['Current']==2 and spend['AfterState']['Current']==0
 assert damage['AppliedAmount']==3 and damage['BeforeState']['Current']==5 and damage['AfterState']['Current']==2
 suffix=m['ExactSuffix'];assert len(suffix)==2 and [x['Kind']for x in suffix]==['create','treat'] and all(x['Turn']==43 for x in suffix)
 assert suffix[1]['Ordinal']==suffix[0]['Ordinal']+1 and m['RequestFingerprint']==binding['requestFingerprint']
 return dict(energy=[2,0],health=[5,2],targetTurn=43,woundSuffix=['create','treat'],requestFingerprint=m['RequestFingerprint'])
def actual_settled(rows):
 x=unique(boundaries(rows),'iobs_actual_current_common_claim_settled')
 assert x['ReservationAbsent'] and x['OpenTakeAbsent'];return x
cases=[];allident=[];roots_seen=[];nonce_seen=[];parents=set();class_roots=[]
assert mode=='uncertain1' and carrier=='f390eb0602a99f5a2387ab7dc6ef52571e856b26'
u=us[0];assert u.attrib['outcome']=='Failed' and u.attrib['testName']=='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_NewCurrentOwnerSupersedesOnlyObsoleteSelectedUncertainDiagnostic'
error=u.find('t:Output/t:ErrorInfo',ns);message=error.find('t:Message',ns).text;stack=error.find('t:StackTrace',ns).text
assert message=='Assert.NotNull() Failure: Value is null' and 'JointInterfaceUncertainResume.cs:line 94'in stack
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

returned=one('iobs_same_engine_uncertain_new_call_returned');observed=one('iobs_same_engine_uncertain_new_call_raw_images')
assert returned['Handled'] is True and returned['Turn']==42 and returned['CurrentProof']is None and returned['CaptureCallbacks']==1 and returned['Disposition']=='PublicationUncertain'
assert returned['OldAttempt']==dict(Live=False,Delegated=False,ContinuationAbsent=True,ReservationAbsent=True)
assert len(returned['Issues'])==1 and returned['Issues'][0]['Message']==issue['Message']['Value'] and returned['Issues'][0]['Code']==issue['Code']['Value']
assert returned['Trace']==pubs and len(returned['Trace'])==1 and returned['Trace'][0]['InterfaceChanged'] and not returned['Trace'][0]['MarkerCommitted']
reason='Joint story/turn completion held: The joint completion marker is missing.; original, ready, outputs, backups and committed union retained.'
assert returned['CurrentDiagnostics']==outcome['EngineDiagnostics']+'\n'+reason
assert 'canonical-write.lock'not in returned['CurrentDiagnostics'] and 'joint_interface_actual_publication_preimage_or_readback_changed'not in returned['CurrentDiagnostics']
def observed_bytes(x):
 assert x['ReadError']is None and not x['Absent'] and x['Bytes']is not None;return b64(x['Bytes'])
pending_after=observed_bytes(observed['Pending']);interface_after=observed_bytes(observed['Interface'])
assert pending_after==raw(direct['Before'][full('game_state/control/pending_wound_resolutions.json')])
assert interface_after==raw(commits[1]['Interface'])==raw(direct['Before'][full('output/interface_updates.json')])
assert set(observed['Mechanical'])==set(mechanical)
for path,x in observed['Mechanical'].items():assert observed_bytes(x)==raw(mechanical[path])
ready=jbytes(raw(direct['Before'][full('ready/turn_complete.json')]))
assert ready['sessionId']==published['binding']['sessionId'] and ready['requestId']==published['binding']['requestId'] and ready['turnNumber']==43 and ready['status']=='success'
assert full('ready/turn_error.json')not in direct['Before'] and full('game_state/wounds/mortal_qte_decision_publication.json')not in direct['Before']
for path in ['BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs','BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs','BookOfEternityClient/Services/MortalQteDecisionPublication.cs']:
 b=frozen(path);assert b==git(R,'show',actual+':'+path)==git(R,'show',compiled+':'+path)==git(C,'show',carrier+':'+path);source.append(dict(path=path,sha256=h(b),bytes=len(b),pinned=path in {p['path']for p in packet['sourcePins']}))
story=frozen('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs').decode();late=frozen('BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs').decode()
assert 'if (bytes is null) return claimed ? HoldMortalJointStoryTurn("The joint completion marker is missing.") : null;'in story
assert story.index('CurrentPublicationState.IntegrityConflict')<story.index('The joint completion marker is missing.')<story.index('var validated = await ValidateAcceptedTurnOutcomeWithRepairLoopAsync')
late=late[late.index('private async Task<bool> ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync()'):]
assert late.index('TryReconcileMortalJointStoryTurnAsync(')<late.index('ResolveConcurrentActiveTerminalSignalsAsync(')<late.index('ValidateAcceptedTurnOutcomeWithRepairLoopAsync(')
assert 'MortalJointRepairPublicationDisposition.PublicationUncertain);'in story
scopes={'reached':['initial exact PublicationUncertain issue after one genuine completed capture and Published I','expired old attempt with no live/delegated/continuation/reservation','held turn42, exact pending Published I, all15 initial mechanical images','direct actual restore refusal and exact222 before/after images','new real late call returned handled=true','new post-call logger, typed diagnostic/scalar and pending/I/15mechanical snapshots emitted without read error','post-call turn42, proofnull, captureCallbacks1, old diagnostic retained, pending/I/15mechanical bytes unchanged','native and owned-root cleanup'], 'unreached':['selected current resume dispatch after the early story hold','second actual capture and new common plan/take/publication','non-null current full DATA proof and turn43 positive assertions','post-resume mechanical effect assertions','seven terminal cleanup assertions','positive after-state and inert repeat','newcrash5']}
cases=[dict(name=u.attrib['testName'],duration=u.attrib['duration'],outcome='Failed',error=message,stack=stack,currentPostCall=returned,postCallImages=dict(pendingSha256=h(pending_after),interfaceSha256=h(interface_after),mechanicalPaths=15,allEqualToInitialRetainedImages=True,readErrors=0),correlatedReady=ready,assertionScope=scopes)]
# Dated v2 snapshots only.
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
assert len(censuses)==2
beforegap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds()
aftergap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds()
assert beforegap>=0 and aftergap>=0
rp=Path(cli_args.recovery_receipt);rc=read(rp)
assert rc['sha']==carrier and rc['remoteShaVerified'] and rc['clean'] and not rc['localSourceOrObjectReuse']
assert len(rc['files'])==rc['changedPathsVerified'] and set(git(R,'diff','--name-only',rc['base'],carrier).decode().splitlines())=={x['path']for x in rc['files']}
assert git(R,'ls-tree','-r',carrier)==git(C,'ls-tree','-r',carrier)
for x in rc['files']:
 b=frozen(x['path']);assert b==git(C,'show',carrier+':'+x['path']) and h(b)==x['sha256'] and len(b)==x['bytes'] and blob(b)==x['gitBlob']
 assert git(C,'ls-tree',carrier,'--',x['path']).split(b'\t')[0].decode().split()==[x['mode'],'blob',x['gitBlob']]
baseline=json.loads(git(R,'show','52a163d0d06b7d1b762d0a5f5a7d81168eb2d728:'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
assert cloud['oldCorrectedCrash']==baseline['oldCorrectedCrash'] and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability']=='UNAVAILABLE'
for name,prior in baseline['actuals'].items():
 for field in ['recordPath','recordSha256','archive']:
  if field in prior:assert cloud['actuals'][name][field]==prior[field]
assert cloud['holds']==baseline['holds'] and cloud['qualifiedCases']['totalDistinctTargets']>=47

assert cloud['qualifiedCases']['totalDistinctTargets']==51
findings=[dict(id='L01-UNCERTAIN-STORY-PREEMPT-001',severity='blocking',classification='PRODUCT_DISPATCH_PREEMPTS_SELECTED_CURRENT_OWNER',fact='Same-frame retained PublicationUncertain marks story completion claimed before common exists. Absent marker causes an early HeldBlocked result, preventing the selected current-resume route from executing.',directEvidence='New post-call logger adds exactly the missing-completion-marker hold; turn42/proofnull/capture1 and unchanged pending/I/mechanics agree with that path.',oldRunLimit='No old missing post-call diagnostics are reconstructed or uniquely attributed by this new observation.')]
out=dict(kind='INDEPENDENT_BORROWED_LEASE_UNCERTAIN1_ORIGINAL_FAILURE_AND_CAUSAL_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,storedRawRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],productDeltaPaths=22,nativeExit=1,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],nineAndFull1575OutputsExact=True,sourceReadback=source,cases=cases,jsonDiagnosticParseErrors=0,rawImageShaChecks=rawcount,journalImageShaChecks=journalchecks,embeddedImageShaChecks=embeddedcount,plainBase64Decodes=plainbase64checks,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=rc['changedPathsVerified'],bytesHashesBlobsModesVerified=True,completeSavedGitTreeMatchesIndependentClone=True),qualifiedCases=51,additionalQualifiedTargets=0,findings=findings,oldCorrectedCrash='UNKNOWN_UNAVAILABLE',newCrash5='UNRUN_HELD',execution='Read-only original/source/Git/archive/build-output/saved-census inspection only; no native/build/discovery/controller/observer execution, current fixture-root or process inspection/control, repo writes or retry.')
q=D/'t179pd-ni-iobs-l01-borrowed-lease-uncertain1-failure-independent-check-f390eb06-20261018.json';q.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(dict(path=str(q),sha256=h(q.read_bytes()),findings=[x['id']for x in findings])))
