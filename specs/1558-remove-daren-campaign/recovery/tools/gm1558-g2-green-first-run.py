"""UNRUN bounded FIRST G2 GREEN recorder. Root exact activation is required.
One canonical current SelectionFile -NoBuild invocation; no retries or source edits.
"""
from pathlib import Path
import sys,subprocess,hashlib,json,datetime,time,tempfile,re,os,gzip
ROOT=Path('/workspace/The-Book-of-Eternity-Reborn-1558')
COMPILED='555ceae66e10300d3cf76ff849fb110b79b15549'
FRONTEND='110e7ac516789fb923adefde04997c04b4685ed7'
FRONTEND_TREE='1ae60672f22452a8df57958215d32a371d2ae8c3'
CATALOG=Path('/tmp/gm1558-implementation-tools/gm1558-g2-catalog-first-555ceae6-20261017t1100z/record.json')
CATALOG_SHA='77caecccc8b0513d6b9801cb069a38f40c180363ef5f3b8aca69cdcbf9c385ea'
PLAN=Path('/tmp/gm1558-implementation-tools/gm1558-g2-green-plan-first-555ceae6-20261017t1057z/record.json')
PLAN_SHA='c6dafb85b31d4825e454e251aa65fff431e4361e319750f4e98f1c305bbbd174'
PACKAGE=Path('/tmp/gm1558-implementation-tools/gm1558-g2-catalog-first-555ceae6-20261017t1100z/fresh-package-frontend-docs-manifest.json')
PACKAGE_SHA='2ffbfa7a1b248ec4975d460e396b392f68feec81f0bda1fbba5bc3eb98a1a892'
SOURCE_PINS_SHA='d55042795f174f07f2dc9d382b4556c07d5d959218d7d07f1fb97515eb5cc2eb'
FEATURE='specs/1558-remove-daren-campaign/'
SELECTION='tests/selection.json'
BINDING=FEATURE+'g2-implementation-source-packet-20261017.json'
def sha(b):return hashlib.sha256(b).hexdigest()
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def file_pin(path):
 b=path.read_bytes();return dict(path=str(path),bytes=len(b),sha256=sha(b))
def namespaces():return sorted(str(p)for p in Path(tempfile.gettempdir()).glob('boe-*'))
def identity(pid):
 try:
  first=(Path('/proc')/str(pid)/'stat').read_text();firstTail=first[first.rfind(')')+2:].split();argv=(Path('/proc')/str(pid)/'cmdline').read_bytes().split(b'\0');second=(Path('/proc')/str(pid)/'stat').read_text();tail=second[second.rfind(')')+2:].split()
  if firstTail[19]!=tail[19]:return dict(pid=pid,status='IDENTITY_CHANGED_DURING_READ')
  return dict(pid=pid,birthTicks=int(tail[19]),state=tail[0],parentPid=int(tail[1]),processGroup=int(tail[2]),session=int(tail[3]),argv=[x.decode(errors='replace')for x in argv if x],status='EXACT_STAT_CMDLINE_STAT')
 except FileNotFoundError:return dict(pid=pid,status='ABSENT')
def main():
 assert len(sys.argv)==3,'unique label and exact separately activated metadata HEAD required'
 label,expected=sys.argv[1:];assert re.fullmatch(r'gm1558-g2-green-first-[A-Za-z0-9-]+',label)and re.fullmatch(r'[0-9a-f]{40}',expected)
 assert git('branch','--show-current').decode().strip()=='1558-remove-daren-campaign-20261017'
 assert git('rev-parse','HEAD').decode().strip()==expected and not git('status','--porcelain')
 subprocess.run(['git','merge-base','--is-ancestor',COMPILED,expected],cwd=ROOT,check=True)
 changed=git('diff','--no-renames','--name-only',COMPILED,expected).decode().splitlines();assert all(p.startswith(FEATURE)for p in changed),changed
 for path,digest in [(CATALOG,CATALOG_SHA),(PLAN,PLAN_SHA),(PACKAGE,PACKAGE_SHA)]:assert sha(path.read_bytes())==digest,str(path)
 cat=json.loads(CATALOG.read_text());reviewed=json.loads(PLAN.read_text());manifest=json.loads(PACKAGE.read_text())
 assert cat['nativeExit']==reviewed['nativeExit']==0 and cat['mode']=='catalog'and reviewed['mode']=='plan'
 assert cat['compiledSource']==reviewed['compiledSource']==COMPILED and len(cat['binaryAfter'])==7 and cat['binaryAfter']==reviewed['binaryBefore']==reviewed['binaryAfter']
 assert len(reviewed['resultDirectories'])==1
 planPath=Path(reviewed['resultDirectories'][0])/'plan.json';planBytes=planPath.read_bytes();plan=json.loads(planBytes)
 assert len(plan['Categories'])==17 and len(plan['Descriptors'])==19
 rows=[dict(category=d['Category'],adapter=d['Adapter'],**m)for d in plan['Descriptors']for m in d['ExpectedMethodCounts']]
 assert len(rows)==len({m['Name']for m in rows})==50
 assert sum(m['Cases']for m in rows if m['adapter']in {'unit','integration'})==98
 assert sum(m['Cases']for m in rows if m['adapter']not in {'unit','integration'})==8
 assert all(not d['FrontendBuild']for d in plan['Descriptors'])
 assert (ROOT/SELECTION).read_bytes()==(ROOT/'tests/selections/1558-daren-retirement.json').read_bytes()
 ids=[x['category']for x in json.loads((ROOT/SELECTION).read_text())['selections']];assert len(ids)==len(set(ids))==17 and set(ids)==set(plan['Categories'])
 bindingBytes=(ROOT/BINDING).read_bytes();binding=json.loads(bindingBytes);pins=binding['sourcePins'];assert len(pins)==binding['sourcePinCount']==274
 assert sha(json.dumps(pins,sort_keys=True,separators=(',',':')).encode())==SOURCE_PINS_SHA
 for p in pins:assert sha((ROOT/p['path']).read_bytes())==p['sha256'],p['path']
 assert git('rev-parse',expected+':BookOfEternityClient.WebFrontend').decode().strip()==FRONTEND_TREE
 def binaries():return [dict(path=p['path'],bytes=(ROOT/p['path']).stat().st_size,sha256=sha((ROOT/p['path']).read_bytes()))for p in cat['binaryAfter']]
 before=binaries();assert before==cat['binaryAfter']
 packageRoot=ROOT/'BookOfEternityClient/bin/Debug/net8.0'
 for pin in manifest['packageFiles']:assert sha((packageRoot/pin['path']).read_bytes())==pin['sha256'],pin['path']
 assert len(manifest['packageFiles'])==370 and len(manifest['distFiles'])==113
 for pin in manifest['distFiles']:
  assert sha((ROOT/'BookOfEternityClient.WebFrontend/dist'/pin['path']).read_bytes())==pin['sha256']
  assert sha((packageRoot/'wwwroot/browser'/pin['path']).read_bytes())==pin['sha256']
 for pin in manifest['expected52SourceEqualPackage']:assert sha((ROOT/pin['path']).read_bytes())==pin['sha256']
 selectorPins=[file_pin(ROOT/p)for p in ['tests/categories.json',SELECTION,'tests/selections/1558-daren-retirement.json']]
 nsBefore=namespaces();out=Path('/tmp/gm1558-implementation-tools')/label;assert not out.exists();out.mkdir(parents=True)
 marker=out.parent/(COMPILED+'-g2-current-selection-green.first-launch.json')
 with marker.open('x')as stream:json.dump(dict(kind='FIRST_G2_GREEN_SENTINEL_NOT_APPROVAL',createdUtc=utc(),actualSource=expected,compiledSource=COMPILED,retainedFrontendSource=FRONTEND,label=label,recordPath=str(out/'record.json')),stream,indent=2)
 args=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-SelectionFile',SELECTION,'-NoBuild']
 results=ROOT/'TestResults/test-categories';old=set(results.iterdir())if results.exists()else set();start=utc();clock=time.monotonic();native=None;error=None;process=None;launched=None
 print('FIRST_G2_GREEN_START',expected,'compiled',COMPILED,'stdout',out/'stdout.log',flush=True)
 try:
  with (out/'stdout.log').open('wb')as log:
   process=subprocess.Popen(args,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,start_new_session=True);launched=identity(process.pid)
   (out/'process.json').write_text(json.dumps(dict(pid=process.pid,identity=launched,argv=args,startedUtc=start,actualSource=expected,compiledSource=COMPILED,retainedFrontendSource=FRONTEND),indent=2)+'\n')
   native=process.wait()
 except BaseException as failure:error=repr(failure)
 new=sorted(str(p)for p in set(results.iterdir())-old)if results.exists()else[];nsAfter=namespaces();raw=[];summaries=[]
 for directory in new:
  for p in sorted(Path(directory).rglob('*')):
   if p.is_file():raw.append(file_pin(p))
  summary=Path(directory)/'summary.json'
  if summary.exists():
   s=json.loads(summary.read_text());summaries.append({k:s.get(k)for k in ['ExitCode','TimedOut','Selection','Tests','OwnedTreeCleanupSucceeded','RuntimeCleanupSucceeded']})
 artifactDirectories=sorted(p for p in set(nsAfter)-set(nsBefore)if Path(p).is_dir());artifacts=[file_pin(p)for d in artifactDirectories for p in sorted(Path(d).rglob('*'))if p.is_file()]
 after=binaries();record=dict(kind='FIRST_CURRENT_G2_GREEN_AGGREGATE_RAW_NOT_INDEPENDENT_ACCEPTANCE',mode='actual',label=label,argv=args,cwd=str(ROOT),actualSource=expected,compiledSource=COMPILED,productSource=COMPILED,retainedFrontendSource=FRONTEND,frontendGitTree=FRONTEND_TREE,currentSelectionPins=selectorPins,sourceBindingPath=BINDING,sourceBindingSha256=sha(bindingBytes),sourcePinsDigest=SOURCE_PINS_SHA,sourcePinsVerified=274,reviewedPlanRecord=str(PLAN),reviewedPlanRecordSha256=PLAN_SHA,reviewedPlanPath=str(planPath),reviewedPlanSha256=sha(planBytes),expectedPlan=dict(categories=17,descriptors=19,methodsOrFiles=50,csharpCases=98,frontendFileEstimates=8,totalEstimated=106,rows=rows),packageManifestPath=str(PACKAGE),packageManifestSha256=PACKAGE_SHA,startedUtc=start,finishedUtc=utc(),wallSeconds=time.monotonic()-clock,nativeExit=native,launchError=error,nativeProcessSettled=bool(process is not None and process.poll()is not None),nativeProcessIdentityAtLaunch=launched,nativeProcessIdentityAfterWait=identity(process.pid)if process else None,processRecord=str(out/'process.json'),stdoutPath=str(out/'stdout.log'),stdoutSha256=sha((out/'stdout.log').read_bytes()),resultDirectories=new,resultFiles=raw,summaryObservations=summaries,binaryBefore=before,binaryAfter=after,ownedPrefixesBefore=nsBefore,ownedPrefixesAfter=nsAfter,newRetainedArtifactDirectories=artifactDirectories,fixtureArtifactFiles=artifacts,headAfter=git('rev-parse','HEAD').decode().strip(),statusAfter=git('status','--porcelain').decode(),firstLaunchSentinel=str(marker),helperText=Path(__file__).read_text(),scope='ONE current reviewed SelectionFile-NoBuild. Canonical runner aborts on first nonzero descriptor; incomplete selection is recorded, never filled by retries or another invocation. Runtime counts/identities/frontend estimates/strict root+task+process cleanup require raw qualification. No cleanup of retained evidence/userdata, source mutation, build, new dependency, browser launch or new authorization.',authority='UNRUN inspectable recorder; invocation requires final persisted independent SOURCE/Root exact actualSource activation. Helper grants none.',qualification='Native0 alone is not GREEN acceptance. Preserve FIRST native/TRX/summary/fixture/process bytes even if failure. Root/independent review decides completeness and semantics. Interactive Chromium proof remains separate and Root-owned.')
 (out/'record.json').write_text(json.dumps(record,indent=2)+'\n')
 print('FIRST_G2_GREEN_EXIT',native,'wall',round(record['wallSeconds'],3),'record',out/'record.json','retained',artifactDirectories,flush=True)
 assert record['headAfter']==expected and not record['statusAfter']and before==after
 if error:raise RuntimeError('FIRST outcome retained; reconcile exact native process before any continuation: '+error)
 sys.exit(native)
if __name__=='__main__':main()
