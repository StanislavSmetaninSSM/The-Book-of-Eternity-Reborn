"""Fixed FIRST NI_IOBS_L01_ORDER tests-only zero-test preparation. Root release required; never runs test cases."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn')
mode,label,actual=sys.argv[1:4]
assert mode in {'catalog','plan'} and re.fullmatch(r't179pd-ni-iobs-l01-order-prep-first-[a-z0-9-]+',label)
assert re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1536-complete-wound-materialization/recovery/t179pd-ni-iobs-l01-source-packet-20261018.json'
binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert len(binding['sourcePins'])==44 and binding['productDelta']==[]
ledger=json.loads((R/binding['ledger']['path']).read_text())
assert ledger['staticTotals']==dict(newMethods=1,newRows=1,newOwners=1,predictedDescriptors=1)
assert not git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge')
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h(json.dumps(binding['sourcePins'],sort_keys=True,separators=(',',':')).encode())==binding['pinDigest']
assert h((R/binding['ledger']['path']).read_bytes())==binding['ledger']['sha256']
selection='tests/selection.json';ids=[x['category'] for x in json.loads((R/selection).read_text())['selections']]
assert ids==['fr063-joint-interface-lifecycle-order']
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
def binaries():return [dict(path=p,bytes=(R/p).stat().st_size,sha256=h((R/p).read_bytes())) for p in paths if (R/p).is_file()]
before=binaries();compiled=actual
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']
if mode=='catalog':
 assert len(sys.argv)==4;argv+=['-ValidateCatalog'] # actual canonical audit builds both test projects before discovery
else:
 assert len(sys.argv)==5;prep=json.loads(Path(sys.argv[4]).read_text())
 assert prep['mode']=='catalog' and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
 assert prep['actualSource']==actual and len(before)==9 and prep['binaryAfter']==before
 argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']
out=Path('/tmp/t179pd-ni-iobs-l01-order-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old_results=set(result_root.iterdir()) if result_root.exists() else set()
patterns=['boe-gameengine-turnlifecycle-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pat in patterns for p in Path(tempfile.gettempdir()).glob(pat)))
ns_before=namespace();assert ns_before==dict(ownedRoots=[]),ns_before
start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic()
sentinel=out.parent/(actual+'-ni-iobs-l01-order-zero-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NI_IOBS_L01_ORDER_ZERO_TEST_PREPARATION',mode=mode,actualSource=actual,label=label,startedUtc=start),f,indent=2)
(out/'source-binding.json').write_bytes(binding_raw)
print('FIRST_NI_IOBS_L01_ORDER_PREP_START',mode,actual,str(out/'stdout.log'),flush=True)
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
record=dict(kind='FIRST_NI_IOBS_L01_ORDER_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE',mode=mode,label=label,actualSource=actual,compiledSource=compiled,productSource=binding['productBase'],sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinCount=len(binding['sourcePins']),sourcePinDigest=binding['pinDigest'],argv=argv,cwd=str(R),selection=selection,selectionSha256=h((R/selection).read_bytes()),startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,actualTestsExecuted=0,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=namespace(),managedOutputInventory=managed,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='Fixed catalog BOTH builds/discovery then exact stage PlanOnly-NoBuild. ZERO test cases. No retry/product edit. Only FIRST real I ordering singleton: static1owner1method1case until actual discovery. Remaining finite controls not authored/admitted. FIRST actual separately named after concrete SOURCE/PREP; no product, retry or old native.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
print('FIRST_NI_IOBS_L01_ORDER_PREP_NATIVE',native,'record',str(out/'record.json'),flush=True)
if native==0:
 assert len(after)==9 and record['headAfter']==actual and not record['statusAfter'] and ns_before==record['namespaceAfter']
 assert len({after[i]['sha256'] for i in [0,2,6,8]})==1 and after[4]['sha256']==after[5]['sha256']
 for result in results:
  summary=Path(result)/'summary.json'
  if summary.exists():assert json.loads(summary.read_text())['Tests']['Executed']==0
 if mode=='plan':
  assert before==after
  plans=[json.loads((Path(r)/'plan.json').read_text()) for r in results if (Path(r)/'plan.json').is_file()]
  assert len(plans)==1
  (out/'observed-plan.json').write_text(json.dumps(plans[0],indent=2)+'\n')

if native==0 and mode=='plan':
 descriptors=plans[0]['Descriptors'];assert len(descriptors)==1
 descriptor=descriptors[0];assert descriptor['Category']=='fr063-joint-interface-lifecycle-order' and descriptor['Adapter']=='integration' and descriptor['EstimatedCases']==1
 assert descriptor['ExpectedMethodCounts']==[dict(Name='BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_InterfacePublicationPrecedesCommonAndLateConsumesOnce',Cases=1)]

sys.exit(native)
