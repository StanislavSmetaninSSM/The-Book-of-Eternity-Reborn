"""Fixed FIRST corrected NI-S01 ordinary Pd.3d zero-test preparation; original56 raw is retained. Root release required; never runs test cases."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn')
label,actual=sys.argv[1:3];mode='build-plan'
assert len(sys.argv)==3 and re.fullmatch(r't179pd3d-nis01-prep-first-[a-z0-9-]+',label)
assert re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1536-complete-wound-materialization/recovery/t179pd3d-ordinary-tests-source-packet-20261017.json'
binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert len(binding['sourcePins'])==64 and binding['productDelta']==[]
assert not git('diff','--name-only',binding['baseline'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge')
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h(json.dumps(binding['sourcePins'],ensure_ascii=False,separators=(',',':')).encode())==binding['sourcePinDigest']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
selection='tests/selection.json';ids=[x['category'] for x in json.loads((R/selection).read_text())['selections']]
assert len(ids)==len(set(ids))==5 and all(x.startswith('fr063-ordinary-output-') for x in ids)
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
def binaries():return [dict(path=p,bytes=(R/p).stat().st_size,sha256=h((R/p).read_bytes())) for p in paths if (R/p).is_file()]
before=binaries();compiled=actual
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',selection,'-PlanOnly'] # No NoBuild: freshly builds selected unit/integration and discovers; ZERO cases.
out=Path('/tmp/t179pd3d-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old_results=set(result_root.iterdir()) if result_root.exists() else set()
patterns=['boe-pd3d-ordinary-*','boe-narrative-validation-*','boe-interface-normalization-*','boe-resource-materialization-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pat in patterns for p in Path(tempfile.gettempdir()).glob(pat)))
ns_before=namespace();start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic()
sentinel=out.parent/(actual+'-pd3d-nis01-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_PD3D_NIS01_ZERO_TEST_PREPARATION',mode=mode,actualSource=actual,label=label,startedUtc=start),f,indent=2)
(out/'source-binding.json').write_bytes(binding_raw)
print('FIRST_PD3D_NIS01_PREP_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT)
 proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=start),indent=2)+'\n')
 native=process.wait()
after=binaries();results=sorted(str(p) for p in set(result_root.iterdir())-old_results) if result_root.exists() else []
main=R/'BookOfEternityClient/bin/Debug/net8.0'
managed=[dict(path=str(p.relative_to(main)),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for p in sorted(main.rglob('*')) if p.is_file()]
record=dict(kind='FIRST_PD3D_NIS01_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE',mode=mode,label=label,actualSource=actual,compiledSource=compiled,productSource=binding['baseline'],sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinCount=len(binding['sourcePins']),sourcePinDigest=binding['sourcePinDigest'],argv=argv,cwd=str(R),selection=selection,selectionSha256=h((R/selection).read_bytes()),startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,actualTestsExecuted=0,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=namespace(),managedOutputInventory=managed,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='Single fresh selected BOTH build+PlanOnly without NoBuild; unchanged catalog/selection/method inventory already audited on56, no catalog replay. ZERO test cases, actual boundary2 and product held. Original18 prep raw and initialSOURCE_NOT_READY retained; no retry. Old World fence intact/unselected/HELD.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
print('FIRST_PD3D_NIS01_PREP_NATIVE',native,'record',str(out/'record.json'),flush=True)
if native==0:
 assert len(after)==9 and record['headAfter']==actual and not record['statusAfter'] and ns_before==record['namespaceAfter']
 for result in results:
  summary=Path(result)/'summary.json'
  if summary.exists():assert json.loads(summary.read_text())['Tests']['Executed']==0
 if mode=='plan':assert before==after
sys.exit(native)
