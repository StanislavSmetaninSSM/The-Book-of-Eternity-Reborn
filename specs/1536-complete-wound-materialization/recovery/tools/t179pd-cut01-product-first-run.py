"""UNRUN fixed FIRST CUT01 product recorder. Separate named Root activation per mode; no build/retry/advance."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re
R=Path('/workspace/The-Book-of-Eternity-Reborn')
mode,label,actual,prep_path=sys.argv[1:]
assert mode in {'protected-late2','new3'} and re.fullmatch(r't179pd-cut01-product-(late2|new3)-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
assert ('-late2-' in label)==(mode=='protected-late2')
def git(*args):return subprocess.check_output(['git',*args],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
binding_path=R/'specs/1536-complete-wound-materialization/recovery/t179pd-cut01-source-packet-20261018.json';binding_raw=binding_path.read_bytes();binding=json.loads(binding_raw)
product_files={'BookOfEternityClient/Core/GameEngine.cs','BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs'}
assert len(binding['sourcePins'])==31 and set(binding['productDelta'])==product_files
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h((R/binding['currentLedger']['path']).read_bytes())==binding['currentLedger']['sha256']
assert h(json.dumps(binding['sourcePins'],ensure_ascii=False,separators=(',',':')).encode())==binding['sourcePinDigest']
assert set(git('diff','--name-only',binding['base'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines())==product_files
prep=json.loads(Path(prep_path).read_text());assert prep['kind']=='FIRST_CUT01_PRODUCT_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE' and prep['mode']=='plan' and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
compiled=prep['compiledSource'];assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in git('diff','--name-only',compiled,actual).decode().splitlines())
plan=json.loads((Path(prep['resultDirectories'][0])/'plan.json').read_text())
expected={
 'fr063-joint-capture-cut-stop':{'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner':1},
 'fr063-joint-capture-cut-transparency':{'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion':2},
 'fr063-joint-late-completion':{'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairCompletesJointOnceWithZeroCycleProgression':1,'BookOfEternityClient.Tests.GameEngineTurnLifecycleTests.GuaranteedTreatment_LiveSeverityRepairConsumesMatchingNonzeroReportExactlyOnce':1}}
assert set(plan['Categories'])==set(expected) and len(plan['Descriptors'])==3
for d in plan['Descriptors']:
 assert d['Adapter']=='integration' and {m['Name']:m['Cases'] for m in d['ExpectedMethodCounts']}==expected[d['Category']]
 assert d['EstimatedCases']==sum(expected[d['Category']].values())
categories=['fr063-joint-late-completion'] if mode=='protected-late2' else ['fr063-joint-capture-cut-stop','fr063-joint-capture-cut-transparency']
selected=[d for d in plan['Descriptors'] if d['Category'] in categories]
def binaries():
 out=[]
 for pin in prep['binaryAfter']:
  b=(R/pin['path']).read_bytes();out.append(dict(path=pin['path'],bytes=len(b),sha256=h(b)))
 return out
before=binaries();assert len(before)==9 and before==prep['binaryAfter']
patterns=['boe-gameengine-turnlifecycle-*']
def namespace():return dict(ownedRoots=sorted(str(p) for pattern in patterns for p in Path(tempfile.gettempdir()).glob(pattern)))
ns_before=namespace();assert ns_before==dict(ownedRoots=[]),ns_before
out=Path('/tmp/t179pd-cut01-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']
selection_source=R/'tests/selection.json';source_selection=json.loads(selection_source.read_text())
assert prep['selectionSha256']==h(selection_source.read_bytes()) and len(source_selection['selections'])==3 and {x['category'] for x in source_selection['selections']}==set(expected)
selection_path=None
if mode=='protected-late2':argv+=['-Category',categories[0],'-NoBuild']
else:
 subset=dict(source_selection);subset['selections']=[x for x in source_selection['selections'] if x['category'] in categories]
 assert len(subset['selections'])==2 and {x['category'] for x in subset['selections']}==set(categories)
 selection_path=out/'exact-new3-selection.json';selection_path.write_text(json.dumps(subset,ensure_ascii=False,indent=2)+'\n')
 argv+=['-SelectionFile',str(selection_path),'-NoBuild']
result_root=R/'TestResults/test-categories';old=set(result_root.iterdir()) if result_root.exists() else set()
started=datetime.datetime.now(datetime.timezone.utc).isoformat();clock=time.monotonic();sentinel=out.parent/(actual+'-cut01-product-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NAMED_CUT01_PRODUCT_'+mode.upper(),actualSource=actual,compiledSource=compiled,label=label,startedUtc=started),f,indent=2)
(out/'source-binding.json').write_bytes(binding_raw)
print('FIRST_CUT01_PRODUCT_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 process=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);proc=Path('/proc')/str(process.pid)
 try:
  stat=(proc/'stat').read_text();tail=stat[stat.rfind(')')+2:].split();identity=dict(pid=process.pid,birthTick=int(tail[19]),stat=stat,cmdline=(proc/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity=dict(pid=process.pid,exitedBeforeCensus=True)
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=started),indent=2)+'\n');native=process.wait()
results=sorted(str(p) for p in set(result_root.iterdir())-old) if result_root.exists() else []
after=binaries();ns_after=namespace()
record=dict(kind='FIRST_CUT01_PRODUCT_RAW_NOT_EVIDENCE_ACCEPTANCE',mode=mode,label=label,actualSource=actual,compiledSource=compiled,productSource=compiled,categories=categories,argv=argv,cwd=str(R),prepRecord=str(prep_path),prepRecordSha256=h(Path(prep_path).read_bytes()),sourceBindingPath=str(binding_path),sourceBindingSha256=h(binding_raw),sourcePinDigest=binding['sourcePinDigest'],sourcePinCount=31,selectionSource=str(selection_source),selectionSourceSha256=h(selection_source.read_bytes()),exactSubsetSelection=str(selection_path) if selection_path else None,exactSubsetSelectionSha256=h(selection_path.read_bytes()) if selection_path else None,startedUtc=started,finishedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),wallSeconds=time.monotonic()-clock,nativeExit=native,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,namespaceBefore=ns_before,namespaceAfter=ns_after,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),expectedDescriptors=selected,expectedCases=sum(d['EstimatedCases'] for d in selected),helperSource=Path(__file__).read_text(),scope='Separate named protectedLate2 FIRST/EVIDENCE before named new3. Canonical NoBuild only; exact temporary new3 subset preserves unchanged tracked stage selection. Original native outcome written before checks; strict real owner/callback/preparation/bytes/cleanup outcomes require independent raw review. No retry/advance/build/other family/NI activation.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_CUT01_PRODUCT_NATIVE',native,'record',str(out/'record.json'),flush=True)
assert before==after and ns_before==ns_after and record['headAfter']==actual and not record['statusAfter']
sys.exit(native)
