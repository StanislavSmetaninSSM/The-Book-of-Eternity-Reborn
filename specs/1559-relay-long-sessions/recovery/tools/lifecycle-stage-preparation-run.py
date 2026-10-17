"""UNRUN inactive lifecycle production selected-build/PlanOnly preparation. Explicit Root release required."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn-1559')
label,actual=sys.argv[1:];mode='stage-build-plan'
assert re.fullmatch(r'gm1559-lifecycle-stage-prep-first-[a-z0-9-]+',label)
assert re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1559-relay-long-sessions/lifecycle-source-packet.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert binding['sourcePinCount']==136
expected_product_paths=set(['BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs', 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs', 'BookOfEternityClient/Services/LiveTurnPreparationService.cs', 'BookOfEternityClient/Services/SaveLoadService.cs', 'BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs', 'BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs', 'BookOfEternityClient/Services/SpiritualOriginalDraftInputs.cs', 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1', 'tools/gm-relay/README.md'])
assert set(binding['productDelta'])==expected_product_paths
actual_product_paths={p for p in git('diff','--name-only',binding['baselineProduct'],actual).decode().splitlines() if p.startswith(('BookOfEternityClient/','BookOfEternityGMBridge/','tools/gm-relay/'))}
assert actual_product_paths==expected_product_paths

for pin in binding['sourcePins']:
 f=R/pin['path'];b=f.read_bytes() if f.is_file() else git('show','HEAD:'+pin['path'])
 assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
selection='tests/selection.json'
ids=[x['category'] for x in json.loads((R/selection).read_text())['selections']];assert len(ids)==len(set(ids))==9
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
def binaries():return [dict(path=p,bytes=(R/p).stat().st_size,sha256=h((R/p).read_bytes())) for p in paths if (R/p).is_file()]
before=binaries();compiled=actual;argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',selection,'-PlanOnly']
assert len(sys.argv)==3 # Fresh selected unit+integration builds; no NoBuild, catalog or second plan.
out=Path('/tmp/gm1559-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old_results=set(result_root.iterdir()) if result_root.exists() else set()
patterns=['boe-relay-operational-helper-*','boe-gameengine-turnlifecycle-*','boe-live-turn-prep-*','boe-save-load-tests-*','boe-relay1559-*','boe-prompt-*','boe-gm1557-package-*','boe-gm1557-session-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pat in patterns for p in Path(tempfile.gettempdir()).glob(pat)),retainedNativeTerminal=sorted(str(p) for p in (R/'TestResults/native-terminal').iterdir()) if (R/'TestResults/native-terminal').exists() else [])
ns_before=namespace();start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(actual+'-lifecycle-stage-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_ZERO_TEST_INACTIVE_LIFECYCLE_PRODUCTION_PREPARATION',actualSource=actual,mode=mode,label=label,startedUtc=start,originalUnexpected='0771e12578ae3a27adef1d8b4f2c998594d1eb2b'),f,indent=2)
print('FIRST_LIFECYCLE_STAGE_PREP_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=start),indent=2)+'\n');native=process.wait()
after=binaries();results=sorted(str(p) for p in set(result_root.iterdir())-old_results) if result_root.exists() else []
helper_relative='Launcher/GM_Turn_Helper.ps1';helper_source=(R/'BookOfEternityClient'/helper_relative).read_bytes();main=R/'BookOfEternityClient/bin/Debug/net8.0'
inventory=[dict(path=str(p.relative_to(main)),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for p in sorted(main.rglob('*')) if p.is_file()]
record=dict(kind='FIRST_INACTIVE_LIFECYCLE_PRODUCTION_ZERO_TEST_PREP_NOT_RUNTIME_ACCEPTANCE',mode=mode,label=label,actualSource=actual,compiledSource=compiled,baselineProduct=binding['baselineProduct'],productSource=compiled,productDelta=binding['productDelta'],originalUnexpectedCarrier='0771e12578ae3a27adef1d8b4f2c998594d1eb2b',sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinCount=136,sourcePinDigest=binding['sourcePinDigest'],argv=argv,cwd=str(R),selection=selection,selectionSha256=h((R/selection).read_bytes()),startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,actualTestsExecuted=0,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=namespace(),managedOutputInventory=inventory,repoHelperSha256=h(helper_source),managedHelperSha256=h((main/helper_relative).read_bytes()) if (main/helper_relative).exists() else None,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='Only fresh selected unit+integration build+stage47PlanOnly; eight exact inactive-reservation owners+README explicitly bound. Catalog/selectors/methods unchanged, no catalog/npm or actual test execution. ZERO tests; Root separate release required. FIRST unexpected STOP preserved, no automatic retry or mutation.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_LIFECYCLE_STAGE_PREP_NATIVE',native,'record',str(out/'record.json'),flush=True)
if native==0:
 assert len(after)==9 and record['headAfter']==actual and not record['statusAfter'] and ns_before==record['namespaceAfter']
 assert record['repoHelperSha256']==record['managedHelperSha256']
 for result in results:
  summary=json.loads((Path(result)/'summary.json').read_text());assert summary['Tests']['Executed']==0
 current_plan=json.loads((Path(results[0])/'plan.json').read_text());old_record=json.loads(Path('/tmp/gm1559-implementation-tools/gm1559-lifecycle-ownerfix-prep-first-stage-build-plan-fb6f8989-20261017/record.json').read_text());old_plan=json.loads((Path(old_record['resultDirectories'][0])/'plan.json').read_text())
 assert all(current_plan[k]==old_plan[k] for k in ['Categories','Reasons','Descriptors'])
 methods=[m for d in current_plan['Descriptors'] for m in d['ExpectedMethodCounts']];assert len(current_plan['Categories'])==9 and len(current_plan['Descriptors'])==10 and len(methods)==15 and sum(m['Cases'] for m in methods)==47
sys.exit(native)
