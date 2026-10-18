#!/usr/bin/env python3
# Read-only review utility; never invokes tests, observers or process control.
from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re,argparse
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
ap=argparse.ArgumentParser(description='Read-only original EVIDENCE checker. Use only after archived carrier recovery; never admits runtime automatically.')
ap.add_argument('mode',choices=['integrity4','uncertain1','crash5']);ap.add_argument('carrier');ap.add_argument('recovery_receipt')
args=ap.parse_args();mode=args.mode;carrier=args.carrier;compiled='3ed6a8f3c0b944777ef05f2a0095431ec63f6664'
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
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['mode']==mode and r['expectedCases']==ncases and r['label']==expected_label and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
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
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256'] and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=ncases,Executed=ncases,Passed=ncases,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=ncases,CompletedCases=ncases,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert category['Completed'] and category['BudgetMinutes']==budget and category['Seconds']<budget*60
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
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e=='0' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
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
if mode=='integrity4':
 faultpaths={'Version':'game_state/control/pending_wound_resolutions.json','RecordFingerprint':'game_state/control/pending_wound_resolutions.json','OriginalManifest':'game_state/control/pending_turn_snapshot.json','CurrentInterface':'output/interface_updates.json'}
 for u in us:
  assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns)is None
  definition=definitions[u.attrib['testId']];method=definition['className'].split(',')[0]+'.'+definition['name'];assert method in expected and u.attrib['testName'].startswith(method+'(fault: ')
  rows=lines(u.find('t:Output/t:StdOut',ns).text);by=collections.defaultdict(list)
  for x in rows:
   walk(x)
   if 'Boundary'in x:by[x['Boundary']].append(x.get('Observation',x))
  def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
  outcome=one('iobs_actual_current_resume_exit');original=one('iobs_actual_abrupt_original_exit');assert original==outcome['Original'];resumed=outcome['Resume'];cut=outcome['Cut'];result=json.loads(outcome['ResultRaw']);fault=result['Fault'];assert fault in faultpaths and f'(fault: "{fault}")'in u.attrib['testName']
  rawcut=one('iobs_actual_abrupt_cut_raw_before_identity');cutbytes=b64(rawcut['Base64']);assert len(cutbytes)==rawcut['Bytes'] and h(cutbytes).upper()==rawcut['Sha256'] and jbytes(cutbytes)==cut
  walk(cut);walk(result)
  own=outcome['OwnedContainer'];root=cut['Root'];aux=result['AuxiliaryRoot'];roots_seen.append(own);nonce_seen.append(cut['Nonce']);parents.add(cut['ParentPid'])
  assert str(Path(own).parent)==cloud['environment']['root'] and re.fullmatch('boe-joint-interface-crash-[0-9a-f]{32}',Path(own).name)
  assert Path(root).parent==Path(aux).parent==Path(own)/'tmp' and root!=aux and result['Root']==root
  assert outcome['ActualContainedRoots']==[root] and re.fullmatch('[0-9a-f]{32}',cut['Nonce']) and result['Nonce']==cut['Nonce']
  assert len(by['iobs_actual_native_join'])==2
  for child,join,role,expected_exit,selfrow in zip([original,resumed],by['iobs_actual_native_join'],['engine-joint-interface-original-crash','engine-joint-interface-resume'],[73,0],[cut,result]):
   assert child['ExitCode']==join['ExitCode']==expected_exit and child['Eof']and child['Joined']and join['Eof']and join['Joined']
   assert join['Failure']is None and join['CleanupFailure']is None and join['RetainedOwnedContainer']is None
   assert child['Pid']==join['Id']==selfrow['Pid'] and child['BirthTick']==join['BirthTick']==selfrow['BirthTick'] and child['BirthTick']>0
   assert child['BirthUtc']==join['BirthUtc'] and child['Stdout']==join['RawStdout'] and child['Stderr']==join['RawStderr']==''
   assert child['Arguments']==join['Arguments'] and child['Executable']==join['Executable']=='/tmp/1536-cloud-tools/dotnet/dotnet'
   args=child['Arguments'];d=child['Dependencies'];deps(d);assert d==join['Dependencies']==selfrow['Dependencies'];loaded(selfrow['Loaded'],d)
   assert args==['exec','--runtimeconfig',d['paths']['runtimeconfig'],'--depsfile',d['paths']['depsfile'],d['paths']['support'],role,d['paths']['integration'],own if expected_exit==73 else root,str(Path(own)/('cut.json'if expected_exit==73 else'resume.json')),'0',own]
   allident.append((child['Pid'],child['BirthTick']))
  assert original['Pid']!=resumed['Pid'] and cut['ParentPid']not in {original['Pid'],resumed['Pid']} and original['BirthTick']<resumed['BirthTick']
  assert cut['Cut']==0 and cut['Phase']=='Committed' and cut['Index']==-1 and cut['Turn']==42 and cut['CommonCallbacks']==0 and cut['FirstMarker']is None
  journal=jbytes(b64(cut['JournalBase64']));walk(journal);assert journal['Committed'] and len(journal['Members'])==1 and journal['GenerationBefore']==journal['GenerationAfter']
  member=journal['Members'][0];assert member['Path']==str(Path(root)/'game_session'/'game_state/control/pending_wound_resolutions.json') and journal_image(member['After'])==b64(cut['ActualPending'])==b64(cut['PendingMemberAfter'])
  pending=jbytes(b64(cut['ActualPending']));prepared=cut['Result'];checkpoint=cut['Checkpoint'];assert pending['jointInterfacePublication']==prepared and pending['jointRepairCheckpoint']==checkpoint and checkpoint['schemaVersion']==2
  assert prepared['state']=='Prepared' and not prepared['issues'] and prepared['normalization']=='Changed' and b64(prepared['beforeImage']['base64'])==b64(cut['Interface']) and b64(prepared['afterImage']['base64'])!=b64(cut['Interface'])
  assert len(cut['MechanicalBefore'])==15 and cut['MechanicalBefore']==cut['MechanicalAtCut']
  prodrows=lines(original['Stdout']);assert not any('CleanupOwnedRoot'in z for z in prodrows)
  prereq=next(z['Observation']for z in prodrows if z.get('Boundary')=='fr063_joint_completion_actual_prerequisites');assert prereq['Source']['TurnNumber']==42 and prereq['Original']['TurnNumber']==43 and prereq['ConfirmedHeldBeforeEntry'] and prereq['NoPrecomposedCommonPlan'] and prereq['EnergyBefore']==2 and prereq['HealthBefore']==5
  for z in prodrows:walk(z)
  resumelines=lines(resumed['Stdout']);mutations=[z for z in resumelines if z.get('Boundary')=='iobs_actual_current_resume_integrity_fault'];assert len(mutations)==1;mut=mutations[0]
  assert mut['fault']==fault and mut['Path']==faultpaths[fault] and mut['GenuinePrepared']==prepared==result['Result']
  bf,afault=b64(mut['Before']),b64(mut['After']);assert bf!=afault
  if fault in {'Version','RecordFingerprint'}:
   old=jbytes(bf);new=jbytes(afault);assert old==pending
   expected_damaged=json.loads(json.dumps(old))
   if fault=='Version':expected_damaged['jointRepairCheckpoint']['schemaVersion']=7
   else:expected_damaged['jointInterfacePublication']['fingerprint']='0'*64
   assert new==expected_damaged and b64(result['RecoveredPending'])==afault
  else:
   assert afault==bf+b' ' and b64(result['RecoveredPending'])==b64(cut['ActualPending'])
   if fault=='OriginalManifest':assert h(bf).upper()==checkpoint['originalManifestHash'] and h(afault).upper()!=checkpoint['originalManifestHash']
   else:assert bf==b64(cut['Interface'])==b64(result['RecoveredI'])
  before,after,repeated=[snap(result[k])for k in ['Before','After','Repeated']];assert len(before)==223 and before==after==repeated and before[mut['Path']]==afault
  assert before['game_state/control/pending_wound_resolutions.json']==b64(result['RecoveredPending'])
  for path,encoded in cut['MechanicalBefore'].items():assert before.get(path)==b64(encoded)
  assert before.get('game_state/wounds/mortal_qte_decision_publication.json')is None
  assert result['ActualCallerEntered'] and result['Handled'] and result['Second'] and not result['Completed'] and result['Held'] and result['Failure']is None and result['NewChangingICommits']==0 and result['InertRepeatBytesEqual'] and result['FreshAuthorityFieldsAbsentBeforeEntry'] and not result['Publications']
  assert result['Recovery']==[dict(Phase='CleanupMember',Index=0),dict(Phase='CleanupComplete',Index=-1)]
  if fault=='Version':assert result['Logs']=='Engine diagnostics:\n';target='Malformed checkpoint schema7 is held at Parse before result admission; no frozen-error diagnostic contract applies.'
  elif fault=='RecordFingerprint':assert result['Logs'].count('joint_interface_required_fingerprint')==2;target='The all-zero record fingerprint is rejected by RequireFingerprint on each entry before original/canonical admission.'
  elif fault=='OriginalManifest':assert result['Logs'].count('mortal_joint_checkpoint_provenance_changed')==2;target='Signed-original byte provenance differs after exactly one appended space; current rejected-capture reconstruction refuses it on both entries.'
  else:
   guards=[z for z in resumelines if z.get('Boundary')=='iobs_actual_current_interface_guard_entry']
   assert len(guards)==2 and result['IntegrityGuardEntries']==guards and [z['Entry']for z in guards]==[1,2]
   for entry in guards:
    assert entry['Reason']==REASON and entry['CountBefore']==entry['LogsBefore'].count(REASON) and entry['CountAfter']==entry['LogsAfter'].count(REASON)
    assert entry['CountAfter']>entry['CountBefore']
    assert 'canonical-write.lock' not in entry['LogsAfter']
   assert guards[1]['LogsBefore']==guards[0]['LogsAfter'] and result['Logs']==guards[1]['LogsAfter']
   target='Both real current calls independently newly emit the exact actual-publication preimage/readback-change refusal; snapshots and occurrence deltas agree.'
  if fault!='CurrentInterface':assert result['IntegrityGuardEntries']==[]
  cleanups=[z for z in rows if 'CleanupOwnedRoot'in z];auxclean=[z for z in resumelines if 'CleanupOwnedRoot'in z];assert len(cleanups)==2 and len(auxclean)==1
  for z in cleanups+auxclean:assert z['OwnedFixtureRemoved'] and z['CleanupFailure']is None and not z.get('OwnedFixtureRetainedForUnresolvedChild',False)
  assert auxclean[0]['CleanupOwnedRoot']==aux and own in {z['CleanupOwnedRoot']for z in cleanups}
  classroot=next(z['CleanupOwnedRoot']for z in cleanups if z['CleanupOwnedRoot']!=own);assert str(Path(classroot).parent)==cloud['environment']['root'];class_roots.append(classroot)
  cases.append(dict(fault=fault,name=u.attrib['testName'],duration=u.attrib['duration'],nativeOutcome='Passed',producer=dict(pid=original['Pid'],birthTick=original['BirthTick'],exit=73),resumer=dict(pid=resumed['Pid'],birthTick=resumed['BirthTick'],exit=0),parentTestHostPid=cut['ParentPid'],ownedContainer=own,nonce=cut['Nonce'],cutTransaction=journal['TransactionId'],cut='Prepared committed before I publication',journalMembers=1,completePhysicalImagesCompared=223,beforeAfterRepeatedDigest=digest_snap(before),mechanicalImagesUnchanged=15,physicalFaultPath=mut['Path'],faultBeforeSha256=h(bf),faultAfterSha256=h(afault),freshAuthorityAbsent=True,actualCallerAndSecondEntryHeld=True,changingI=0,commonPublications=0,parentOwnedCleanups=2,childAuxiliaryCleanup=True,dependenciesAndLoadedHashesExact=True,targetReach=target,logs=result['Logs'],targetIntegrityQualified=True,guardEntries=result['IntegrityGuardEntries']))
 assert len(set(allident))==len({x[0]for x in allident})==8 and len(set(roots_seen))==len(set(nonce_seen))==len(set(class_roots))==4 and len(parents)==1
 assert {x['fault']for x in cases}==set(faultpaths)
if mode=='uncertain1':
 u=us[0];assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns)is None and u.attrib['testName']==method_name
 rows=lines(u.find('t:Output/t:StdOut',ns).text);by=boundaries(rows)
 def one(k):return unique(by,k)
 pre=one('fr063_joint_completion_actual_prerequisites');assert pre['Source']['TurnNumber']==42 and pre['Original']['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['ActualTurn']==42 and outcome['entered'] and outcome['liveCallerReturned'] and outcome['liveWaitEntered'] and outcome['jointOriginalAuthored'] and outcome['ActualCaller']=='ProcessPlayerTurn' and outcome['Failure']is None and outcome['ObserverFailure']is None
 mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem');assert mem['CanonicalDisposition']=='PublicationUncertain' and mem['IssueCount']==mem['ReportedIssueCount']==1 and not mem['IssueRowsTruncated']
 issue=mem['Issues'][0];assert issue['Code']['Value']=='mortal_joint_repair_admission_refused' and issue['Message']['Value']=='L01 one-shot existing completed capture checkpoint; preparation not entered.'
 for k in ['Code','FilePath','Message']:assert not issue[k]['Truncated'] and h(issue[k]['Value'].encode()).upper()==issue[k]['FullUtf8Sha256']
 commits=by['iobs_actual_result_commit'];assert [x['Record']['state']for x in commits]==['Prepared','Published'];published=commits[1]['Record'];assert not published['issues']
 direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['scenario']=='CutResume' and direct['ActualOuterDisposition']is None and 'MortalJointRepairPublicationHoldException'in direct['DirectFailure'] and direct['Before']==direct['After'] and len(direct['Before'])==222
 cleanups=[x for x in rows if 'CleanupOwnedRoot'in x];assert len(cleanups)==1
 clean=cleanups[0];root=clean['CleanupOwnedRoot'];assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure']is None and str(Path(root).parent)==cloud['environment']['root']
 full=lambda rel:str(Path(root)/'game_session'/rel)
 mechanical=one('iobs_actual_public_correction_before_ready')['Mechanical'];assert len(mechanical)==15
 for path,b in mechanical.items():assert raw(b)==raw(direct['Before'].get(full(path)))
 for path,b in direct['Before'].items():assert raw(b)==raw(direct['After'][path]) and Path(path).is_relative_to(root)
 assert jbytes(raw(direct['Before'][full('game_state/control/pending_wound_resolutions.json')]))['jointInterfacePublication']==published
 assert raw(direct['Before'][full('output/interface_updates.json')])==raw(commits[1]['Interface'])==b64(published['afterImage']['base64'])
 proof=one('iobs_same_engine_new_current_fullproof_before_cleanup');assert proof['Original']['TurnNumber']==43 and proof['Result']==published
 returned=one('iobs_same_engine_uncertain_new_call_returned');assert returned['Turn']==43 and returned['CurrentProof']==published and returned['CaptureCallbacks']==2
 assert returned['OldAttempt']==dict(Live=False,Delegated=False,ContinuationAbsent=True,ReservationAbsent=True)
 assert 'canonical-write.lock'not in returned['CurrentDiagnostics']
 observation=one('iobs_same_engine_uncertain_new_call_raw_images')
 def image_observation(x):
  assert x['ReadError']is None
  if x['Absent']:assert x['Bytes']is None;return None
  assert x['Bytes']is not None;return b64(x['Bytes'])
 assert image_observation(observation['Pending']) is None and image_observation(observation['Interface'])==b64(published['afterImage']['base64']) and set(observation['Mechanical'])==set(mechanical)
 for x in observation['Mechanical'].values():image_observation(x)
 final=one('iobs_same_engine_uncertain_diagnostic_current_resume');assert final['FrozenResult']==final['CurrentProof']==published and final['InertRepeat'] is False and final['Handled']==returned['Handled']
 before,after=snap(final['Before']),snap(final['After']);assert before!=after and before['output/interface_updates.json']==after['output/interface_updates.json']==b64(published['afterImage']['base64'])
 for rel in TERMINAL:assert after.get(rel)is None
 assert sum(bool(x['InterfaceChanged'])for x in final['Trace'])==1 and any(x['MarkerCommitted']for x in final['Trace']) and final['Trace']==returned['Trace']
 mechanics=actual_mechanics(rows,published['binding']);settled=actual_settled(rows)
 cases.append(dict(name=u.attrib['testName'],duration=u.attrib['duration'],initialDisposition='PublicationUncertain',initialDirectRestoreRetainedPaths=222,oldAttemptExpired=True,newFullProofReached=True,turn=43,unchangedPublishedInterface=True,captureCallbacks=2,mechanics=mechanics,settled=settled,terminalArtifactsAbsent=TERMINAL,inertRepeat=True,postCallTelemetry=returned,rawObservationReadErrors=False,originalMissingDiagnosticsNotReconstructed=True))
if mode=='crash5':
 for u in us:
  assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns) is None
  definition=definitions[u.attrib['testId']];assert definition['className'].split(',')[0]+'.'+definition['name']==method_name
  rows=lines(u.find('t:Output/t:StdOut',ns).text);by=boundaries(rows)
  def one(k):return unique(by,k)
  outcome=one('iobs_actual_current_resume_exit');original=one('iobs_actual_abrupt_original_exit');assert original==outcome['Original'];resumed=outcome['Resume'];cut=outcome['Cut'];result=json.loads(outcome['ResultRaw']);assert result['Fault'] is None
  rawcut=one('iobs_actual_abrupt_cut_raw_before_identity');cutbytes=b64(rawcut['Base64']);assert len(cutbytes)==rawcut['Bytes'] and h(cutbytes).upper()==rawcut['Sha256'] and jbytes(cutbytes)==cut
  walk(cut);walk(result)
  own=outcome['OwnedContainer'];root=cut['Root'];aux=result['AuxiliaryRoot'];roots_seen.append(own);nonce_seen.append(cut['Nonce']);parents.add(cut['ParentPid'])
  assert str(Path(own).parent)==cloud['environment']['root'] and re.fullmatch('boe-joint-interface-crash-[0-9a-f]{32}',Path(own).name)
  assert Path(root).parent==Path(aux).parent==Path(own)/'tmp' and root!=aux and result['Root']==root
  assert outcome['ActualContainedRoots']==[root] and re.fullmatch('[0-9a-f]{32}',cut['Nonce']) and result['Nonce']==cut['Nonce']
  assert len(by['iobs_actual_native_join'])==2
  for child,join,role,expected_exit,selfrow in zip([original,resumed],by['iobs_actual_native_join'],['engine-joint-interface-original-crash','engine-joint-interface-resume'],[73,0],[cut,result]):
   assert child['ExitCode']==join['ExitCode']==expected_exit and child['Eof']and child['Joined']and join['Eof']and join['Joined']
   assert join['Failure']is None and join['CleanupFailure']is None and join['RetainedOwnedContainer']is None
   assert child['Pid']==join['Id']==selfrow['Pid'] and child['BirthTick']==join['BirthTick']==selfrow['BirthTick'] and child['BirthTick']>0
   assert child['BirthUtc']==join['BirthUtc'] and child['Stdout']==join['RawStdout'] and child['Stderr']==join['RawStderr']==''
   assert child['Arguments']==join['Arguments'] and child['Executable']==join['Executable']=='/tmp/1536-cloud-tools/dotnet/dotnet'
   args=child['Arguments'];d=child['Dependencies'];deps(d);assert d==join['Dependencies']==selfrow['Dependencies'];loaded(selfrow['Loaded'],d)
   assert args==['exec','--runtimeconfig',d['paths']['runtimeconfig'],'--depsfile',d['paths']['depsfile'],d['paths']['support'],role,d['paths']['integration'],own if expected_exit==73 else root,str(Path(own)/('cut.json'if expected_exit==73 else'resume.json')),str(cut['Cut']),own]
   allident.append((child['Pid'],child['BirthTick']))
  assert original['Pid']!=resumed['Pid'] and cut['ParentPid']not in {original['Pid'],resumed['Pid']} and original['BirthTick']<resumed['BirthTick']

  n=cut['Cut'];assert n in range(5) and u.attrib['testName']==method_name+'(cut: '+str(n)+')'
  assert cut['Turn']==42 and cut['CommonCallbacks']==0 and cut['FirstMarker'] is None
  assert len(cut['MechanicalBefore'])==15 and cut['MechanicalBefore']==cut['MechanicalAtCut']
  journal=jbytes(b64(cut['JournalBase64']));walk(journal)
  assert journal['Committed']==(n!=1) and cut['Phase']==('MemberPublished'if n==1 else'Committed')
  pending_member=next(x for x in journal['Members']if x['Path']==str(Path(root)/'game_session'/'game_state/control/pending_wound_resolutions.json'))
  assert journal_image(pending_member['After'])==b64(cut['PendingMemberAfter'])
  pending=jbytes(b64(cut['PendingMemberAfter']));assert pending['jointRepairCheckpoint']==cut['Checkpoint'] and cut['Checkpoint']['schemaVersion']==2
  if n==3:assert cut['Result'] is None and 'jointInterfacePublication'not in pending
  else:
   assert pending['jointInterfacePublication']==cut['Result'] and cut['Result']['state']==('Published'if n in [1,2]else'Prepared')
   assert bool(cut['Result']['issues'])==(n==4)
  if n==1:
   applied=journal['Members'][cut['Index']];assert applied['Path']==str(Path(root)/'game_session'/'output/interface_updates.json')
   assert journal_image(applied['After'])==b64(cut['Interface'])==b64(cut['Result']['afterImage']['base64'])
  else:assert cut['Index']==-1 and b64(cut['ActualPending'])==b64(cut['PendingMemberAfter'])
  prodrows=lines(original['Stdout']);assert not any('CleanupOwnedRoot'in z for z in prodrows)
  prereq=unique(boundaries(prodrows),'fr063_joint_completion_actual_prerequisites')
  assert prereq['Source']['TurnNumber']==42 and prereq['Original']['TurnNumber']==43 and prereq['ConfirmedHeldBeforeEntry'] and prereq['NoPrecomposedCommonPlan'] and prereq['EnergyBefore']==2 and prereq['HealthBefore']==5
  resumelines=lines(resumed['Stdout']);resby=boundaries(resumelines)
  before,after,repeated=[snap(result[k])for k in ['Before','After','Repeated']]
  assert after==repeated and result['InertRepeatBytesEqual'] and result['FreshAuthorityFieldsAbsentBeforeEntry'] and result['ActualCallerEntered'] and result['Failure']is None
  assert result['Completed']==(n<3) and result['Held']==(n>=3) and result['NewChangingICommits']==(1 if n<2 else 0) and result['IntegrityGuardEntries']==[]
  recovered=jbytes(b64(result['RecoveredPending']))
  assert before['game_state/control/pending_wound_resolutions.json']==b64(result['RecoveredPending'])
  if n!=3:
   rr=recovered['jointInterfacePublication'];assert rr['state']==('Published'if n==2 else'Prepared')
   assert b64(result['RecoveredI'])==b64(rr['afterImage'if n==2 else'beforeImage']['base64'])
  else:assert 'jointInterfacePublication'not in recovered
  semantic={}
  if n<3:
   proof=unique(resby,'iobs_actual_completed_data_before_terminal_cleanup');assert proof['Original']['TurnNumber']==43 and proof['Record']==result['Result']
   published=dict(cut['Result']);published['state']='Published';assert published==result['Result'] and not published['issues']
   assert b64(proof['Interface'])==after['output/interface_updates.json']==b64(published['afterImage']['base64'])
   assert result['Second'] is False and result['Publications']
   for rel in TERMINAL:assert after.get(rel) is None
   semantic['mechanics']=actual_mechanics(resumelines,published['binding']);semantic['settled']=actual_settled(resumelines)
   assert before!=after
   for publication in result['Publications']:
    walk(publication);assert publication['Journal']['Committed'] and b64(publication['Marker']) is not None
  else:
   assert before==after and not result['Publications'] and not resby['iobs_actual_completed_data_before_terminal_cleanup'] and not resby['iobs_actual_resumed_once_mechanics']
   for path,value in cut['MechanicalBefore'].items():assert before.get(path)==b64(value)
   if n==4:
    assert result['Result']==cut['Result'] and result['Result']['issues']
    for issue in result['Result']['issues']:assert json.dumps(issue,separators=(',',':'),ensure_ascii=False) in result['Logs']
  if n==2:
   assert cut['AmbientParseAccepted'] and not result['AmbientParseAccepted'] and result['IndependentOrdinaryPolicyRejected']
   assert result['Result']['timestampCheck']['timestamp']=='ParseAccepted' and len(resby['iobs_independent_ordinary_policy_contrast'])==1
  cleanups=[z for z in rows if 'CleanupOwnedRoot'in z];auxclean=[z for z in resumelines if 'CleanupOwnedRoot'in z]
  assert len(cleanups)==2 and len(auxclean)==1 and auxclean[0]['CleanupOwnedRoot']==aux and own in {z['CleanupOwnedRoot']for z in cleanups}
  for z in cleanups+auxclean:assert z['OwnedFixtureRemoved'] and z['CleanupFailure']is None and not z.get('OwnedFixtureRetainedForUnresolvedChild',False)
  classroot=next(z['CleanupOwnedRoot']for z in cleanups if z['CleanupOwnedRoot']!=own);assert str(Path(classroot).parent)==cloud['environment']['root'];class_roots.append(classroot)
  cases.append(dict(cut=n,name=u.attrib['testName'],duration=u.attrib['duration'],producer=dict(pid=original['Pid'],birthTick=original['BirthTick'],exit=73),resumer=dict(pid=resumed['Pid'],birthTick=resumed['BirthTick'],exit=0),ownedContainer=own,nonce=cut['Nonce'],journalCommitted=journal['Committed'],phase=cut['Phase'],completed=result['Completed'],held=result['Held'],changingI=result['NewChangingICommits'],repeatByteEquality=True,semantic=semantic))
 assert {x['cut']for x in cases}==set(range(5)) and len(set(allident))==len({x[0]for x in allident})==10
 assert len(set(roots_seen))==len(set(nonce_seen))==len(set(class_roots))==5 and len(parents)==1
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
rp=Path(args.recovery_receipt);rc=read(rp)
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
out=dict(kind='INDEPENDENT_BORROWED_LEASE_ORIGINAL_EVIDENCE_CHECK_ONLY',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),mode=mode,carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,storedRawRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],productDeltaPaths=22,nativeExit=0,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],nineAndFull1575OutputsExact=True,sourceReadback=source,cases=cases,childIdentities=allident,jsonDiagnosticParseErrors=0,rawImageShaChecks=rawcount,journalImageShaChecks=journalchecks,embeddedImageShaChecks=embeddedcount,plainBase64Decodes=plainbase64checks,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=rc['changedPathsVerified'],bytesHashesBlobsModesVerified=True,completeSavedGitTreeMatchesIndependentClone=True),qualifiedRecorded=cloud['qualifiedCases']['totalDistinctTargets'],additionalQualifiedByChecker=0,findings=[],verdict='CHECKS_ONLY_REQUIRES_INDEPENDENT_SEMANTIC_REVIEW_NO_AUTOMATIC_ADMISSION_OR_NEXT_MODE',limits='Saved original/source/Git/archive/build-output/census reads only. No native/build/discovery/controller/observer execution, current fixture-root or process inspection/control, repository edit or retry. Dated snapshots do not prove continuous absence. Old original failures and lost corrected crash UNKNOWN unchanged.')
q=D/('t179pd-ni-iobs-l01-borrowed-lease-'+mode+'-independent-check-'+carrier[:8]+'-20261018.json');q.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(path=str(q),bytes=q.stat().st_size,sha256=h(q.read_bytes()),findings=[],admission=False)))
