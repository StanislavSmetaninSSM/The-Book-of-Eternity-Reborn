"""Exact FIRST G2 actual archive into a NEW /tmp directory; no launch/cleanup/retry."""
from pathlib import Path
import sys,json,hashlib,gzip,datetime

def sha(b):return hashlib.sha256(b).hexdigest()
def main():
 assert len(sys.argv)==3;record=Path(sys.argv[1]);dest=Path(sys.argv[2]);assert dest.is_absolute()and str(dest).startswith('/tmp/')and not dest.exists();dest.mkdir();j=json.loads(record.read_text());assert j['kind']=='FIRST_E02_REMAINING_G2_GREEN_RAW_NOT_INDEPENDENT_ACCEPTANCE';assert j['nativeProcessSettled'],'Do not archive live/uncertain raw; reconcile exact owner first';rows=[];skipped=[]
 def save(p,name,expected=None):
  p=Path(p)
  if p.is_symlink():skipped.append(dict(path=str(p),kind='SYMLINK_NOT_FOLLOWED',target=p.readlink().as_posix()));return
  assert p.is_file();b=p.read_bytes()
  if expected:assert len(b)==expected['bytes']and sha(b)==expected['sha256'],str(p)
  z=gzip.compress(b,mtime=0);target=dest/(name+'.gz');assert not target.exists();target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z);assert gzip.decompress(target.read_bytes())==b;rows.append(dict(rawPath=str(p),storedPath=name+'.gz',bytes=len(b),sha256=sha(b),storedBytes=len(z),storedSha256=sha(z),compression='gzip mtime0'))
 save(record,'native/record.json');save(j['stdoutPath'],'native/stdout.log');save(j['processRecord'],'native/process.json');save(j['firstLaunchSentinel'],'native/sentinel.json');save('/tmp/gm1558-g2-e02-remaining-green-first-run.py','support/approved-first-recorder.py');save(Path(__file__),'support/archive-helper.py')
 for p in j['resultFiles']:
  path=Path(p['path']);matches=[(i,Path(d))for i,d in enumerate(j['resultDirectories'])if path.is_relative_to(Path(d))];assert len(matches)==1;i,d=matches[0];save(path,'runner/'+str(i)+'/'+str(path.relative_to(d)),p)
 for p in j['fixtureArtifactFiles']:
  path=Path(p['path']);matches=[Path(d)for d in j['newRetainedArtifactDirectories']if path.is_relative_to(Path(d))];assert len(matches)==1;d=matches[0];assert d.parent==Path('/tmp')and d.name.startswith('boe-');save(path,'fixture-artifacts/'+d.name+'/'+str(path.relative_to(d)),p)
 root=Path(j['cwd']);save(root/j['sourceBindingPath'],'support/current-source-binding.json',dict(bytes=(root/j['sourceBindingPath']).stat().st_size,sha256=j['sourceBindingSha256']));save(j['reviewedPlanPath'],'support/reviewed-plan.json',dict(bytes=Path(j['reviewedPlanPath']).stat().st_size,sha256=j['reviewedPlanSha256']));save(j['packageManifestPath'],'support/fresh-package-manifest.json',dict(bytes=Path(j['packageManifestPath']).stat().st_size,sha256=j['packageManifestSha256']))
 result=dict(kind='EXACT_FIRST_E02_REMAINING_G2_GREEN_RAW_ARCHIVE_NOT_EVIDENCE_ACCEPTANCE',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=j['actualSource'],compiledSource=j['compiledSource'],retainedFrontendSource=j['retainedFrontendSource'],nativeExit=j['nativeExit'],recordPath=str(record),recordSha256=sha(record.read_bytes()),files=rows,skippedNonRegularArtifacts=skipped,scope='FIRST native/runner/TRX/frontend/process/sentinel/helper and observed retained fixture artifact bytes only. No execution/cleanup/migration/replay. A failed/incomplete command remains failed/incomplete; qualified counts and independent EVIDENCE remain separate.',helperText=Path(__file__).read_text());(dest/'archive.json').write_text(json.dumps(result,indent=2)+'\n');print('EXACT_FIRST_G2_ARCHIVE',dest,'chains',len(rows),'native',j['nativeExit'],'skipped',len(skipped))
if __name__=='__main__':main()
