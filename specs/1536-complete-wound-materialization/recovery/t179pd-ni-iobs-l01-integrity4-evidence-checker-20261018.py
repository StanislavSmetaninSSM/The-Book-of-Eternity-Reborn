from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='90fbb068817257a670c0ac99f6d1bbfd6cbfc19b';actual='c82b8a095fb200ae5f02d3f920edb2181a531a19';compiled='7dbfbddc74b9fe55fcf80bb08d85bf26d245b38c'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['integrity4'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='aba7ad1dc1301329ef926b3616f32e94f88b7af754f03a6dbbb4990f7931fa34'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['mode']=='integrity4' and r['expectedCases']==4 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='304a7e8beb3174246ac3db792c6aa63319fa8ead9f3bab62f91346d4f204248d'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==git(R,'show',compiled+':'+pin['path'])==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='48f53f9d0c73ed0459bf8ac5c9ea5c1bc96fa6521ff52e48008b9e4591cb5e43' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=4,Executed=4,Passed=4,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=4,CompletedCases=4,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==15 and desc['EstimatedCases']==4 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='2536796c17257853b72940736ad92763443c7727597e9915a32f28ae9b87fa5f' and prep['mode']=='remaining-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==28852 and tick==1382471 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline']==''
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='integrity4' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert len(expected)==1 and set(expected.values())=={4} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e=='0' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
trxs=list(rd.rglob('*.trx'));assert len(trxs)==1;t=E.parse(trxs[0]).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==4 and {u.attrib['testName'].split('(fault:')[0] for u in us}==set(expected)
definitions={x.attrib['id']:x.find('t:TestMethod',ns).attrib for x in t.findall('.//t:UnitTest',ns)};assert len(definitions)==4
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
assert len([x for x in r['binaryAfter']if x['path'].endswith('/BookOfEternityClient.dll') and x['sha256']=='2da34865a700f8a2b1ec240a9038490e7f76583a65df18aa8cd0305d80c96478'])==4
sourcepaths=['BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceCrash.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceLifecycle.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.QtePhysicalInjuryConsumerArchive.cs','BookOfEternityClient.TestSupport/NativeHostScenarioDriver.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInterfaceResume.cs','BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs','BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs','BookOfEternityClient/Services/MortalJointInterfacePublication.cs','BookOfEternityClient/Core/FileSystemManager.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SpiritualContinuation.cs','BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.JointInterfaceUncertainResume.cs']
source=[]
for path in sourcepaths:
 b=frozen(path);assert b==git(R,'show',compiled+':'+path)==git(C,'show',carrier+':'+path);source.append(dict(path=path,sha256=h(b),bytes=len(b)))
crash=frozen(sourcepaths[0]).decode();resume=frozen(sourcepaths[4]).decode();upstream=frozen(sourcepaths[5]).decode();items=frozen(sourcepaths[6]).decode();logger=frozen(sourcepaths[9]).decode()
assert 'Environment.Exit(73);'in crash and 'TimeSpan.FromSeconds(220)'in crash and 'fields[19]'in crash and 'start.Environment["TMPDIR"] = Path.Combine(own, "tmp")'in crash
assert 'HasUnsettledChildren' in crash and 'OwnedFixtureRetainedForUnresolvedChild = true'in crash
assert resume.index('var lease = await _fs.AcquireCanonicalWriteLeaseAsync(); Exception? failure = null;')<resume.index('var upstream = await CollectAcceptedTurnUpstreamRawStateIssuesAsync();')<resume.index('current = await MortalJointInterfacePublication.CommitPublishedAsync')
assert 'var itemValidation = await ValidateAcceptedTurnUpstreamRawItemsAsync();'in upstream
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
cases=[];allident=[];roots_seen=[];nonce_seen=[];parents=set();class_roots=[];faultpaths={'Version':'game_state/control/pending_wound_resolutions.json','RecordFingerprint':'game_state/control/pending_wound_resolutions.json','OriginalManifest':'game_state/control/pending_turn_snapshot.json','CurrentInterface':'output/interface_updates.json'}
for u in us:
 assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns)is None
 definition=definitions[u.attrib['testId']];method=definition['className'].split(',')[0]+'.'+definition['name'];assert method in expected and u.attrib['testName'].startswith(method+'(fault: ')
 rows=lines(u.find('t:Output/t:StdOut',ns).text);by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
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
  assert result['Logs'].count('System.IO.IOException: The process cannot access the file')==2 and result['Logs'].count('canonical-write.lock')==2 and 'joint_interface_actual_publication_preimage_or_readback_changed'not in result['Logs']
  chunks=result['Logs'].removeprefix('Engine diagnostics:\n').split('\nSelected joint I result retained; no ordinary normalization/restore fallback.\n')
  target='NOT PROVEN: both entries stop at nested canonical-write lock IOException; unchanged/held is compatible with unrelated early refusal. Intended I preimage check is downstream.'
 cleanups=[z for z in rows if 'CleanupOwnedRoot'in z];auxclean=[z for z in resumelines if 'CleanupOwnedRoot'in z];assert len(cleanups)==2 and len(auxclean)==1
 for z in cleanups+auxclean:assert z['OwnedFixtureRemoved'] and z['CleanupFailure']is None and not z.get('OwnedFixtureRetainedForUnresolvedChild',False)
 assert auxclean[0]['CleanupOwnedRoot']==aux and own in {z['CleanupOwnedRoot']for z in cleanups}
 classroot=next(z['CleanupOwnedRoot']for z in cleanups if z['CleanupOwnedRoot']!=own);assert str(Path(classroot).parent)==cloud['environment']['root'];class_roots.append(classroot)
 cases.append(dict(fault=fault,name=u.attrib['testName'],duration=u.attrib['duration'],nativeOutcome='Passed',producer=dict(pid=original['Pid'],birthTick=original['BirthTick'],exit=73),resumer=dict(pid=resumed['Pid'],birthTick=resumed['BirthTick'],exit=0),parentTestHostPid=cut['ParentPid'],ownedContainer=own,nonce=cut['Nonce'],cutTransaction=journal['TransactionId'],cut='Prepared committed before I publication',journalMembers=1,completePhysicalImagesCompared=223,beforeAfterRepeatedDigest=digest_snap(before),mechanicalImagesUnchanged=15,physicalFaultPath=mut['Path'],faultBeforeSha256=h(bf),faultAfterSha256=h(afault),freshAuthorityAbsent=True,actualCallerAndSecondEntryHeld=True,changingI=0,commonPublications=0,parentOwnedCleanups=2,childAuxiliaryCleanup=True,dependenciesAndLoadedHashesExact=True,targetReach=target,logs=result['Logs'],targetIntegrityQualified=False))
assert len(set(allident))==len({x[0]for x in allident})==8 and len(set(roots_seen))==len(set(nonce_seen))==len(set(class_roots))==4 and len(parents)==1
assert {x['fault']for x in cases}==set(faultpaths)
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
assert [x['entries']for x in censuses]==[83,84]
beforegap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();aftergap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds();assert beforegap==2.538864 and aftergap==1.077795
rp=Path('/tmp/1536-cloud-tools/carrier-recovery-90fbb068.json');rc=read(rp);assert rc['sha']==carrier and rc['base']==actual and rc['changedPathsVerified']==24 and rc['remoteShaVerified'] and rc['clean'] and not rc['localSourceOrObjectReuse']
assert set(git(R,'diff','--name-only',actual,carrier).decode().splitlines())=={x['path']for x in rc['files']} and git(R,'ls-tree','-r',carrier)==git(C,'ls-tree','-r',carrier)
for x in rc['files']:
 b=frozen(x['path']);assert b==(C/x['path']).read_bytes() and h(b)==x['sha256'] and len(b)==x['bytes'] and blob(b)==x['gitBlob']
 assert git(C,'ls-tree',carrier,'--',x['path']).split(b'\t')[0].decode().split()==[x['mode'],'blob',x['gitBlob']]
assert cloud['qualifiedCases']['totalDistinctTargets']==47 and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability']=='UNAVAILABLE'
oldcloud=json.loads(git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for key,v in oldcloud['actuals'].items():
 if key!='settlement3Corrected':assert cloud['actuals'][key]==v
assert cloud['oldCorrectedCrash']==oldcloud['oldCorrectedCrash'] and cloud['holds']==oldcloud['holds']
for suffix in ['json','md']:
 name='t179pd-ni-iobs-l01-settlement3-corrected-independent-review-20261018.'+suffix;assert frozen(REC+name)==(D/name).read_bytes()
for ref in [info['parentInspection'],info['strongOracleParentReadback']]:assert h(frozen(ref['path']))==ref['sha256']
findings=[dict(id='L01-INTEGRITY-CURRENTI-001',severity='blocking',case='CurrentInterface',classification='UNRELATED_NESTED_CANONICAL_LEASE_FAILURE_SATISFIES_BROAD_REFUSAL_ORACLE',evidence='Both resumed calls log canonical-write.lock IOException; logger truncates each exception message at2000 chars. Current I was genuinely changed by one space and all223 captured images retained, but no target I preimage mismatch diagnosis or reached guard is recorded.',sourceCause='Resume.cs59 holds one canonical lease; Resume.cs67 calls CollectAcceptedTurnUpstreamRawStateIssuesAsync; ValidationAndRepair.cs1730 calls ValidateAcceptedTurnUpstreamRawItemsAsync; SpiritualContinuation.cs60 acquires another canonical lease. Physical FileShare.None acquisition exhausts retries and is caught by selected hold catch at Resume.cs124-129 before CommitPublishedAsync/RequireImagesAsync.',impact='Native4PASS unchanged; intended CurrentInterface integrity refusal is not qualified. Hold entire integrity4 group, retain qualified47, freeze further progression including newcrash5; preserve any already-running uncertain1 original. Other three native passes are not separately accepted here.')]
out=dict(kind='INDEPENDENT_INTEGRITY4_ORIGINAL_EVIDENCE_CHECK_WITH_BLOCKING_SEMANTIC_FINDING',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,storedRawRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],productDeltaPaths=21,sourceReadback=source,nativeExit=0,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],nineAndFull1575OutputsExact=True,controllerProcess=dict(pid=pid,startTick=tick,cmdlineInitiallyEmpty=True,argvSeparateExact=True),cases=cases,distinctChildPidBirthIdentities=8,distinctOwnedContainersAndNonces=4,jsonDiagnosticParseErrors=0,rawImageShaChecks=rawcount,journalImageShaChecks=journalchecks,embeddedImageShaChecks=embeddedcount,plainBase64Decodes=plainbase64checks,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=24,bytesHashesBlobsModesVerified=True,completeGitTreeMatchesIndependentClone=True),qualifiedBeforeAndAfter=47,additionalQualifiedTargets=0,findings=findings,limits='Saved evidence/source/build-output reads only. No native/build/discovery/observer/controller execution, current fixture-root or process inspection/control, repo writes or retry. Native originals and old crashUNKNOWN unchanged. Dated snapshots do not prove continuous absence. Loaded dependency checks cover recorded integration/support/immutable and full managed build inventory; no child image reuse or old process restoration claimed.')
p=D/'t179pd-ni-iobs-l01-integrity4-independent-check-90fbb068-20261018.json';p.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(p),bytes=p.stat().st_size,sha256=h(p.read_bytes()),findings=[x['id']for x in findings])))
