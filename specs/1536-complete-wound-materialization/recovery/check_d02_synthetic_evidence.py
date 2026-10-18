"""Read original D02 synthetic evidence only; no controller import/native/procfs."""
from pathlib import Path
import ast, hashlib, json, re, subprocess, zipfile

REPO=Path('/workspace/The-Book-of-Eternity-Reborn')
CLONE=Path('/workspace/1536-cloud-clean-restore')
REC=REPO/'specs/1536-complete-wound-materialization/recovery'
OUT=Path('/tmp/1536-cloud-tools/review')
CANDIDATE='be89497c746d8393470d2ff9789c262c1d60ed5a'
ACTUAL='237a26c39d5530933ff844209349d20bc8303111'
COMPILED='3a36b1f1b790205099f6c940b0e362e456fa0975'
CONTROLLER='42837634637f1e0ee5c3752147263bdd7f5388775abf741c033ff55f937e26b6'
BINDINGS='0565587f35dd6fca647e0235141ffe9f5cc98f0ac3485173d2f286ec239d33ff'
ROOT=Path('/tmp/l01d02-b796a0e4bf514a62')
checks=[]
def check(name,condition,detail=None):
 row={'name':name,'passed':bool(condition)}
 if detail is not None:row['detail']=detail
 checks.append(row)
def read(p):return Path(p).read_bytes()
def sha(data):return hashlib.sha256(data).hexdigest()
def obj(p):return json.loads(read(p))
def git(*args,cwd=REPO):return subprocess.check_output(['git','-C',str(cwd),*args])
def matches(row,path):
 p=Path(path);return p.is_file() and not p.is_symlink() and p.stat().st_size==row['bytes'] and sha(read(p))==row['sha256']

index_path=REC/'l01-delivery-d02-synthetic-first-20261018.json';index=obj(index_path)
control=obj(REC/'l01-delivery-d02/control.json')
receipt_path=Path('/tmp/1536-cloud-tools/d01-carrier-recovery-be89497c.json');receipt=obj(receipt_path)
packet=obj(REC/'l01-delivery-d01-source-packet-20261018.json')
check('writer and independent clone exact clean',all(git('rev-parse','HEAD',cwd=p).decode().strip()==CANDIDATE and not git('status','--porcelain',cwd=p).strip() for p in [REPO,CLONE]))
check('remote/recovery receipt164paths149pins no copied writer objects or production delta',receipt['sha']==CANDIDATE and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localWriterObjectsCopied'] and receipt['changedPaths']==164 and receipt['currentPinsVerified']==149 and not receipt['productionChanges'])
changed=git('diff','--name-only',ACTUAL,CANDIDATE).decode().splitlines()
check('only four outcome/metadata paths',set(changed)=={str(x.relative_to(REPO)) for x in [index_path,REPO/index['archive']['path'],REC/'l01-delivery-d02/control.json',REC.parent/'plan.md']})
check('four saved paths exact Git and independent clone',all(read(REPO/p)==read(CLONE/p)==git('show',CANDIDATE+':'+p) for p in changed))
check('all149 source pins exact current/clone',len(packet['sourcePins'])==149 and all(matches(x,REPO/x['path']) and matches(x,CLONE/x['path']) for x in packet['sourcePins']))
check('pin packet unchanged compiled/actual/current',all(git('show',rev+':'+str((REC/'l01-delivery-d01-source-packet-20261018.json').relative_to(REPO)))==read(REC/'l01-delivery-d01-source-packet-20261018.json') for rev in [COMPILED,ACTUAL]))
check('exact accepted controller/bindings',sha(read(REC/'l01-delivery-d02/phase.py'))==CONTROLLER and sha(read(REC/'l01-delivery-d02/bindings.json'))==BINDINGS)
release=json.loads(git('show',ACTUAL+':'+str((REC/'l01-delivery-d02/control.json').relative_to(REPO))))['synthetic']
review=obj(REPO/index['prepReviewPath'])
check('actual had saved exact one-shot independent PREP release',release['status']=='PREP_ACCEPTED_SYNTHETIC_0_7_ONCE' and release['reviewPath']==index['prepReviewPath'] and release['reviewSha256']==index['prepReviewSha256']==sha(read(REPO/index['prepReviewPath'])) and review['verdict']=='ACCEPT_D02_COMPOSITE_PREP_SYNTHETIC_0_7_ONCE' and review['controllerSha256']==CONTROLLER and review['bindingsSha256']==BINDINGS and release['prepRecords']==review['prepRecords'])
check('three original PREP record hashes still exact',all(sha(read(ROOT/name/'record.json'))==h for name,h in release['prepRecords'].items()))
prepared=obj(ROOT/'prepare/record.json')
check('four compiled outputs exact admitted PREP',prepared['source']==COMPILED and len(prepared['outputs'])==4 and all(matches(x,x['path']) for x in prepared['outputs']) and prepared['outputs'][0]['sha256']=='ec59272038423c31a7c63959977c79136d6634b85b460c89f4955991f3094d7c')
for relative in ['BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs','specs/1536-complete-wound-materialization/recovery/l01-delivery-d01/Live.cs','specs/1536-complete-wound-materialization/recovery/l01-delivery-d01/phase.py']:
 check('compiled/actual/current source unchanged '+Path(relative).name,read(REPO/relative)==git('show',COMPILED+':'+relative)==git('show',ACTUAL+':'+relative))
archive=REPO/index['archive']['path'];z=zipfile.ZipFile(archive)
check('archive exact12470B hash Git restored',len(read(archive))==12470==index['archive']['bytes'] and sha(read(archive))==index['archive']['sha256']=='514e464f3b836736f8187ab0daefcbcd5625d7ea71783fa1ae9e95ae958f7c3a' and read(archive)==read(CLONE/index['archive']['path'])==git('show',CANDIDATE+':'+index['archive']['path']))
check('exact18 unique original members',len(z.namelist())==len(set(z.namelist()))==len(index['archive']['members'])==18 and set(z.namelist())=={x['member'] for x in index['archive']['members']})
for row in index['archive']['members']:
 data=z.read(row['member']);check('original chain '+row['member'],len(data)==row['bytes'] and sha(data)==row['sha256'] and data==read(row['originalPath']))
record=json.loads(z.read('record.json'))
check('first aggregate record exact source/hash and verification result',sha(z.read('record.json'))==index['recordSha256']=='9ceee126edea7ed15f4aa72dba88de475dab46a3c658058807b4d7e82836d898' and record['source']==ACTUAL and record['controllerSha256']==CONTROLLER and record['bindingsSha256']==BINDINGS and record['nativeExit']==0 and record['observerErrors']==[] and record['status']=='PASS_OWN_SYNTHETIC_DELIVERY2_ONLY' and record['durationSeconds']==index['durationSeconds'])
check('two targets and two reader hosts only; no game/test/API',record['fixtureSubprocessTargets']==record['readerHarnessProcesses']==2 and len(record['cases'])==2 and all(record[x]==0 for x in ['testsExecuted','gameProcesses','diagnosticRequests']) and [c['expectedTargetNativeExit'] for c in record['cases']]==[0,7])
check('dated before/after tools clean with preserved visibility limit',record['afterLiveTools']==[] and record['beforeTools']==record['afterTools']==[{'comm':'dotnet','state':'Z','count':35}] and record['beforeVisibility']==record['afterVisibility']=={'executableFallbacks':5,'positivelyVanished':0})
check('original normal899 unchanged observations and172inputs bound',record['originalNormal899UnchangedBefore'] and record['originalNormal899UnchangedAfter'] and record['verifiedInstalledInputs']=={'count':172,'sha256':'1ee529f929e54f79a2f3b2c7a516ef0afeb6cc0b32cfb9a73f174dd44b392cce'})
base=['/tmp/1536-cloud-tools/dotnet/dotnet','exec','--runtimeconfig',str(ROOT/'prepare/D01Live.runtimeconfig.json'),str(ROOT/'prepare/D01Live.dll')]
identities=[];case_details=[]
for case in record['cases']:
 exit=case['expectedTargetNativeExit'];prefix='exit'+str(exit);case_path=ROOT/'synthetic'/prefix
 initial=json.loads(z.read(prefix+'/started.json'))
 reader_lines=[json.loads(line) for line in z.read(prefix+'/reader.stdout').decode().splitlines()]
 pulse,terminal=reader_lines
 child=json.loads(z.read(prefix+'/target.stderr'))
 target_id=json.loads(z.read(prefix+'/target-identity.json'));reader_id=json.loads(z.read(prefix+'/reader-identity.json'));manifest=json.loads(z.read(prefix+'/reader-manifest.json'))
 target=case['target'];reader=case['reader'];e=pulse['Event'];h=pulse['Health'];last=terminal['Health']
 check(prefix+' case exactly embedded original',case==json.loads(z.read(prefix+'/record.json')) and case['passed'] and case['primaryFailure'] is None and not case['observerErrors'])
 check(prefix+' first empty case admission recorded',initial['target'] is None and initial['reader'] is None and initial['targetLaunches']==initial['readerLaunches']==0 and initial['nonce']==case['nonce'] and initial['root']==case['root'])
 check(prefix+' exact one target/reader native separation',case['targetLaunches']==case['readerLaunches']==1 and target['nativeExit']==exit and reader['nativeExit']==0 and target['joined'] and reader['joined'] and 'cleanupExit' not in target and 'cleanupExit' not in reader and case['joinedEof'])
 check(prefix+' exact owned commands/root/nonce',target['command']==base+['--child',str(case_path/'fixture'),case['nonce'],str(exit)] and reader['command']==base+['read',str(case_path/'reader-manifest.json')] and case['root']==str(case_path/'fixture') and re.fullmatch('[0-9a-f]{32}',case['nonce']) is not None)
 check(prefix+' launched identities retained unchanged',all(x['command']==y['command'] and x['identity']==y['identity'] and y['nativeExit'] is None and not y['joined'] for x,y in [(target,target_id),(reader,reader_id)]) and target['identity']['pid']==case['targetLaunchPid'] and reader['identity']['pid']==case['readerLaunchPid'])
 for actor in [target,reader]:
  identity=actor['identity'];identities.append((identity['pid'],identity['birthTick']))
  check(prefix+' owned '+('target' if actor is target else 'reader')+' pid/birth/uid/parent',identity['pid']>0 and identity['birthTick']>0 and identity['uid']==1000 and identity['parentPid']==112073)
 expected={'Pid':target['identity']['pid'],'BirthTick':target['identity']['birthTick'],'Root':case['root'],'Nonce':case['nonce']}
 check(prefix+' manifest and emitted identity exact',manifest==expected and all(child[k]==e[k]==v for k,v in expected.items()) and child['ExpectedExit']==exit and e['Cut']==1)
 check(prefix+' two exact ordered reader reports',len(reader_lines)==2 and pulse['Kind']=='D01_LIVE_PULSE' and terminal['Kind']=='D01_READER_TERMINAL' and pulse==case['livePulse'] and terminal==case['terminal'] and child==case['childSelfReport'] and pulse['ReaderPid']==terminal['ReaderPid']==reader['identity']['pid'])
 check(prefix+' live before STOP while raw pending and child alive',case['childAliveBeforeStop'] and pulse['RawPending'] and not h['Eof'] and case['pulseReceivedMonotonic']>0 and terminal['Failure'] is None and last['Eof'] and not last['RawReadFault'])
 check(prefix+' intended exact health survives EOF',h['Candidates']==1+(exit==7) and h['Published']==1 and h['Malformed']==int(exit==7) and h['Uncertain']==(exit==7) and all(h[k]==0 for k in ['Dropped','Oversize','IncompleteTail','IdentityRejected','Duplicates','ElapsedRegressions','TrackingExhausted']) and h['CustodySeen'] and not h['AcceptedSeen'] and not h['LateReturnSeen'] and h['ObserverFailure'] is None and last==dict(h,Eof=True))
 raw=terminal['RawStdout'];lines=raw.splitlines(keepends=True);marker_line=lines[2 if exit==7 else 1].removesuffix('\n')
 marker=json.loads(marker_line)
 expected_prefix='ordinary 🦉\r\n'+('{iobs_current_resume_diagnostic\n' if exit==7 else '')
 check(prefix+' raw decoded Unicode CRLF malformed and suffix exact',raw==expected_prefix+marker_line+'\ndone\n' and z.read(prefix+'/reader.stderr')==b'' and terminal['Runtime']=='.NET 8.0.32')
 check(prefix+' childself/raw hash equality independently recomputed',sha(raw.encode())==terminal['RawStdoutSha256']==child['RawStdoutSha256'] and case['rawStdoutChildHashMatches'])
 check(prefix+' source event content linehash/UTF16 offset exact',marker['Boundary']=='iobs_current_resume_diagnostic' and marker['Stage']==e['Stage']=='finalization-return' and marker['Detail']==e['ControlDetail']=='MortalJointCustodyCaptureCompletedBeforePreparation' and e['Control']=='CUSTODY_RETURN' and e['Disposition']=='IDENTITY_ORDERED' and e['Ordinal']==1+(exit==7) and e['LineOrdinal']==2+(exit==7) and e['CharacterOffset']==len(expected_prefix.encode('utf-16-le'))//2 and e['RawLineSha256'].lower()==sha(marker_line.encode()) and marker['ElapsedMilliseconds']==e['SourceElapsedMilliseconds']==1 and marker['Utc']==e['SourceUtc'])
 check(prefix+' unknown timing/source completeness retained',case['sourceToReceiveLatency']==case['capturePhase']=='UNKNOWN' and terminal['CapturePhase']==terminal['SourceEmissionCompleteness']==h['CapturePhase']==h['SourceEmissionCompleteness']==last['CapturePhase']==last['SourceEmissionCompleteness']=='UNKNOWN')
 case_details.append({'exit':exit,'target':target['identity'],'reader':reader['identity'],'nonce':case['nonce'],'root':case['root'],'rawSha256':terminal['RawStdoutSha256'],'reportedMvid':terminal['Mvid'],'health':h})
check('four distinct owned identities and two isolated nonces/roots',len(identities)==len(set(identities))==4 and len({x['nonce'] for x in record['cases']})==len({x['root'] for x in record['cases']})==2)
check('same compiled runtime MVID reported by both reader hosts',len({c['terminal']['Mvid'] for c in record['cases']})==1 and record['cases'][0]['terminal']['Mvid']=='b392b19e-acad-44d7-93f2-a5347fb3e1d4')
# Static reach conditions explain what the original successful assertions establish.
source=(REC/'l01-delivery-d01/phase.py').read_text();actual=next(x for x in ast.parse(source).body if isinstance(x,ast.FunctionDef) and x.name=='actual_case');segment=ast.get_source_segment(source,actual)
check('live assertion precedes private STOP then joins and terminal oracle',segment.index("assert pulse['RawPending']")<segment.index("target.stdin.write(('STOP:'")<segment.index("target.wait(timeout=5)")<segment.index("record.update(terminal=terminal"))
check('sole inherited pipe reader and exact ownership path source',"stdin=target.stdout" in segment and "target.stdout.close()" in segment and "assert identity['parentPid']==os.getpid() and identity['uid']==os.geteuid()" in segment and "'cmdline'" in segment and "fd=lib['open_owned'](process.pid,identity)" in segment)
check('no retry/game release after consumed first outcome',control['synthetic']['status']=='CONSUMED_FIRST_OUTCOME_PENDING_INDEPENDENT_EVIDENCE' and control['synthetic']['retryRelease']=='NONE' and control['gameplayRelease']==control['crash5Release']==index['gameplayRelease']==index['crash5Release']=='NONE' and index['permit']=='CONSUMED_NO_RETRY')
check('old outcomes and52/57/UNKNOWN unchanged',not any(index[x] for x in ['originalCatalog124Replaced','originalUnit137Replaced','firstBindingFailureReplaced']) and index['L01Qualified']=='52/57' and index['old66529']=='UNKNOWN_UNAVAILABLE_NO_RECOVERY_CLAIM' and record['originalCatalogNativeExit']==124 and record['originalUnitNativeExit']==137 and not record['oldOutcomeReplaced'])
result={'kind':'D02_FIRST_SYNTHETIC_INDEPENDENT_READ_ONLY_CHECKS','candidate':CANDIDATE,'actual':ACTUAL,'compiled':COMPILED,'passed':sum(x['passed'] for x in checks),'findings':[x for x in checks if not x['passed']],'checks':checks,'cases':case_details,'nativeActions':0,'procfsReads':0,'repositoryWrites':0,'indexSha256':sha(read(index_path)),'archiveSha256':index['archive']['sha256'],'recoveryReceiptSha256':sha(read(receipt_path)),'checkerSha256':sha(read(__file__))}
path=OUT/'l01-delivery-d02-synthetic-independent-checks-20261018.json';path.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'path':str(path),'sha256':sha(read(path)),'passed':result['passed'],'findings':result['findings']}))
