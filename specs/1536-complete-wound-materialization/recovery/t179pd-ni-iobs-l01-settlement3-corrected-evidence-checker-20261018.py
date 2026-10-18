from pathlib import Path
import json,hashlib,gzip,subprocess,base64,xml.etree.ElementTree as E,datetime as dt,collections,re
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/'
carrier='c82b8a095fb200ae5f02d3f920edb2181a531a19';actual='3119d29ae5a44c6bc5804b91a9f6efe47e4b66d6';compiled='7dbfbddc74b9fe55fcf80bb08d85bf26d245b38c'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['settlement3Corrected'];p=Path(info['recordPath']);r=read(p)
assert h(p.read_bytes())==info['recordSha256']=='2c81238282ff0d54df65f437102f4d2f639dc3ba5049a9eec1e02ea4a36eb0a9'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==0 and r['mode']=='settlement3' and r['expectedCases']==3 and r['statusAfter']=='' and '-NoBuild' in r['argv'] and '-PlanOnly' not in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='304a7e8beb3174246ac3db792c6aa63319fa8ead9f3bab62f91346d4f204248d'
assert h(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['pinDigest']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json') and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==git(R,'show',compiled+':'+pin['path'])==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
 assert git(C,'ls-tree',carrier,'--',pin['path']).split(b'\t')[0].decode().split()==[pin['mode'],'blob',pin['gitBlob']]
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert (p.parent/'ledger-context.json').read_bytes()==frozen(packet['ledger']['path']) and h((p.parent/'ledger-context.json').read_bytes())==packet['ledger']['sha256']
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='0432802cfe44191048be33912c0ef3fbf28402cdaa231b46a14adca48c5a7329' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[0]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=3,Executed=3,Passed=3,Failed=0) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=3,CompletedCases=3,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests'] and not s['PlanOnly'] and not s['DiscoveryOnly']
assert s['Revision']['Head']==actual and s['Revision']['ChangedFiles']==0 and len(s['CategoryResults'])==1
category=s['CategoryResults'][0];assert category['Completed'] and category['BudgetMinutes']==15 and category['Seconds']<900
desc=plan['Descriptors'][0];assert len(plan['Descriptors'])==1 and desc['BudgetMinutes']==15 and desc['EstimatedCases']==3 and desc['Adapter']=='integration' and desc['Exclusive']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256']=='7ed48419621c0f0d778c6aed35ba7611444de1a3f753d475ef07df372dadbab1' and prep['mode']=='corrective-plan' and prep['actualTestsExecuted']==prep['nativeExit']==0
assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
roots=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
assert [str(q.relative_to(R)) for q in sorted(q for root in roots for q in root.rglob('*') if q.is_file())]==[x['path'] for x in r['allOutputInventory']]
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert h(Path(r['stdoutPath']).read_bytes())==r['stdoutSha256'];proc=read(r['processRecord']);assert proc['argv']==r['argv'] and proc['startedUtc']==r['startedUtc']
pid=proc['identity']['pid'];tick=int(proc['identity']['stat'].rsplit(')',1)[1].split()[19]);assert pid==27993 and tick==1308577 and '(pwsh)' in proc['identity']['stat'] and proc['identity']['cmdline'].split()==r['argv']
sentinel=read(r['firstSentinel']);assert sentinel['mode']=='settlement3' and sentinel['actualSource']==actual and sentinel['compiledSource']==compiled and sentinel['label']==r['label']
selected=read(p.parent/'exact-selected-scope.json');assert selected==dict(schemaVersion=1,selections=[x for x in json.loads(frozen('tests/selection.json'))['selections'] if x['category']==desc['Category']])
expected={x['Name']:x['Cases'] for x in desc['ExpectedMethodCounts']};assert len(expected)==2 and set(expected.values())=={1,2} and set(desc['Filter'].split('|'))=={'FullyQualifiedName='+m for m in expected}
assert expected=={x['Name']:x['Cases'] for x in next(x for x in read(Path(prep['resultDirectories'][0])/'plan.json')['Descriptors'] if x['Category']==desc['Category'])['ExpectedMethodCounts']}
exits=re.findall(r'^===== ([^\n]+): exit (-?\d+) =====$',(rd/'dotnet-test.log').read_text(),re.M);assert exits and all(e=='0' for _,e in exits) and not any(n.startswith('Build-') for n,_ in exits)
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
assert 'private readonly InvalidDataException _settlementFailure' in fixture and '_settlementFaults != 0' in fixture and 'throw _settlementFailure;' in fixture
assert 'outcome.Failure is InvalidDataException' in storage and 'outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack' in storage and '!IsTransientFileAccessException(outcome.Failure)' in storage
assert 'attempt.Prepared.Members.All(member => Matches(member.Path, member.Before))' in publisher
# Frozen source / reviewed fixture-only correction. Never inspect current fixture roots or /proc.
assert len(packet['productDelta'])==21
assert sorted(packet['productDelta'])==git(R,'diff','--name-only',packet['productBase'],compiled,'--','BookOfEternityClient').decode().splitlines()
old_fixture=git(R,'show','67e143b24645e0a5b25829b17cb27e80edf3f000:'+sourcepaths[0])
assert frozen(sourcepaths[0]).replace(b'private readonly InvalidDataException _settlementFailure',b'private readonly IOException _settlementFailure')==old_fixture
unchanged_assertion_files=[]
for pin in packet['sourcePins']:
 if pin['path'].startswith(('BookOfEternityClient.IntegrationTests/','BookOfEternityClient.Tests/','BookOfEternityClient.TestSupport/')) or pin['path'] in ['tests/categories.json','tests/selection.json']:
  if pin['path']!=sourcepaths[0]:assert frozen(pin['path'])==git(R,'show','67e143b24645e0a5b25829b17cb27e80edf3f000:'+pin['path'])
  unchanged_assertion_files.append(pin['path'])
assert len([x for x in r['binaryAfter']if x['path'].endswith('/BookOfEternityClient.dll') and x['sha256']=='2da34865a700f8a2b1ec240a9038490e7f76583a65df18aa8cd0305d80c96478'])==4
assert len({x['sha256']for x in r['binaryAfter']if x['path'].endswith('/BookOfEternityClient.TestSupport.dll')})==1
registry_path='BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs'
registry=frozen(registry_path).decode();assert 'receipt.Preparation.ValidateInventoryCurrentAsync' in registry and 'receipt.TryConsume(JointRepairPublicationReceiptCapability)'in registry
source.append(dict(path=registry_path,sha256=h(frozen(registry_path)),bytes=len(frozen(registry_path))))
common_source=frozen('BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalJointRepair.cs').decode()
assert 'var compensation = await transaction.CompensateAndRearmAsync(_fs, lease);'in common_source
assert 'return new(compensation.Disposition, null, JointPublicationIssue(error).Concat(compensation.Issues).ToArray());'in common_source
assert 'await token.CompensateRemovalAsync(_fs, lease);'in frozen('BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairTerminalRelease.cs').decode()
oldcloud=json.loads(git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'))
for key,value in oldcloud['actuals'].items():assert cloud['actuals'][key]==value
assert cloud['qualifiedCases']==oldcloud['qualifiedCases'] and cloud['qualifiedCases']['totalDistinctTargets']==44
assert cloud['oldCorrectedCrash']==oldcloud['oldCorrectedCrash'] and cloud['oldCorrectedCrash']['outcome']=='UNKNOWN' and cloud['oldCorrectedCrash']['artifactAvailability']=='UNAVAILABLE'
assert cloud['holds']==oldcloud['holds']
for key in ['settlement3','races2','close2']:
 x=cloud['actuals'][key];assert h(Path(x['recordPath']).read_bytes())==x['recordSha256']
 ar=R/x['archive']['path'];assert h(ar.read_bytes())==x['archive']['sha256'] and ar.read_bytes()==git(R,'show',actual+':'+x['archive']['path'])
# Verify every archived image, then reconstruct exact post-oracle comparisons from emitted originals.
cases=[];semantics={};parse_errors=[];roots_seen=[];all_case_names=[]
pending_rel='game_state/control/pending_wound_resolutions.json';interface_rel='output/interface_updates.json';marker_rel='game_state/wounds/mortal_qte_decision_publication.json'
for u in us:
 assert u.attrib['outcome']=='Passed' and u.find('t:Output/t:ErrorInfo',ns) is None
 definition=definitions[u.attrib['testId']];method=definition['className'].split(',')[0]+'.'+definition['name'];assert method==u.attrib['testName'].split('(fault:')[0]
 all_case_names.append(u.attrib['testName']);rows=[]
 for line in u.find('t:Output/t:StdOut',ns).text.splitlines():
  if line.startswith('{'):
   try:rows.append(json.loads(line))
   except Exception as error:parse_errors.append(str(error))
 assert not parse_errors
 by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
 def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
 pre=one('fr063_joint_completion_actual_prerequisites');assert pre['Source']['TurnNumber']==42 and pre['Original']['TurnNumber']==43 and pre['ConfirmedHeldBeforeEntry'] and pre['NoPrecomposedCommonPlan'] and pre['EnergyBefore']==2 and pre['HealthBefore']==5 and not pre['nonzeroProgression']
 outcome=one('fr063_joint_completion_actual_owner_outcome');assert outcome['Failure'] is None and outcome['ObserverFailure'] is None and outcome['entered'] and outcome['liveCallerReturned'] and outcome['liveWaitEntered'] and outcome['jointOriginalAuthored'] and outcome['ActualCaller']=='ProcessPlayerTurn' and outcome['ActualTurn']==42
 clean=next(x for x in rows if 'CleanupOwnedRoot'in x);root=clean['CleanupOwnedRoot'];roots_seen.append(root)
 assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and str(Path(root).parent)==cloud['environment']['root'] and re.fullmatch(r'boe-gameengine-turnlifecycle-[0-9a-f]{32}',Path(root).name)
 # Do not inspect live/current fixture roots. Native cleanup claims and dated saved censuses are separate evidence.
 scenario='CommonFault'if 'CommonMember'in method else('TerminalFault'if '(fault: True)'in u.attrib['testName']else'Terminal')
 session=str(Path(root)/'game_session');full=lambda rel:str(Path(session)/rel)
 direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['scenario']==scenario and 'MortalJointRepairPublicationHoldException'in direct['DirectFailure'] and 'RestorePreTurnBackup'in direct['DirectFailure']
 assert direct['Before']==direct['After'] and len(direct['Before'])==222
 for path,value in direct['Before'].items():assert raw(value)==raw(direct['After'][path]) and Path(path).is_relative_to(root)
 public=one('iobs_actual_public_correction_before_ready');mechanical=public['Mechanical'];assert len(mechanical)==15
 for path,value in mechanical.items():assert raw(value)==raw(direct['Before'].get(full(path)))
 mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem');assert mem['CheckpointPresent'] and mem['CorrectionReady'] and mem['CanonicalRefreshPresent'] and mem['IssueCount']==mem['ReportedIssueCount']==1 and not mem['IssueRowsTruncated']
 for issue in mem['Issues']:
  for field in ['Code','FilePath','Message']:
   value=issue[field];assert not value['Truncated'] and h(value['Value'].encode()).upper()==value['FullUtf8Sha256']
 commits=by['iobs_actual_result_commit'];pubs=by['iobs_actual_committed_member'];assert not by['fr063_joint_complete_union'] and not by['fr063_joint_completion_inert_repeat']
 if scenario in {'CommonFault','TerminalFault'}:
  fault=one('iobs_actual_owning_member_fault');journal=fault['Journal'];assert fault['scenario']==scenario and fault['Phase']=='MemberPublished' and fault['Index']==0 and fault['Error']=='L01 actual whole-owner member publication fault.' and not journal['Committed'] and journal['GenerationBefore']==journal['GenerationAfter']
  members={x['Path'].split('/game_session/')[-1]:x for x in journal['Members']};assert len(members)==len(journal['Members'])
  for m in journal['Members']:
   assert Path(m['Path']).is_relative_to(root)
   assert journal_image(m['Before'])==raw(direct['Before'].get(m['Path']))
  assert journal['Members'][0]['Path']==full('game_state/control/pending_mortal_wound_occurrences.json' if scenario=='CommonFault' else pending_rel) and journal_image(members[pending_rel]['After']) is None
 if scenario=='CommonFault':
  assert direct['ActualOuterDisposition'] is None and mem['CanonicalDisposition']=='RetryableHeld'
  assert mem['Issues'][0]['Code']['Value']=='mortal_joint_repair_publication_refused' and mem['Issues'][0]['Message']['Value']==fault['Error']
  held=one('iobs_actual_common_compensation_retained');assert held['Disposition']=='RetryableHeld' and len(held['Trace'])==1 and held['Trace'][0]['Journal']==journal and held['Trace'][0]['IsCommon'] and not held['Trace'][0]['IsTerminal']
  assert len(members)==16 and [x['Record']['state']for x in commits]==['Prepared','Published','Published']
  prepared,published,retained=[x['Record']for x in commits];assert published==retained==held['Result'] and not published['issues'] and published['normalization']=='Changed'
  # Third result observation is the same pending record written by compensation, not another I projection.
  assert {k:v for k,v in prepared.items()if k not in {'state','fingerprint'}}=={k:v for k,v in published.items()if k not in {'state','fingerprint'}}
  assert commits[1]['Mtime']==commits[2]['Mtime'] and raw(commits[1]['Interface'])==raw(commits[2]['Interface'])==raw(held['Interface'])==raw(direct['Before'][full(interface_rel)])
  assert raw(held['Interface'])==base64.b64decode(published['afterImage']['base64']) and raw(public['Interface'])==base64.b64decode(published['beforeImage']['base64'])
  assert len(pubs)==1 and pubs[0]['InterfaceChanged'] and not pubs[0]['MarkerCommitted'] and pubs[0]['TransactionId']==commits[1]['TransactionId']
  assert raw(pubs[0]['Before'])==raw(public['Interface']) and raw(pubs[0]['After'])==raw(held['Interface'])
  capture=one('iobs_actual_completed_capture');assert capture['Callback']==capture['InterfaceCommittedCohorts']==1 and raw(capture['InterfaceRaw'])==raw(held['Interface'])
  pending=json.loads(raw(direct['Before'][full(pending_rel)]).decode('utf-8-sig'));assert pending['jointInterfacePublication']==published
  assert json.loads(journal_image(members[pending_rel]['Before']).decode('utf-8-sig'))==pending
  assert journal_image(members[marker_rel]['Before']) is None and full(marker_rel)not in direct['Before']
  intended_marker=json.loads(journal_image(members[marker_rel]['After']).decode('utf-8-sig'));assert intended_marker['outputBinding']['version']==3 and intended_marker['outputBinding']['publishedInterfaceResult']['record']==published
  assert len({x['TransactionId']for x in commits}|{journal['TransactionId']})==4
  assert outcome['After'][interface_rel]['Sha256']==held['Interface']['Sha256']
  semantics[scenario]=dict(disposition='RetryableHeld',actualTurn=42,oneRealMemberFault=True,faultTransaction=journal['TransactionId'],faultCohortMembers=16,appliedMemberIndex=0,memberBeforeImagesExactlyRetained=16,captureCallbacks=1,interfaceChangingCohorts=1,commonMarkerCommits=0,resultObservations=['Prepared','Published','Published'],compensationRecordSameAsPublished=True,interfaceBytesAndTimestampRetained=True,originalFaultIssueRetained=True,mechanicalPathsUnchanged=15,actualStageDirectRestorePathsUnchanged=222,outerWrapperInvoked=False,sourceOwnership='Actual taken receipt/private common publisher; known storage rollback, original InvalidDataException propagated once without transient retry, owner whole-union compensation/current receipt and confirmed agreement/inventory checks, RetryableHeld rearm.',scopeLimit='No later retry, replay, common completion, cold restart or successful consumption is executed by this case; RetryableHeld does not prohibit a future lawful owning retry.')
 else:
  assert direct['ActualOuterDisposition']=='MortalJointRepairPublicationHeldBlocked' and mem['CanonicalDisposition']=='PublicationUncertain' and len(commits)==1 and not pubs and not by['iobs_actual_completed_capture']
  prepared=commits[0]['Record'];assert prepared['state']=='Prepared' and len(prepared['issues'])==1 and prepared['issues'][0]['code']=='interface_updates_unknown_field'
  held=one('iobs_actual_error_prepared_held');assert held['Result']==prepared and held['ActualTurn']==42 and not held['Trace'] and held['MechanicalBefore']==held['MechanicalAfter']==mechanical
  assert raw(held['Interface'])==raw(public['Interface'])==base64.b64decode(prepared['beforeImage']['base64']) and raw(held['Interface'])!=base64.b64decode(prepared['afterImage']['base64'])
  assert held['Mtime']==public['Mtime']==commits[0]['Mtime'] and 'interface_updates_unknown_field'in held['OwningDiagnostics'] and held['OwningDiagnostics']==outcome['EngineDiagnostics']
  settled=one('iobs_actual_whole_terminal_settlement');assert settled['scenario']==scenario and len(settled['Members'])==6 and settled['Before']==direct['After'] and len(settled['Before'])==222
  changed={full(m['RelativePath']):m for m in settled['Members']};assert len(changed)==6
  assert set(changed)=={full(x)for x in [pending_rel,'game_state/wounds/wound_commands.json','output/debug_logs.json',interface_rel,'output/mortal_wound_decisions.json','output/narrative_response.json']}
  for path,m in changed.items():assert(None if m['Before']is None else base64.b64decode(m['Before']))==raw(settled['Before'].get(path))
  if scenario=='TerminalFault':
   assert settled['Disposition']=='PublicationUncertain' and settled['Faults']==1 and settled['Before']==settled['After'] and len(settled['After'])==222
   assert len(settled['Trace'])==1 and settled['Trace'][0]['Journal']==journal and settled['Trace'][0]['IsTerminal'] and not settled['Trace'][0]['IsCommon'] and len(members)==6
   assert json.loads(raw(settled['After'][full(pending_rel)]).decode('utf-8-sig'))['jointInterfacePublication']==prepared
   for path,m in changed.items():
    journal_member=members[m['RelativePath']];assert journal_image(journal_member['Before'])==raw(settled['After'][path])
    assert journal_image(journal_member['After'])==(None if m['After']is None else base64.b64decode(m['After']))
   semantics[scenario]=dict(disposition='PublicationUncertain',faults=1,faultTransaction=journal['TransactionId'],faultCohortMembers=6,appliedMemberIndex=0,traceKnownCommitted=False,errorPreparedBeforeTerminal=True,preTerminalMechanicalPathsUnchanged=15,actualStageDirectAndOuterRestorePathsUnchanged=222,postTerminalFullBeforeAfterPathsEqual=222,pendingAndExactPreparedResultRetained=True,sourceOwnership='Actual Stage backup/checkpoint and current participating canonical lease; real whole-terminal token/cohort; nonretryable fault after actual first removal reaches existing owner catch/compensation, with current-claim/retained-guard/inventory checks.',scopeLimit='Storage already restores known before-images. The owner compensation path may validate without another physical write; no second physical compensation batch, successful claim release, later retry or cold restart is claimed. Returned uncertainty and full before/after retention are the runtime oracle; the test does not emit the terminal returned issue list.')
  else:
   assert settled['Disposition']=='TerminalReleased' and settled['Faults']==0 and not by['iobs_actual_owning_member_fault'] and len(settled['After'])==219
   trace=settled['Trace'];assert [(x['Phase'],x['Index'])for x in trace]==[('MemberPublished',i)for i in range(6)]+[('Committed',-1)]
   assert len({x['Journal']['TransactionId']for x in trace})==1 and all(not x['Journal']['Committed']for x in trace[:-1]) and trace[-1]['Journal']['Committed']
   for z in trace:assert z['IsTerminal'] and not z['IsCommon'] and len(z['Journal']['Members'])==6
   for path,m in changed.items():assert(None if m['After']is None else base64.b64decode(m['After']))==raw(settled['After'].get(path))
   for path in [pending_rel,'game_state/wounds/wound_commands.json','output/mortal_wound_decisions.json']:assert full(path)not in settled['After']
   assert set(settled['Before'])-set(changed)==set(settled['After'])-set(changed)
   for path in set(settled['Before'])-set(changed):assert raw(settled['Before'][path])==raw(settled['After'][path])
   for m in trace[-1]['Journal']['Members']:
    assert journal_image(m['Before'])==raw(settled['Before'].get(m['Path'])) and journal_image(m['After'])==raw(settled['After'].get(m['Path']))
   semantics[scenario]=dict(disposition='TerminalReleased',faults=0,terminalTransaction=trace[-1]['Journal']['TransactionId'],appliedMemberIndices=list(range(6)),committedTraceCount=1,actualStageDirectAndOuterRestorePathsUnchanged=222,preTerminalErrorPreparedAndMechanicalPathsUnchanged=15,fullBeforeFiles=222,fullAfterFiles=219,changedMembersExact=6,unownedPathsByteEqual=216,removedExactGraphMembers=3,sourceOwnership='Real current confirmed agreement/private terminal token and complete validated Stage baseline; complete cohort/original/readback checks then exact prepared agreement release and one token consumption. Generic record-only deletion cannot return TerminalReleased.',scopeLimit='One successful terminal invocation; no separate duplicate-release attempt, later game progression, cold restart or mechanical settlement is claimed.')
 cases.append(dict(name=u.attrib['testName'],method=method,scenario=scenario,outcome=u.attrib['outcome'],duration=u.attrib['duration'],diagnosticRows=len(rows),ownedRoot=root,ownedFixtureRemoved=True,cleanupFailure=None))
assert len(set(roots_seen))==3 and len(set(all_case_names))==3 and set(semantics)=={'CommonFault','Terminal','TerminalFault'}
assert collections.Counter(x['method']for x in cases)==expected
# Saved v2 snapshots only; do not re-run observer or inspect live namespaces/processes.
censuses=[]
for ref in info['supplementalAuthoritativeCensuses']:
 b=frozen(ref['path']);assert b==git(C,'show',carrier+':'+ref['path']) and h(b)==ref['sha256'] and len(b)==ref['bytes'];c=json.loads(b)
 assert c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and c['fixtureTemp']==cloud['environment']['root'] and c['fixtureTempUid']==1000
 assert c['cleanBoundary'] and not any(c[k]for k in ['unknown','activeTaskGameTestProcesses','ownedRoots','confirmedGone'])
 assert len(c['entries'])==len(c['pidsEnumerated']) and {x['pid']for x in c['entries']}==set(c['pidsEnumerated'])
 for x in c['entries']:
  def ids(raw):
   f=raw.rsplit(')',1)[1].split();return int(raw.split(' ',1)[0]),int(f[19]),int(f[1]),f[0]
  assert ids(x['statBefore'])[:2]==ids(x['statAfter'])[:2]==(x['pid'],x['startTick']) and ids(x['statAfter'])[2:]==(x['ppid'],x['state'])
  if 'argv'in x:assert h(('\0'.join(x['argv'])+'\0').encode())==x['argvSha256']
  if x['classification']=='exited-nonexecuting':assert x['state']in{'Z','X','x'} and x['argvCount']==0
  assert x['classification']in{'other-readable','observer-self','build-infrastructure','exited-nonexecuting'}
 assert all(x['status']=='MATCH'for x in c['knownVisibility'])
 assert {x['pid']for x in c['buildInfrastructure']}=={x['pid']for x in c['entries']if x['classification']=='build-infrastructure'}
 for x in c['buildInfrastructure']:assert any('MSBuild.dll'in a or 'VBCSCompiler'in a for a in x['argv'])
 censuses.append(dict(path=ref['path'],sha256=h(b),startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=len(c['entries']),knownVisibility=c['knownVisibility'],classes=dict(collections.Counter(x['classification']for x in c['entries'])),clean=True))
assert [x['entries']for x in censuses]==[81,82]
beforegap=(dt.datetime.fromisoformat(r['startedUtc'])-dt.datetime.fromisoformat(censuses[0]['finishedUtc'])).total_seconds();aftergap=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds()
assert beforegap==2.708841 and aftergap==2.375432
assert h(frozen(REC+'t179pd-ni-iobs-l01-process-census-v2-20261018.py'))=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f'
# Recovery: original remote provenance is the receipt; independently compare all saved Git objects/modes and restored bytes.
rp=Path('/tmp/1536-cloud-tools/carrier-recovery-c82b8a09.json');rc=read(rp);assert rc['sha']==carrier and rc['base']==actual and rc['changedPathsVerified']==22 and rc['remoteShaVerified'] and rc['clean'] and not rc['localSourceOrObjectReuse']
assert set(git(R,'diff','--name-only',actual,carrier).decode().splitlines())=={x['path']for x in rc['files']}
assert git(R,'ls-tree','-r',carrier)==git(C,'ls-tree','-r',carrier)
for x in rc['files']:
 b=frozen(x['path']);assert b==(C/x['path']).read_bytes() and h(b)==x['sha256'] and len(b)==x['bytes'] and blob(b)==x['gitBlob']
 assert git(C,'ls-tree',carrier,'--',x['path']).split(b'\t')[0].decode().split()==[x['mode'],'blob',x['gitBlob']]
for x in info['acceptanceAndRecoveryReadbacks']+[info['parentInspection'],info['strongOracleParentReadback']]:
 b=frozen(x['path']);assert h(b)==x['sha256'] and len(b)==x['bytes'] and b==git(C,'show',carrier+':'+x['path'])
assert frozen(info['acceptanceAndRecoveryReadbacks'][2]['path'])==(D/'t179pd-ni-iobs-l01-settlement-prep-acceptance-preflight-readback-3119d29a-20261018.json').read_bytes()
# Saved release/actual/provenance bijection; qualification count only changes after independent acceptance.
release=packet['namedActualRelease'];assert release['mode']=='settlement3' and release['label']==r['label']=='t179pd-ni-iobs-l01-process-first-settlement3-fixture-corrected-cloud-813f1057' and release['compiledSource']==compiled and release['prepRecord']==r['prepRecord']
assert {x['method']:x['cases']for x in release['methods']}==expected
out=dict(kind='INDEPENDENT_CORRECTED_SETTLEMENT3_EVIDENCE_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,originalStoredRestoredExact=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],productDeltaPaths=21,sourceReadback=source,unchangedAssertionFixtureCatalogSelectionPaths=unchanged_assertion_files,oneLineFixtureCorrectionExactInverse=True,nativeExit=0,tests=s['Tests'],selection=s['Selection'],categoryResult=category,wallSeconds=r['wallSeconds'],noTimeout=True,binaries9Full1575Unchanged=True,currentPhysicalOutputInventoryVerified=True,controllerProcessOriginal=dict(pid=pid,startTick=tick,argvExact=True),jsonDiagnosticParseErrors=0,rawImageLengthShaChecks=rawcount,embeddedImageShaChecks=embeddedcount,journalImageShaChecks=journalchecks,cases=cases,semantics=semantics,censuses=censuses,beforeGapSeconds=beforegap,afterGapSeconds=aftergap,cleanup='Original native owned-tree/runtime cleanup and three isolated GUID-root cleanup succeeded. Original exe-dependent arrays remain incomplete. Saved v2 snapshots are finite dated boundaries only; no current root/process inspection.',recovery=dict(receiptPath=str(rp),receiptSha256=h(rp.read_bytes()),changedPaths=22,allBytesHashesBlobsModesVerified=True,completeSavedGitTreeMatchesIndependentClone=True,remoteProvenance='Exact remote/full independent clone confirmed by parent recovery receipt; no new remote fetch by reviewer.'),preservation=dict(priorQualified44Unchanged=True,originalSettlement1PASS2FAILUnchanged=True,priorActualsAndHoldsUnchanged=True,lostOldCorrectedCrash='UNKNOWN_UNAVAILABLE_UNCHANGED',additionalDistinctTargetsEligible=3,qualifiedTotalAfterAcceptance=47),findings=[],limits=['No runtime/build/discovery/controller/observer execution, current fixture-root or process inspection/intervention, repository edits or repeat of retained44.','No common completion or lawful retry, cold child, duplicate release, uncertain1/integrity4/crash5 outcome inferred from settlement3.','TerminalFault proves PublicationUncertain and full222before/after retention; no invented standalone compensation write or terminal issue-list observation.','Original frozen outcomes and old crashUNKNOWN remain immutable; no cross-build equivalence.'])
p=D/'t179pd-ni-iobs-l01-settlement3-corrected-independent-check-c82b8a09-20261018.json';p.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(p),bytes=p.stat().st_size,sha256=h(p.read_bytes()),rawChecks=rawcount,journalChecks=journalchecks,findings=[])))
