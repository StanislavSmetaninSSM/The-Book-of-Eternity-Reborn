"""Retained issue diagnostic correction: races2 corrective regression and separately named unrun8/new crash5; retained42 cannot be selected. Each mode requires its own Root named activation; no auto advance/retry/build."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re,os
R=Path('/workspace/The-Book-of-Eternity-Reborn')
mode,label,actual,prep_path=sys.argv[1:]
groups={
 'crash5':(['fr063-joint-interface-crash-current'],5),
 'integrity4':(['fr063-joint-interface-current-integrity'],4),
 'races2':(['fr063-joint-interface-source-races'],2),
 'settlement3':(['fr063-joint-interface-settlement'],3),
 'uncertain1':(['fr063-joint-interface-uncertain-current'],1)}
assert mode in groups and re.fullmatch(r't179pd-ni-iobs-l01-process-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
p=R/'specs/1536-complete-wound-materialization/recovery/t179pd-ni-iobs-l01-source-packet-20261018.json';raw=p.read_bytes();binding=json.loads(raw)
assert len(binding['sourcePins'])==98 and len(binding['productDelta'])==21
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h(json.dumps(binding['sourcePins'],sort_keys=True,separators=(',',':')).encode())==binding['pinDigest']
ledger_path=R/binding['ledger']['path'];assert h(ledger_path.read_bytes())==binding['ledger']['sha256']
prep=json.loads(Path(prep_path).read_bytes());assert prep['kind']=='FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW' and prep['mode']==('corrective-plan' if mode=='races2' else 'remaining-plan') and prep['nativeExit']==0 and prep['actualTestsExecuted']==0
compiled=prep['compiledSource'];assert prep['sourcePinDigest']==binding['pinDigest'] and prep['sourcePinCount']==98
assert not subprocess.run(['git','merge-base','--is-ancestor',compiled,actual],cwd=R).returncode
assert all(n.startswith('specs/1536-complete-wound-materialization/') for n in git('diff','--name-only',compiled,actual).decode().splitlines())
assert git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==binding['productDelta']
plans=[json.loads((Path(d)/'plan.json').read_bytes()) for d in prep['resultDirectories'] if (Path(d)/'plan.json').exists()];assert len(plans)==1
categories,expected_cases=groups[mode];selected=[d for d in plans[0]['Descriptors'] if d['Category'] in categories]
assert len(selected)==len(categories) and sum(d['EstimatedCases'] for d in selected)==expected_cases
stage=json.loads((R/'tests/selection.json').read_bytes());subset=dict(schemaVersion=1,selections=[x for x in stage['selections'] if x['category'] in categories]);assert len(subset['selections'])==len(categories)
nine=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.TestSupport.dll','BookOfEternityClient/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityGMBridge.dll','BookOfEternityGMBridge/bin/Debug/net8.0/BookOfEternityClient.dll']
outputs=[R/x/'bin/Debug/net8.0' for x in ['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','BookOfEternityGMBridge']]
def inventory(paths):return [dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=h(p.read_bytes())) for p in sorted(set(paths)) if p.is_file()]
def binaries():return inventory([R/x for x in nine])
def all_outputs():return inventory([p for root in outputs for p in root.rglob('*') if p.is_file()])
def namespace():return dict(ownedRoots=sorted(str(p) for pattern in ['boe-gameengine-turnlifecycle-*','boe-joint-interface-crash-*'] for p in Path(tempfile.gettempdir()).glob(pattern)))
def processes():
 found=[]
 for d in Path('/proc').iterdir():
  if not d.name.isdigit():continue
  try:
   exe=(d/'exe').resolve();cmd=(d/'cmdline').read_bytes().decode(errors='replace').replace('\0',' ')
   if exe.name in {'dotnet','testhost'} and re.search(r'testhost\.dll|BookOfEternityClient(?:\.TestSupport|\.IntegrationTests|\.Tests)?\.dll',cmd):found.append(dict(pid=int(d.name),executable=str(exe),cmdline=cmd,stat=(d/'stat').read_text()))
  except (OSError,ValueError):pass
 return sorted(found,key=lambda row:row['pid'])
before=binaries();all_before=all_outputs();ns_before=namespace();proc_before=processes()
assert len(before)==9 and before==prep['binaryAfter'] and all_before==prep['allOutputInventory']
assert ns_before==dict(ownedRoots=[]) and not proc_before
out=Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
selection=out/'exact-selected-scope.json';selection.write_text(json.dumps(subset,indent=2)+'\n')
started=now();clock=time.monotonic();sentinel=out.parent/(compiled+'-ni-iobs-l01-process-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NAMED_NI_IOBS_L01_WHOLE_ACTUAL',mode=mode,actualSource=actual,compiledSource=compiled,label=label,startedUtc=started),f,indent=2)
(out/'source-binding.json').write_bytes(raw);(out/'ledger-context.json').write_bytes(ledger_path.read_bytes())
results_root=R/'TestResults/test-categories';old=set(results_root.iterdir()) if results_root.exists() else set()
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',str(selection),'-NoBuild']
print('FIRST_WHOLE_NI_IOBS_L01_ACTUAL_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 child=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);identity=dict(pid=child.pid)
 try:identity.update(stat=(Path('/proc')/str(child.pid)/'stat').read_text(),cmdline=(Path('/proc')/str(child.pid)/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity['exitedBeforeCensus']=True
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=started),indent=2)+'\n');native=child.wait()
results=sorted(str(p) for p in set(results_root.iterdir())-old) if results_root.exists() else [];after=binaries();all_after=all_outputs();ns_after=namespace();proc_after=processes()
record=dict(kind='FIRST_NI_IOBS_L01_WHOLE_ACTUAL_RAW_NOT_EVIDENCE',phase='READY_DRIFT_DIAGNOSTIC_CORRECTIVE2_AND_REMAINING13',mode=mode,label=label,actualSource=actual,compiledSource=compiled,productBase=binding['productBase'],implementedProductCompiledSource=compiled,categories=categories,expectedCases=expected_cases,expectedDescriptors=selected,argv=argv,cwd=str(R),prepRecord=str(prep_path),prepRecordSha256=h(Path(prep_path).read_bytes()),sourceBindingSha256=h(raw),sourcePinCount=98,sourcePinDigest=binding['pinDigest'],startedUtc=started,finishedUtc=now(),wallSeconds=time.monotonic()-clock,nativeExit=native,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,allOutputInventoryBefore=all_before,allOutputInventory=all_after,namespaceBefore=ns_before,namespaceAfter=ns_after,processesBefore=proc_before,processesAfter=proc_after,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='ONE named fixed mode only, NoBuild. Original native/stdout/process/TRX/source/child/cleanup outcome precedes utility qualification. No retry, automatic next scope, retained42 replay or inferred semantic acceptance; races2 is a separately named post-source-change regression, not a retry of the frozen original.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_WHOLE_NI_IOBS_L01_ACTUAL_NATIVE',native,str(out/'record.json'),flush=True)
assert before==after and all_before==all_after and ns_before==ns_after and not proc_after and record['headAfter']==actual and not record['statusAfter']
# Semantic methods/categories, not plan ordinal suffixes, bind actual subset.
if native==0:
 summaries=[json.loads((Path(d)/'summary.json').read_bytes()) for d in results if (Path(d)/'summary.json').exists()];assert len(summaries)==1
 tests=summaries[0]['Tests'];assert tests['Executed']==expected_cases and tests['Passed']==expected_cases and tests['Failed']==0
 actual_plans=[json.loads((Path(d)/'plan.json').read_bytes()) for d in results if (Path(d)/'plan.json').exists()];assert len(actual_plans)==1
 def identity(d):return (d['Category'],d['Adapter'],d['EstimatedCases'],json.dumps(d['ExpectedMethodCounts'],sort_keys=True))
 assert sorted(map(identity,selected))==sorted(map(identity,actual_plans[0]['Descriptors']))
sys.exit(native)
