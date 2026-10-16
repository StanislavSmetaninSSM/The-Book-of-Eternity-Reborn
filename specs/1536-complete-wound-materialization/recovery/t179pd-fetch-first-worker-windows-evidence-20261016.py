from pathlib import Path,PurePosixPath
import subprocess,json,hashlib,datetime,concurrent.futures,zipfile,stat,re
root=Path('/workspace/The-Book-of-Eternity-Reborn'); prefix=Path('/tmp/t179pd-hot-worker-windows-first-outcome-20261016');run=38978917195;repo='StanislavSmetaninSSM/The-Book-of-Eternity-Reborn';source='53a38ff8d6e18e441e4d6f53a0738bc6bccc64b6'
assert not prefix.with_suffix('.json').exists() and not prefix.with_suffix('.zip').exists()
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()==source and not subprocess.check_output(['git','status','--porcelain'],cwd=root)
observations=[]
def get(label,endpoint):
 started=datetime.datetime.now(datetime.timezone.utc).isoformat();argv=['gh','api','repos/'+repo+'/'+endpoint];p=subprocess.run(argv,cwd=root,capture_output=True)
 # Signed redirect query strings are never persisted in error diagnostics.
 safe=re.sub(r'(https?://[^\s?]+)\?[^\s]+',r'\1?[REDACTED]',p.stderr.decode(errors='replace'))
 return dict(label=label,argv=argv,startedUtc=started,endedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),nativeExit=p.returncode,stdoutSha256=hashlib.sha256(p.stdout).hexdigest(),stdoutBytes=len(p.stdout),stderrRedacted=safe),p.stdout
requests=[('run',f'actions/runs/{run}'),('jobs',f'actions/runs/{run}/jobs'),('artifacts',f'actions/runs/{run}/artifacts')]
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as ex:results=list(ex.map(lambda p:get(*p),requests))
data={}
for (record,body) in results:
 observations.append(record)
 if record['nativeExit']==0:
  data[record['label']]=json.loads(body);out=prefix.parent/(prefix.name+'-'+record['label']+'.json');assert not out.exists();out.write_bytes(body)
receipt=dict(source=source,runId=run,observations=observations,scope='Official read-only GitHub metadata and first artifact retrieval only; no workflow/settings mutation, dispatch, retry, runtime, or denied-route workaround.')
def save():prefix.with_suffix('.json').write_text(json.dumps(receipt,indent=2)+'\n')
save()
assert len(data)==3,'Metadata/access unavailable; STOP, no alternative route or retry.'
r=data['run'];assert r['status']=='completed' and r['head_sha']==source and r['run_attempt']==1 and r['event']=='push' and r['head_branch']=='1536-wound-b2-cloud-20261012'
assert data['jobs']['total_count']==1 and data['jobs']['jobs'][0]['id']==117234913253
arts=data['artifacts']['artifacts'];expected='fr063-hot-worker-windows-'+source+'-1';matches=[a for a in arts if a['name']==expected and not a['expired']]
receipt['workflowConclusion']=r['conclusion'];receipt['matchingArtifactCount']=len(matches);save();assert len(matches)==1,'Necessary artifact unavailable; STOP without triggering anything.'
a=matches[0];record,body=get('artifact-zip',f"actions/artifacts/{a['id']}/zip");observations.append(record);receipt['artifact']=a;save()
assert record['nativeExit']==0,'Artifact access denied/unavailable; STOP without alternate transfer route.'
archive=prefix.with_suffix('.zip');archive.write_bytes(body);digest=hashlib.sha256(body).hexdigest();receipt['archive']=dict(path=str(archive),bytes=len(body),sha256=digest)
if a.get('digest'):assert a['digest']=='sha256:'+digest
out=prefix.parent/(prefix.name+'-artifact');assert not out.exists();out.mkdir()
rows=[]
with zipfile.ZipFile(archive) as z:
 infos=z.infolist();assert len(infos)<1000 and sum(i.file_size for i in infos)<500_000_000 and len({i.filename for i in infos})==len(infos)
 for i in infos:
  path=PurePosixPath(i.filename);assert not path.is_absolute() and '..' not in path.parts and '\\' not in i.filename and ':' not in i.filename and not stat.S_ISLNK(i.external_attr>>16)
  p=out.joinpath(*path.parts)
  if i.is_dir():p.mkdir(parents=True,exist_ok=True);continue
  raw=z.read(i);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(raw);rows.append(dict(path=i.filename,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
receipt['extracted']=dict(directory=str(out),files=rows);save();print('FIRST_NATIVE_OUTCOME_RETRIEVED',r['conclusion'],a['id'],len(rows),'files',digest)
