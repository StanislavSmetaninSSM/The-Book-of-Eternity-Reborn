"""UNRUN fixed FIRST current-capture stop1 recorder. Explicit Root named actual activation required."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn')
label,actual,prep_path=sys.argv[1:]
assert re.fullmatch(r't179pd-cut01-stop-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1536-complete-wound-materialization/recovery/t179pd-cut01-source-packet-20261018.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert len(binding['sourcePins'])==31 and binding['productDelta']==[]
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
assert h(json.dumps(binding['sourcePins'],ensure_ascii=False,separators=(',',':')).encode())==binding['sourcePinDigest']
assert not git('diff','--name-only',binding['base'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge')
prep=json.loads(Path(prep_path).read_text());assert prep['kind']=='FIRST_CUT01_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE' and prep['mode']=='plan' and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
compiled=prep['compiledSource'];assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in git('diff','--name-only',compiled,actual).decode().splitlines())
plan=json.loads((Path(prep['resultDirectories'][0])/'plan.json').read_text());category='fr063-joint-capture-cut-stop';selected=[d for d in plan['Descriptors'] if d['Category']==category]
assert len(selected)==1 and selected[0]['Adapter']=='integration' and selected[0]['EstimatedCases']==1
assert selected[0]['ExpectedMethodCounts']==[dict(Name='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner',Cases=1)]
def binaries():
 out=[]
 for pin in prep['binaryAfter']:
  p=R/pin['path'];b=p.read_bytes();out.append(dict(path=pin['path'],bytes=len(b),sha256=h(b)))
 return out
before=binaries();assert len(before)==9 and before==prep['binaryAfter']
paths=['boe-gameengine-turnlifecycle-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pattern in paths for p in Path(tempfile.gettempdir()).glob(pattern)))
ns_before=namespace();assert ns_before==dict(ownedRoots=[]),ns_before
out=Path('/tmp/t179pd-cut01-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old=set(result_root.iterdir()) if result_root.exists() else set()
started=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(actual+'-cut01-stop1.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NAMED_CUT01_CURRENT_CAPTURE_STOP1',actualSource=actual,compiledSource=compiled,label=label,startedUtc=started),f,indent=2)
(out/'source-binding.json').write_bytes(binding_raw)
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild']
print('FIRST_CUT01_STOP1_START',actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=started),indent=2)+'\n');native=process.wait()
results=sorted(str(p) for p in set(result_root.iterdir())-old) if result_root.exists() else []
after=binaries();ns_after=namespace()
record=dict(kind='FIRST_CUT01_STOP1_RAW_NOT_CAUSAL_ACCEPTANCE',label=label,actualSource=actual,compiledSource=compiled,productSource=binding['base'],category=category,argv=argv,cwd=str(R),prepRecord=str(prep_path),prepRecordSha256=h(Path(prep_path).read_bytes()),sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinDigest=binding['sourcePinDigest'],sourcePinCount=31,startedUtc=started,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=ns_after,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),expectedDescriptor=selected[0],helperSource=Path(__file__).read_text(),scope='Only first named current capture stop1 on unchanged product. Native1 alone is not causal: genuine Stage43/original/repair/held-request/corrected-ready prerequisites, actual old preparation/common observations and final semantic missing-cut callback assertion plus strict cleanup require independent raw review. No missing-enum/NotNull/setup acceptance; no retry/other owner/product.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_CUT01_STOP1_NATIVE',native,'record',str(out/'record.json'),flush=True)
assert before==after and ns_before==ns_after and record['headAfter']==actual and not record['statusAfter']
sys.exit(native)
