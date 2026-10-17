"""UNRUN fixed136 zero-test correction preparation. Explicit Root release required."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn-1559')
mode,label,actual=sys.argv[1:4]
assert mode in {'stage-build-plan','guards-plan'} and re.fullmatch(r'gm1559-lifecycle-ownerfix-prep-first-[a-z0-9-]+',label)
assert re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1559-relay-long-sessions/lifecycle-source-packet.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert binding['sourcePinCount']==136 and binding['productDelta']==[]
for pin in binding['sourcePins']:
 f=R/pin['path'];b=f.read_bytes() if f.is_file() else git('show','HEAD:'+pin['path'])
 assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
selection='tests/selection.json' if mode=='stage-build-plan' else 'tests/selections/1559-operational-lifecycle-guards-red.json'
ids=[x['category'] for x in json.loads((R/selection).read_text())['selections']];assert len(ids)==len(set(ids))==(9 if mode=='stage-build-plan' else 4)
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
def binaries():return [dict(path=p,bytes=(R/p).stat().st_size,sha256=h((R/p).read_bytes())) for p in paths if (R/p).is_file()]
before=binaries();compiled=actual;argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',selection,'-PlanOnly']
if mode=='stage-build-plan':assert len(sys.argv)==4 # Deliberately builds both selected projects; NOT NoBuild.
else:
 assert len(sys.argv)==5;prep=json.loads(Path(sys.argv[4]).read_text());assert prep['mode']=='stage-build-plan' and prep['nativeExit']==0 and prep['actualTestsExecuted']==0 and prep['binaryAfter']==before and len(before)==9
 compiled=prep['actualSource'];assert all(p.startswith('specs/1559-relay-long-sessions/') for p in git('diff','--name-only',compiled,actual).decode().splitlines());argv+=['-NoBuild']
out=Path('/tmp/gm1559-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old_results=set(result_root.iterdir()) if result_root.exists() else set()
patterns=['boe-relay-operational-helper-*','boe-gameengine-turnlifecycle-*','boe-live-turn-prep-*','boe-save-load-tests-*','boe-relay1559-*','boe-prompt-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pat in patterns for p in Path(tempfile.gettempdir()).glob(pat)),retainedNativeTerminal=sorted(str(p) for p in (R/'TestResults/native-terminal').iterdir()) if (R/'TestResults/native-terminal').exists() else [])
ns_before=namespace();start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(actual+'-lifecycle-ownerfix-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_ZERO_TEST_DECLARING_OWNER_CORRECTION_PREPARATION',actualSource=actual,mode=mode,label=label,startedUtc=start,originalUnexpected='0771e12578ae3a27adef1d8b4f2c998594d1eb2b'),f,indent=2)
print('FIRST_OWNERFIX_PREP_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=start),indent=2)+'\n');native=process.wait()
after=binaries();results=sorted(str(p) for p in set(result_root.iterdir())-old_results) if result_root.exists() else []
helper_relative='Launcher/GM_Turn_Helper.ps1';helper_source=(R/'BookOfEternityClient'/helper_relative).read_bytes();main=R/'BookOfEternityClient/bin/Debug/net8.0'
inventory=[dict(path=str(p.relative_to(main)),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for p in sorted(main.rglob('*')) if p.is_file()]
record=dict(kind='FIRST_DECLARING_OWNER_CORRECTION_ZERO_TEST_PREP_NOT_RUNTIME_ACCEPTANCE',mode=mode,label=label,actualSource=actual,compiledSource=compiled,productSource='a82b3d8439f76f83baade6df8516e78eea86e546',originalUnexpectedCarrier='0771e12578ae3a27adef1d8b4f2c998594d1eb2b',sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinCount=136,sourcePinDigest=binding['sourcePinDigest'],argv=argv,cwd=str(R),selection=selection,selectionSha256=h((R/selection).read_bytes()),startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,actualTestsExecuted=0,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=namespace(),managedOutputInventory=inventory,repoHelperSha256=h(helper_source),managedHelperSha256=h((main/helper_relative).read_bytes()) if (main/helper_relative).exists() else None,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='Only fixed fresh selected-stage build+PlanOnly then guardPlanOnly-NoBuild; unchanged catalog/selectors/methods mean no repeated catalog/npm/oldbaseline actual. ZERO tests; Root separate release required. FIRST unexpected STOP preserved, no automatic retry or mutation.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_OWNERFIX_PREP_NATIVE',native,'record',str(out/'record.json'),flush=True)
if native==0:
 assert len(after)==9 and record['headAfter']==actual and not record['statusAfter'] and ns_before==record['namespaceAfter']
 assert record['repoHelperSha256']==record['managedHelperSha256']
 for result in results:
  summary=json.loads((Path(result)/'summary.json').read_text());assert summary['Tests']['Executed']==0
 if mode=='guards-plan':assert before==after
sys.exit(native)
