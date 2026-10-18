from pathlib import Path
import json,hashlib,gzip,base64,subprocess,xml.etree.ElementTree as E,datetime as dt,collections
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');D=Path('/tmp/1536-cloud-tools/review');REC='specs/1536-complete-wound-materialization/recovery/';carrier='8f94b4b2b16d67d7862ea106cf6a9fb33eb86c15';actual='23362e5701daf6f01aa989f7fde070242a9167d3';compiled='cf52201ca165d24c971018251ef9723a8e493384'
def h(b):return hashlib.sha256(b).hexdigest()
def read(p):return json.loads(Path(p).read_bytes())
def git(root,*a):return subprocess.check_output(['git',*a],cwd=root)
def frozen(p):return git(R,'show',carrier+':'+p)
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
cloud=json.loads(frozen(REC+'t179pd-ni-iobs-l01-cloud-qualification-20261018.json'));info=cloud['actuals']['races2'];p=Path(info['recordPath']);r=read(p);assert h(p.read_bytes())==info['recordSha256']=='359cdd08bf17c7b19b8292e946a0be9d964ac0cc42a54e86bf18652fbb0c5dfb'
assert r['actualSource']==r['headAfter']==actual and r['compiledSource']==compiled and r['nativeExit']==1 and r['mode']=='races2' and r['expectedCases']==2 and r['statusAfter']=='' and '-NoBuild'in r['argv']
packet=read(p.parent/'source-binding.json');assert len(packet['sourcePins'])==98 and packet['pinDigest']==r['sourcePinDigest']=='b29c971342ee772b13cd123a954d7afa8937e1f50a7e5f4c4c14403e4de4c347' and h((p.parent/'source-binding.json').read_bytes())==r['sourceBindingSha256']
assert (p.parent/'source-binding.json').read_bytes()==git(R,'show',actual+':'+REC+'t179pd-ni-iobs-l01-source-packet-20261018.json')
for pin in packet['sourcePins']:
 b=frozen(pin['path']);assert b==(R/pin['path']).read_bytes()==git(C,'show',carrier+':'+pin['path']) and h(b)==pin['sha256'] and len(b)==pin['bytes'] and blob(b)==pin['gitBlob']
assert r['helperSource']==frozen(REC+'t179pd-ni-iobs-l01-process-first-run.py').decode()
assert not git(R,'diff','--name-only',compiled,carrier,'--','.',':!specs/1536-complete-wound-materialization')
af=R/info['archive']['path'];a=read(af);assert h(af.read_bytes())==info['archive']['sha256']=='12c6c0e7c9d09593a5ee809dadd2439b0136934fe4a7f87c5d3263a69ff0066e' and len(a['files'])==a['rawChainCount']==11 and a['actualSources']==[actual] and a['compiledSources']==[compiled] and a['nativeExits']==[1]
paths=set();rawtotal=0
for f in a['files']:
 q=af.parent/f['storedPath'];b=q.read_bytes();assert b==git(C,'show',carrier+':'+str(q.relative_to(R))) and h(b)==f['storedSha256'] and len(b)==f['storedBytes'];raw=gzip.decompress(b);assert raw==Path(f['rawPath']).read_bytes() and len(raw)==f['bytes'] and h(raw)==f['sha256'];assert f['rawPath'] not in paths;paths.add(f['rawPath']);rawtotal+=len(raw)
assert len(r['resultDirectories'])==1;rd=Path(r['resultDirectories'][0]);assert paths=={str(x) for x in p.parent.iterdir() if x.is_file()}|{str(x) for x in rd.rglob('*') if x.is_file()}|{r['firstSentinel']}
s=read(rd/'summary.json');plan=read(rd/'plan.json');assert s['Tests']==dict(Total=2,Executed=2,Passed=1,Failed=1) and s['Selection']==dict(PlannedDescriptors=1,CompletedDescriptors=1,PlannedCases=2,CompletedCases=2,UnderfilledDescriptors=[],Complete=True) and s['ExitCode']==1 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'] and not s['DuplicateTests']
prep=read(r['prepRecord']);assert h(Path(r['prepRecord']).read_bytes())==r['prepRecordSha256'] and prep['mode']=='remaining-plan';assert r['binaryBefore']==r['binaryAfter']==prep['binaryAfter'] and len(r['binaryAfter'])==9 and r['allOutputInventoryBefore']==r['allOutputInventory']==prep['allOutputInventory'] and len(r['allOutputInventory'])==1575
for f in r['allOutputInventory']:
 b=(R/f['path']).read_bytes();assert len(b)==f['bytes'] and h(b)==f['sha256']
assert r['namespaceBefore']==r['namespaceAfter']==dict(ownedRoots=[]) and r['processesBefore']==r['processesAfter']==[]
t=E.parse(next(rd.rglob('*.trx'))).getroot();ns={'t':t.tag.split('}')[0][1:]};us=t.findall('.//t:UnitTestResult',ns);assert len(us)==2 and {u.attrib['testName'] for u in us}=={x['Name'] for x in plan['Descriptors'][0]['ExpectedMethodCounts']}
rawcount=0
def raw(x):
 global rawcount
 if x is None:return None
 b=base64.b64decode(x['Bytes'],validate=True);assert len(b)==x['Length'] and h(b).upper()==x['Sha256'];rawcount+=1;return b
def walk(x):
 if isinstance(x,dict):
  if set(x)=={'Bytes','Length','Sha256'}:raw(x)
  else:
   for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
cases=[];diagnosis={}
for u in us:
 rows=[json.loads(l) for l in u.find('t:Output/t:StdOut',ns).text.splitlines() if l.startswith('{')];by=collections.defaultdict(list)
 for x in rows:
  walk(x)
  if 'Boundary'in x:by[x['Boundary']].append(x['Observation'])
 def one(k):assert len(by[k])==1,(k,len(by[k]));return by[k][0]
 o=one('fr063_joint_completion_actual_owner_outcome');assert o['Failure'] is None and o['ObserverFailure'] is None and o['ActualTurn']==42 and o['liveCallerReturned'] and o['entered'] and o['liveWaitEntered']
 clean=next(x for x in rows if 'CleanupOwnedRoot'in x);assert clean['OwnedFixtureRemoved'] and clean['CleanupFailure'] is None and not Path(clean['CleanupOwnedRoot']).exists()
 if u.attrib['outcome']=='Failed':
  assert u.attrib['testName'].endswith('GuaranteedTreatment_PublishedInterfaceCannotExcuseOtherImmutableReadyDrift')
  message=u.find('t:Output/t:ErrorInfo/t:Message',ns).text;stack=u.find('t:Output/t:ErrorInfo/t:StackTrace',ns).text;assert 'Assert.Contains() Failure'in message and 'JointInterfaceSourceRaces.cs:line 117'in stack
  mem=one('fr063_joint_in_memory_owner_snapshot_before_filesystem');assert mem['CanonicalDisposition']=='PublicationUncertain' and mem['IssueCount']==mem['ReportedIssueCount']==1 and not mem['IssueRowsTruncated']
  issue=mem['Issues'][0];expected='joint_interface_ready_submitted_source_changed: game_state/player/experience.json';assert issue['Message']['Value']==expected and issue['Code']['Value']=='mortal_joint_repair_admission_refused'
  for k in ['Code','Message','FilePath']:
   field=issue[k];assert not field['Truncated'] and len(field['Value'])==field['OriginalLength'] and h(field['Value'].encode()).upper()==field['FullUtf8Sha256']
  assert o['EngineDiagnostics']=='Engine diagnostics:\nCritical accepted-turn raw state corruption after обработки хода: 1 errors'
  drift=one('iobs_actual_published_ready_noninterface_drift');assert drift['Path']=='game_state/player/experience.json' and raw(drift['After'])==raw(drift['Before'])+b' '
  commits=by['iobs_actual_result_commit'];assert [x['Record']['state'] for x in commits]==['Prepared','Published'] and drift['Record']==commits[1]['Record'] and raw(commits[1]['Interface'])==base64.b64decode(drift['Record']['afterImage']['base64'])
  member=one('iobs_actual_committed_member');assert member['InterfaceChanged'] and not member['MarkerCommitted'] and not by['iobs_actual_completed_capture'] and not by['iobs_actual_source_race_held_no_repair'] and not by['iobs_actual_direct_and_outer_restore_retention'] and not by['fr063_joint_complete_union']
  mechanical=one('iobs_actual_public_correction_before_ready')['Mechanical'];checks=[]
  for path,before in mechanical.items():
   want=drift['After'] if path==drift['Path'] else before;data=raw(want);actual_image=o['After'].get(path)
   if data is None:assert actual_image is None or not actual_image['Exists']
   else:assert actual_image['Exists'] and actual_image['Length']==len(data) and actual_image['Sha256']==h(data).upper()
   checks.append(path)
  diagnosis=dict(failedMethod=u.attrib['testName'],message=message,stack=stack,inMemoryDisposition=mem['CanonicalDisposition'],retainedIssue=issue,loggerDiagnostic=o['EngineDiagnostics'],loggerDiagnosticLength=len(o['EngineDiagnostics']),collectorTruncationExplainsFailure=False,publishedPositiveReach=True,immutableReadyFaultIsWhitespaceOnly=True,earlierAssertionsReached=['one physical mutation','no completed capture or common marker','turn42','fresh consumed','exact retained pending','exact foreign experience bytes','Published and exact I','original Ready source not reseeded','attempt not live/delegated; continuation/reservation null; stages empty; capabilities expired'],downstreamAssertionsNotReached=['final mechanical-before foreach','iobs_actual_source_race_held_no_repair emission','direct RestorePreTurnBackup hold/byte-retention oracle'],independentRawMechanicalDigestComparison=dict(paths=len(checks),allExpectedOriginalOrInjectedForeignBytesMatchRecordedFinalDigests=True,distinction='This independent comparison does not claim the failed test reached its later mechanical assertion loop.'),causalPath=['Ready.cs Recheck throws original InvalidDataException with immutable source path','ConsumeCurrentMortalJointInterfaceAsync propagates before optional HOT custody','PublishCurrentMortalJointRepairAsync filtered catch closes attempt and retains original error.Message as one admission issue','No original exception logging in that catch; no rethrow to the previously corrected canonical-refresh catch','ValidateMortalJointPostPublicationAsync returns Held at its initial non-Committed gate before its logging paths'],noNewRuntimePass=True)
 else:
  assert u.attrib['testName'].endswith('GuaranteedTreatment_ActualReturnedReadCannotHideChangedWriterPreimage') and u.find('t:Output/t:ErrorInfo',ns) is None
  state=one('iobs_actual_source_race_held_no_repair');assert state['scenario']=='ReadRace' and 'joint_interface_consumed_read_preimage_changed'in state['Diagnostics'] and not state['ResultCommits'] and not state['Trace']
  direct=one('iobs_actual_direct_and_outer_restore_retention');assert direct['Before']==direct['After'] and direct['ActualOuterDisposition']=='MortalJointRepairPublicationHeldBlocked'
 cases.append(dict(method=u.attrib['testName'],nativeOutcome=u.attrib['outcome'],duration=u.attrib['duration'],cleanup=clean,independentlyQualifiedAsPassHere=False))
censuses=[]
for ref in info['supplementalAuthoritativeCensuses']:
 b=frozen(ref['path']);assert h(b)==ref['sha256'] and len(b)==ref['bytes'] and b==git(C,'show',carrier+':'+ref['path']);c=json.loads(b);assert c['observerVersion']==2 and c['observerSourceSha256']=='141a2020436ec04099afa76affba05a1d485a97a9e05c16ad3d1201ca4103c1f' and c['cleanBoundary'] and not any(c[k] for k in ['activeTaskGameTestProcesses','unknown','ownedRoots','confirmedGone']) and len(c['entries'])==len(c['pidsEnumerated'])==43 and all(x['status']=='MATCH' for x in c['knownVisibility'])
 for x in c['entries']:
  def identity(v):return int(v.split(' ',1)[0]),int(v.rsplit(')',1)[1].split()[19])
  assert identity(x['statBefore'])==identity(x['statAfter'])==(x['pid'],x['startTick'])
 censuses.append(dict(path=ref['path'],sha256=ref['sha256'],startedUtc=c['startedUtc'],finishedUtc=c['finishedUtc'],entries=43,clean=True))
receipt=read('/tmp/1536-cloud-tools/carrier-recovery-8f94b4b2.json');changed=git(R,'diff','--name-only',actual,carrier).decode().splitlines();assert receipt['sha']==carrier and receipt['base']==actual and len(changed)==receipt['changedPathsVerified']==18 and set(changed)=={x['path'] for x in receipt['files']}
for f in receipt['files']:
 b=frozen(f['path']);assert b==git(C,'show',carrier+':'+f['path']) and h(b)==f['sha256'] and len(b)==f['bytes'] and blob(b)==f['gitBlob'] and git(C,'ls-tree',carrier,'--',f['path']).split(b'\t')[0].decode().split()==[f['mode'],'blob',f['gitBlob']]
out=dict(kind='INDEPENDENT_RACES2_FROZEN_FAILURE_CAUSAL_CHECK',checkedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),carrier=carrier,actualSource=actual,compiledSource=compiled,recordSha256=h(p.read_bytes()),archive=dict(path=info['archive']['path'],sha256=h(af.read_bytes()),rawChains=11,rawBytes=rawtotal,completeOriginalCoverage=True,allOriginalStoredRestoredBytesMatched=True),sourcePinCount=98,sourcePinDigest=packet['pinDigest'],nativeExit=1,wallSeconds=r['wallSeconds'],tests=s['Tests'],selection=s['Selection'],noTimeout=True,binaries9AndAll1575Unchanged=True,rawImageChecks=rawcount,cases=cases,diagnosis=diagnosis,nativeCleanup=True,censuses=censuses,afterCensusGapSeconds=(dt.datetime.fromisoformat(censuses[1]['startedUtc'])-dt.datetime.fromisoformat(r['finishedUtc'])).total_seconds(),restoredCarrierPaths=18,attribution='PRODUCT_DIAGNOSTIC_TRANSPORT_OMISSION_IN_CANONICAL_PUBLICATION_CATCH',execution='All further actuals held; no retry/source edit/test/build/observer execution or process intervention by reviewer.')
op=D/'t179pd-ni-iobs-l01-races2-failure-independent-check-8f94b4b2-20261018.json';op.write_text(json.dumps(out,indent=2,ensure_ascii=False)+'\n');print(json.dumps(dict(path=str(op),sha256=h(op.read_bytes()),bytes=op.stat().st_size,rawImageChecks=rawcount,mechanicalComparisonPaths=diagnosis['independentRawMechanicalDigestComparison']['paths'])))
