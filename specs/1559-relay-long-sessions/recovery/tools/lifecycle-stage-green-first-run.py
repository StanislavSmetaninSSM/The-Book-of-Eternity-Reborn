"""UNRUN one fixed SelectionFile-NoBuild FIRST inactive lifecycle GREEN47 recorder. Named exact Root activation required."""
from pathlib import Path
import sys,subprocess,json,hashlib,re,datetime,time,tempfile
R=Path('/workspace/The-Book-of-Eternity-Reborn-1559')
label,actual,plan_record=sys.argv[1:]
assert re.fullmatch(r'gm1559-lifecycle-stage-green-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
prep=json.loads(Path(plan_record).read_text());compiled=prep['compiledSource']
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
assert all(p.startswith('specs/1559-relay-long-sessions/') for p in git('diff','--name-only',compiled,actual).decode().splitlines())
prep=json.loads(Path(plan_record).read_text());assert prep['mode']=='stage-build-plan' and prep['nativeExit']==0 and prep['compiledSource']==compiled and prep['actualTestsExecuted']==0
plan_path=Path(prep['resultDirectories'][0])/'plan.json';plan=json.loads(plan_path.read_text())
assert len(plan['Categories'])==9 and len(plan['Descriptors'])==10
methods=[m for d in plan['Descriptors'] for m in d['ExpectedMethodCounts']]
assert len(methods)==15 and sum(m['Cases'] for m in methods)==47
selection='tests/selection.json';assert prep['selection']==selection and prep['selectionSha256']==h((R/selection).read_bytes())
binding_path=R/'specs/1559-relay-long-sessions/lifecycle-source-packet.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw);assert binding['sourcePinCount']==136
expected_product_paths=set(['BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs', 'BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs', 'BookOfEternityClient/Services/LiveTurnPreparationService.cs', 'BookOfEternityClient/Services/SaveLoadService.cs', 'BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs', 'BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs', 'BookOfEternityClient/Services/SpiritualOriginalDraftInputs.cs', 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1', 'tools/gm-relay/README.md'])
assert set(binding['productDelta'])==expected_product_paths
actual_product_paths={p for p in git('diff','--name-only',binding['baselineProduct'],actual).decode().splitlines() if p.startswith(('BookOfEternityClient/','BookOfEternityGMBridge/','tools/gm-relay/'))}
assert actual_product_paths==expected_product_paths

for pin in binding['sourcePins']:
 f=R/pin['path'];b=f.read_bytes() if f.is_file() else git('show','HEAD:'+pin['path'])
 assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
pins=prep['binaryAfter'];assert len(pins)==9
def binaries():return [dict(path=p['path'],bytes=(R/p['path']).stat().st_size,sha256=h((R/p['path']).read_bytes())) for p in pins]
assert binaries()==pins
main_helper=R/'BookOfEternityClient/bin/Debug/net8.0/Launcher/GM_Turn_Helper.ps1';repo_helper=R/'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1'
assert main_helper.read_bytes()==repo_helper.read_bytes()
out=Path('/tmp/gm1559-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
patterns=['boe-relay-operational-helper-*','boe-gameengine-turnlifecycle-*','boe-live-turn-prep-*','boe-save-load-tests-*','boe-relay1559-*','boe-prompt-*','boe-gm1557-package-*','boe-gm1557-session-*']
artifact_root=R/'TestResults/native-terminal'
def namespace():return dict(ownedRoots=sorted(str(p) for pat in patterns for p in Path(tempfile.gettempdir()).glob(pat)),retainedNativeTerminal=sorted(str(p) for p in artifact_root.iterdir()) if artifact_root.exists() else [],retainedGmDocumentation=sorted(str(p) for p in Path(tempfile.gettempdir()).glob('boe-gm1557-evidence-*')))
ns_before=namespace();result_root=R/'TestResults/test-categories';old_results=set(result_root.iterdir()) if result_root.exists() else set()
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',selection,'-NoBuild']
start=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(compiled+'.lifecycle-stage-green.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_ACTUAL_INACTIVE_LIFECYCLE_GREEN47_SENTINEL',actualSource=actual,compiledSource=compiled,selection=selection,label=label,startedUtc=start),f,indent=2)
print('FIRST_LIFECYCLE_STAGE_GREEN_START',selection,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=start),indent=2)+'\n')
 native=process.wait()
ns_after=namespace();new_artifacts=sorted((set(ns_after['retainedNativeTerminal'])-set(ns_before['retainedNativeTerminal'])) | (set(ns_after['retainedGmDocumentation'])-set(ns_before['retainedGmDocumentation'])))
artifact_files=[dict(path=str(p),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for directory in new_artifacts for p in sorted(Path(directory).rglob('*')) if p.is_file()]
record=dict(kind='FIRST_INACTIVE_LIFECYCLE_GREEN47_ORIGINAL_RAW_NOT_ACCEPTANCE',label=label,selection=selection,argv=argv,cwd=str(R),actualSource=actual,compiledSource=compiled,baselineProduct=binding['baselineProduct'],productSource=compiled,productDelta=binding['productDelta'],sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinCount=136,sourcePinDigest=binding['sourcePinDigest'],nativeExit=native,startedUtc=start,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),planRecord=plan_record,planSha256=h(plan_path.read_bytes()),expectedDescriptors=plan['Descriptors'],resultDirectories=sorted(str(p) for p in set(result_root.iterdir())-old_results),binaryBefore=pins,binaryAfter=binaries(),repoHelperSha256=h(repo_helper.read_bytes()),managedHelperSha256=h(main_helper.read_bytes()),namespaceBefore=ns_before,namespaceAfter=ns_after,newFixtureArtifactDirectories=new_artifacts,fixtureArtifactFiles=artifact_files,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='ONLY named fixed47 SelectionFile-NoBuild aggregate after source/prep release. First unexpected aborts dependents; no retry/new selection. Eight lifecycle production owners+README only. Native0 alone is never full task acceptance. Require exact complete rows/prerequisites/no unexpected failures and strict caller roots, all actual guardian/scenario/child/joins and current-source hashes. FilesModified policy proof is not Complete/client acceptance. Retained diagnostics are archived separately, not misreported as removed caller roots.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
print('FIRST_LIFECYCLE_STAGE_GREEN_NATIVE',native,'record',str(out/'record.json'),flush=True)
sys.exit(native)
