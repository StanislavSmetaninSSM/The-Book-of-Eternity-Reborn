"""UNRUN fixed FIRST post-product protectedLate2 or ordinary52 recorder; separate named Root activation for each mode. Explicit Root named actual activation required."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn')
mode,label,actual,prep_path=sys.argv[1:]
assert mode in {'protected-late2','ordinary52'}
assert re.fullmatch(r't179pd3d-product-(late2|ordinary52)-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
assert label.startswith('t179pd3d-product-'+('late2' if mode=='protected-late2' else 'ordinary52')+'-first-')
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1536-complete-wound-materialization/recovery/t179pd3d-ordinary-tests-source-packet-20261017.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
assert len(binding['sourcePins'])==67 and len(binding['productDelta'])==2
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
assert h(json.dumps(binding['sourcePins'],ensure_ascii=False,separators=(',',':')).encode())==binding['sourcePinDigest']
assert sorted(git('diff','--name-only',binding['baseline'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines())==sorted(binding['productDelta'])
prep=json.loads(Path(prep_path).read_text());assert prep['kind']=='FIRST_PD3D_PRODUCT_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE' and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
compiled=prep['compiledSource'];assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in git('diff','--name-only',compiled,actual).decode().splitlines())
plan=json.loads((Path(prep['resultDirectories'][0])/'plan.json').read_text())
assert len(plan['Descriptors'])==6 and sum(d['EstimatedCases'] for d in plan['Descriptors'])==54
ordinary=['fr063-ordinary-output-policy-boundary','fr063-ordinary-output-policy-source','fr063-ordinary-output-policy-regression','fr063-ordinary-output-existing','fr063-ordinary-output-projection']
categories=['fr063-joint-late-completion'] if mode=='protected-late2' else ordinary
selected=[d for d in plan['Descriptors'] if d['Category'] in categories]
assert len(selected)==(1 if mode=='protected-late2' else 5)
assert sum(d['EstimatedCases'] for d in selected)==(2 if mode=='protected-late2' else 52)
assert len([m for d in selected for m in d['ExpectedMethodCounts']])==(2 if mode=='protected-late2' else 29)
if mode=='protected-late2':
 assert selected[0]['Adapter']=='integration'
 assert {m['Name']:m['Cases'] for m in selected[0]['ExpectedMethodCounts']}=={
 'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression':1,
 'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce':1}
def binaries():
 out=[]
 for pin in prep['binaryAfter']:
  p=R/pin['path'];b=p.read_bytes();out.append(dict(path=pin['path'],bytes=len(b),sha256=h(b)))
 return out
before=binaries();assert len(before)==9 and before==prep['binaryAfter']
paths=['boe-pd3d-ordinary-*','boe-narrative-validation-*','boe-interface-normalization-*','boe-resource-materialization-*','boe-held-treatment-pipeline-*','boe-gameengine-turnlifecycle-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pattern in paths for p in Path(tempfile.gettempdir()).glob(pattern)))
ns_before=namespace();out=Path('/tmp/t179pd3d-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
result_root=R/'TestResults/test-categories';old=set(result_root.iterdir()) if result_root.exists() else set()
started=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(actual+'-pd3d-product-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NAMED_PD3D_PRODUCT_RAW',actualSource=actual,compiledSource=compiled,label=label,startedUtc=started),f,indent=2)
(out/'source-binding.json').write_bytes(binding_raw)
if mode=='protected-late2':
 argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',categories[0],'-NoBuild']
else:
 # Exact fixed category array; canonical script, no generated test harness or selector change.
 command="& ./scripts/test-csharp.ps1 -Category @("+','.join("'"+x+"'" for x in ordinary)+") -NoBuild"
 argv=['pwsh','-NoLogo','-NoProfile','-Command',command]
print('FIRST_PD3D_PRODUCT_START',actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=started),indent=2)+'\n');native=process.wait()
results=sorted(str(p) for p in set(result_root.iterdir())-old) if result_root.exists() else []
after=binaries();ns_after=namespace()
record=dict(kind='FIRST_PD3D_PRODUCT_RAW_NOT_EVIDENCE_ACCEPTANCE',label=label,actualSource=actual,compiledSource=compiled,productSource=compiled,categories=categories,mode=mode,argv=argv,cwd=str(R),prepRecord=str(prep_path),prepRecordSha256=h(Path(prep_path).read_bytes()),sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinDigest=binding['sourcePinDigest'],sourcePinCount=67,startedUtc=started,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=ns_after,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),expectedDescriptors=selected,helperSource=Path(__file__).read_text(),scope='Only separately named FIRST protectedLate2 OR ordinary52 on freshly compiled minimal two-file extraction. Protected2 separate EVIDENCE required before ordinary52 release. Preserve all FIRST original outcomes; no retry or other selection. New strict ordinary fixture receipts and actual protected roots differ from SP best-effort Dispose: namespace observations never invent SP strict receipts. Old World fence/HOT/history/full F1 remain held.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_PD3D_PRODUCT_NATIVE',native,'record',str(out/'record.json'),flush=True)
assert before==after and ns_before==ns_after and record['headAfter']==actual and not record['statusAfter']
sys.exit(native)
