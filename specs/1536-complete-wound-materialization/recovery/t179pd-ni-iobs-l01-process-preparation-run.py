"""Settlement fixture classification correction: separately named fresh BOTH/catalog, whole57, corrective3 and remaining10 ZERO. Root named release required."""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re,os
R=Path('/workspace/The-Book-of-Eternity-Reborn')
mode,label,actual=sys.argv[1:4]
assert mode in {'catalog','plan','corrective-plan','remaining-plan'} and re.fullmatch(r't179pd-ni-iobs-l01-process-prep-first-[a-z0-9-]+',label) and re.fullmatch(r'[0-9a-f]{40}',actual)
def git(*a):return subprocess.check_output(['git',*a],cwd=R)
def h(b):return hashlib.sha256(b).hexdigest()
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
assert git('rev-parse','HEAD').decode().strip()==actual and not git('status','--porcelain')
p=R/'specs/1536-complete-wound-materialization/recovery/t179pd-ni-iobs-l01-source-packet-20261018.json';raw=p.read_bytes();binding=json.loads(raw)
assert len(binding['sourcePins'])==98 and len(binding['productDelta'])==21
for pin in binding['sourcePins']:
 b=(R/pin['path']).read_bytes();assert len(b)==pin['bytes'] and h(b)==pin['sha256'],pin['path']
assert h(json.dumps(binding['sourcePins'],sort_keys=True,separators=(',',':')).encode())==binding['pinDigest']
ledger_path=R/binding['ledger']['path'];assert h(ledger_path.read_bytes())==binding['ledger']['sha256'];ledger=json.loads(ledger_path.read_bytes())
assert (ledger['staticTotals']['newMethods'],ledger['staticTotals']['newRows'])==(28,51)
assert git('diff','--name-only',binding['productBase'],actual,'--','BookOfEternityClient','BookOfEternityGMBridge').decode().splitlines()==binding['productDelta']
selection='tests/selection.json';selection_raw=(R/selection).read_bytes();whole_selection_raw=selection_raw;stage=json.loads(selection_raw)
assert len(stage['selections'])==16 and (R/'tests/selections/1536-joint-interface-lifecycle-stage.json').read_bytes()==selection_raw
expected_descriptors,expected_methods,expected_cases=16,33,57
if mode in {'corrective-plan','remaining-plan'}:
 pending_categories=({'fr063-joint-interface-settlement'} if mode=='corrective-plan' else {'fr063-joint-interface-crash-current','fr063-joint-interface-current-integrity','fr063-joint-interface-uncertain-current'})
 stage=dict(schemaVersion=1,selections=[x for x in stage['selections'] if x['category'] in pending_categories])
 expected_descriptors,expected_methods,expected_cases=(1,2,3) if mode=='corrective-plan' else (3,3,10)
 assert len(stage['selections'])==expected_descriptors
 selection_raw=(json.dumps(stage,indent=2)+'\n').encode()

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
before=binaries();all_before=all_outputs();ns_before=namespace();proc_before=processes();assert ns_before==dict(ownedRoots=[]) and not proc_before
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']
if mode=='catalog':assert len(sys.argv)==4;argv+=['-ValidateCatalog']
else:
 assert len(sys.argv)==5;catalog=json.loads(Path(sys.argv[4]).read_bytes());assert catalog['mode']=='catalog' and catalog['nativeExit']==0 and catalog['actualTestsExecuted']==0
 assert catalog['actualSource']==actual and catalog['sourcePinDigest']==binding['pinDigest'] and catalog['binaryAfter']==before and catalog['allOutputInventory']==all_before
 argv+=['-SelectionFile',selection,'-PlanOnly','-NoBuild']
out=Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
if mode in {'corrective-plan','remaining-plan'}:
 selection=out/'exact-selected-scope.json';selection.write_bytes(selection_raw)
 argv[argv.index('-SelectionFile')+1]=str(selection)
results_root=R/'TestResults/test-categories';old=set(results_root.iterdir()) if results_root.exists() else set();started=now();clock=time.monotonic()
sentinel=out.parent/(actual+'-ni-iobs-l01-process-zero-'+mode+'.FIRST.json')
with sentinel.open('x') as f:json.dump(dict(kind='FIRST_NAMED_WHOLE_NI_IOBS_L01_ZERO',mode=mode,actualSource=actual,label=label,startedUtc=started),f,indent=2)
(out/'source-binding.json').write_bytes(raw);(out/'ledger-context.json').write_bytes(ledger_path.read_bytes())
print('FIRST_WHOLE_NI_IOBS_L01_PREP_START',mode,actual,str(out/'stdout.log'),flush=True)
with (out/'stdout.log').open('wb') as stream:
 child=subprocess.Popen(argv,cwd=R,stdout=stream,stderr=subprocess.STDOUT);identity=dict(pid=child.pid)
 try:identity.update(stat=(Path('/proc')/str(child.pid)/'stat').read_text(),cmdline=(Path('/proc')/str(child.pid)/'cmdline').read_bytes().decode().replace('\0',' '))
 except FileNotFoundError:identity['exitedBeforeCensus']=True
 (out/'process.json').write_text(json.dumps(dict(argv=argv,identity=identity,startedUtc=started),indent=2)+'\n');native=child.wait()
results=sorted(str(p) for p in set(results_root.iterdir())-old) if results_root.exists() else [];after=binaries();all_after=all_outputs();ns_after=namespace();proc_after=processes()
summaries=[json.loads((Path(d)/'summary.json').read_bytes()) for d in results if (Path(d)/'summary.json').exists()]
executed=sum(j.get('Tests',{}).get('Executed',0) for j in summaries)
record=dict(kind='FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW',phase='SETTLEMENT_FIXTURE_CLASSIFICATION_FRESH_ZERO',mode=mode,label=label,actualSource=actual,compiledSource=actual,productBase=binding['productBase'],implementedProductCompiledSource=actual,sourcePinCount=len(binding['sourcePins']),sourcePinDigest=binding['pinDigest'],sourceBindingSha256=h(raw),ledgerContextSha256=h(ledger_path.read_bytes()),selectionSha256=h(selection_raw),wholeSelectionSha256=h(whole_selection_raw),expectedDescriptors=expected_descriptors,expectedMethods=expected_methods,expectedCases=expected_cases,argv=argv,cwd=str(R),startedUtc=started,finishedUtc=now(),wallSeconds=time.monotonic()-clock,nativeExit=native,actualTestsExecuted=executed,stdoutPath=str(out/'stdout.log'),stdoutSha256=h((out/'stdout.log').read_bytes()),processRecord=str(out/'process.json'),firstSentinel=str(sentinel),resultDirectories=results,binaryBefore=before,binaryAfter=after,allOutputInventoryBefore=all_before,allOutputInventory=all_after,namespaceBefore=ns_before,namespaceAfter=ns_after,processesBefore=proc_before,processesAfter=proc_after,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),helperSource=Path(__file__).read_text(),scope='Corrected-fixture fresh BOTH/catalog discovery then whole16/33/57, corrective1/2/3 or remaining3/3/10 PlanOnly-NoBuild (exact subsets archived), ZERO actual tests. No retries, accepted-family replay or automatic actual execution.')
(out/'record.json').write_text(json.dumps(record,indent=2)+'\n');print('FIRST_WHOLE_NI_IOBS_L01_PREP_NATIVE',native,str(out/'record.json'),flush=True)
# Original outcome is already frozen before utility qualification.
if native==0:
 assert executed==0 and not list(p for d in results for p in Path(d).rglob('*.trx'))
 assert len(after)==9 and len({x['sha256'] for x in after if Path(x['path']).name=='BookOfEternityClient.dll'})==1
 support=[x['sha256'] for x in after if Path(x['path']).name=='BookOfEternityClient.TestSupport.dll'];assert len(support)==2 and len(set(support))==1
 assert ns_before==ns_after and not proc_after and record['headAfter']==actual and not record['statusAfter']
 if mode in {'plan','corrective-plan','remaining-plan'}:
  assert before==after and all_before==all_after
  plans=[json.loads((Path(d)/'plan.json').read_bytes()) for d in results if (Path(d)/'plan.json').exists()];assert len(plans)==1
  plan=plans[0];(out/'observed-plan.json').write_text(json.dumps(plan,indent=2)+'\n');descriptors=plan['Descriptors']
  assert len(descriptors)==expected_descriptors and {d['Category'] for d in descriptors}=={x['category'] for x in stage['selections']}
  assert sum(d['EstimatedCases'] for d in descriptors)==expected_cases
  assert sum(len(d['ExpectedMethodCounts']) for d in descriptors)==expected_methods
  actual_methods={m['Name']:m['Cases'] for d in descriptors for m in d['ExpectedMethodCounts']}
  assert len(actual_methods)==expected_methods
  for row in ledger['rows']:
   if row['category'] in {x['category'] for x in stage['selections']}:assert actual_methods[row['method']]==row['staticRows'],row['method']
sys.exit(native)
