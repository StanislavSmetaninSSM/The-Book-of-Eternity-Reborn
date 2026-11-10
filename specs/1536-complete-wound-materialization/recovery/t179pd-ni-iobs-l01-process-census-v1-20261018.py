"""Read-only L01 process/root boundary observer; argv/stat identity precedes optional exe link."""
import argparse,datetime,hashlib,json,os,re,sys
from pathlib import Path
ap=argparse.ArgumentParser();ap.add_argument('--phase',required=True);ap.add_argument('--out',required=True);ap.add_argument('--fixture-temp',required=True);ap.add_argument('--known',action='append',default=[]);a=ap.parse_args()
out=Path(a.out);assert not out.exists() and out.parent.is_dir()
root=Path(a.fixture_temp);assert root.is_dir() and root.stat().st_uid==os.getuid()
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def sha(b):return hashlib.sha256(b).hexdigest()
def stat(pid):
 raw=(Path('/proc')/str(pid)/'stat').read_text();end=raw.rfind(')');parts=raw[end+2:].split();assert int(raw.split(' ',1)[0])==pid and len(parts)>19
 return dict(pid=pid,comm=raw[raw.index('(')+1:end],state=parts[0],ppid=int(parts[1]),startTick=int(parts[19]),raw=raw)
known={int(x.split(':')[0]):int(x.split(':')[1]) for x in a.known};self_id=stat(os.getpid());known[os.getpid()]=self_id['startTick']
started=now();entries=[];gone=[];unknown=[];pids=sorted(int(d.name) for d in Path('/proc').iterdir() if d.name.isdigit())
target=re.compile(r'^BookOfEternity(?:Client(?:\.TestSupport|\.IntegrationTests|\.Tests)?|GMBridge)(?:\.dll)?$')
for pid in pids:
 try:before=stat(pid)
 except FileNotFoundError:gone.append(dict(pid=pid,stage='stat-before'));continue
 except (OSError,ValueError,AssertionError) as e:unknown.append(dict(pid=pid,stage='stat-before',error=repr(e)));continue
 try:
  raw=(Path('/proc')/str(pid)/'cmdline').read_bytes();argv=[x.decode('utf-8',errors='surrogateescape') for x in raw.split(b'\0') if x]
  exe={}
  try:exe=dict(path=os.readlink(Path('/proc')/str(pid)/'exe'),access='READ')
  except OSError as e:exe=dict(access='UNAVAILABLE_OPTIONAL',errno=e.errno,error=type(e).__name__)
  after=stat(pid)
 except FileNotFoundError:gone.append(dict(pid=pid,startTick=before['startTick'],stage='collection-or-stat-after'));continue
 except (OSError,ValueError,AssertionError) as e:unknown.append(dict(pid=pid,startTick=before['startTick'],stage='argv-or-stat-after',error=repr(e)));continue
 if before['startTick']!=after['startTick']:
  unknown.append(dict(pid=pid,stage='PID_REUSE_DURING_COLLECTION',before=before,after=after));continue
 names=[Path(x).name for x in argv];first=names[0] if names else '';kind='other-readable';reasons=[]
 if not argv:
  if after['state'] in {'Z','X','x'}:kind='exited-nonexecuting'
  else:kind='UNKNOWN';reasons.append('live-empty-argv')
 elif pid==os.getpid():kind='observer-self'
 elif any(target.fullmatch(x) for x in names):kind='game-or-test';reasons.append('target-DLL-or-apphost-argv')
 elif first in {'dotnet','testhost','vstest.console','VBCSCompiler'} or before['comm'] in {'dotnet','testhost','VBCSCompiler'}:
  if any(x in {'MSBuild.dll','VBCSCompiler.dll','VBCSCompiler'} for x in names) or (first=='dotnet' and len(argv)>1 and argv[1] in {'build','msbuild','restore'}):kind='build-infrastructure'
  elif '--list-tests' in argv or '/ListTests' in argv:kind='task-discovery'
  elif any(x in {'testhost.dll','testhost','vstest.console.dll'} for x in names) or (first=='dotnet' and len(argv)>1 and argv[1] in {'test','vstest'}):kind='test-launcher-or-host'
  else:kind='UNKNOWN';reasons.append('unclassified-managed-runtime')
 elif first in {'pwsh','powershell'} and any(x=='test-csharp.ps1' for x in names):kind='task-runner'
 elif first.startswith('python') and any(x in {'t179pd-ni-iobs-l01-process-first-run.py','t179pd-ni-iobs-l01-process-preparation-run.py'} for x in names):kind='task-controller'
 row=dict(pid=pid,startTick=after['startTick'],ppid=after['ppid'],comm=after['comm'],state=after['state'],statBefore=before['raw'],statAfter=after['raw'],argv0=first,argvCount=len(argv),argvSha256=sha(raw),exe=exe,classification=kind,reasons=reasons,readUtc=now())
 if kind not in {'other-readable','exited-nonexecuting','observer-self'}:row['argv']=argv
 if pid in known:
  row['knownExpectedStartTick']=known[pid];row['knownIdentityMatched']=known[pid]==after['startTick']
  if not row['knownIdentityMatched']:row['classification']='UNKNOWN';row['reasons'].append('known-PID-birth-mismatch')
 entries.append(row)
# Mark descendants of positively classified task/game runners, without exempting managed build infrastructure.
active_kinds={'game-or-test','task-discovery','test-launcher-or-host','task-runner','task-controller'}
owned={x['pid'] for x in entries if x['classification'] in active_kinds}
for _ in range(len(entries)):
 added={x['pid'] for x in entries if x['ppid'] in owned}-owned
 if not added:break
 owned.update(added)
for x in entries:
 if x['pid'] in owned and x['classification']=='other-readable':x['classification']='task-descendant'
 if x['classification']=='UNKNOWN':unknown.append(dict(pid=x['pid'],startTick=x['startTick'],stage='classification',reasons=x['reasons']))
known_visibility=[]
for pid,tick in known.items():
 matches=[x for x in entries if x['pid']==pid]
 known_visibility.append(dict(pid=pid,expectedStartTick=tick,status=('MATCH' if matches and matches[0].get('knownIdentityMatched') else 'UNKNOWN' if matches else 'GONE'),classification=matches[0]['classification'] if matches else None))
active=[x for x in entries if x['classification'] in active_kinds|{'task-descendant'}]
roots=sorted(str(p) for pattern in ['boe-gameengine-turnlifecycle-*','boe-joint-interface-crash-*'] for p in root.glob(pattern))
record=dict(kind='L01_SUPPLEMENTAL_ARGV_STAT_PROCESS_ROOT_BOUNDARY',phase=a.phase,startedUtc=started,finishedUtc=now(),observerSource=str(Path(__file__)),observerSourceSha256=sha(Path(__file__).read_bytes()),observerPid=os.getpid(),fixtureTemp=str(root),fixtureTempUid=root.stat().st_uid,pidsEnumerated=pids,entries=entries,confirmedGone=gone,unknown=unknown,knownVisibility=known_visibility,activeTaskGameTestProcesses=active,buildInfrastructure=[x for x in entries if x['classification']=='build-infrastructure'],ownedRoots=roots,cleanBoundary=not active and not unknown and not roots,limitations='Dated boundary only. Optional exe-link errors never suppress argv/stat classification. Non-target argv is hashed rather than retained to avoid unrelated command arguments. No historical snapshot reconstruction, process intervention, runtime/test execution or external state access.')
with out.open('x') as f:json.dump(record,f,ensure_ascii=True,indent=2);f.write('\n')
print(json.dumps(dict(path=str(out),sha256=sha(out.read_bytes()),cleanBoundary=record['cleanBoundary'],entries=len(entries),knownVisibility=known_visibility,active=[dict(pid=x['pid'],classification=x['classification']) for x in active],unknown=unknown,ownedRoots=roots)))
sys.exit(0 if record['cleanBoundary'] else 2)
