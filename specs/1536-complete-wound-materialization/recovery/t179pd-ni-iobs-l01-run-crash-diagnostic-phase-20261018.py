"""One named diagnostic phase only; immutable frozen-root check and immediate v2 boundaries. No auto advance/retry."""
from pathlib import Path
import sys,json,subprocess,os,hashlib,datetime
r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery';t=Path('/tmp/1536-cloud-tools');stem='t179pd-ni-iobs-l01-'
kind,mode,actual=sys.argv[1:4];p=json.loads((d/f'{stem}source-packet-20261018.json').read_bytes());c=json.loads((d/f'{stem}cloud-qualification-20261018.json').read_bytes());ns=c['crashDiagnostic1']['runNamespace'];root=ns['tmpdir'];suffix=ns['identity'].split('-')[-1][:8]
assert c['qualifiedCases']['totalDistinctTargets']==52 and os.environ['TMPDIR']==root and Path(root).is_dir()
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==actual and not subprocess.check_output(['git','status','--porcelain'],cwd=r)
if kind=='prep':
 release=p['namedZeroRelease'];assert release['status']=='SOURCE_ACCEPTED_SAVED_RECOVERED_DIAGNOSTIC_ZERO3_ONLY' and mode in {'catalog','plan','diagnostic-plan'}
 label=release['labels'][mode];controller=d/f'{stem}process-preparation-run.py';args=[mode,label,actual]
 if mode!='catalog':
  catalog=Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/release['labels']['catalog']/'record.json';assert json.loads(catalog.read_bytes())['nativeExit']==0;args.append(str(catalog))
else:
 assert kind=='actual' and mode=='diagnostic1';release=p['namedActualRelease'];assert release['status']=='SEPARATELY_NAMED_ONE_DIAGNOSTIC_AFTER_SAVED_ACCEPTANCE_RECOVERY_AND_CLEAN_PREFLIGHT' and release['mode']==mode
 label=release['label'];controller=d/f'{stem}process-first-run.py';args=[mode,label,actual,release['prepRecord']]
assert not (Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/label).exists()
# Only read current-cloud frozen roots. This is not access to old failed66529 environment.
frozen=ns['frozenRootsOutsideMutableNamespace'];assert len(frozen)==2
archive=r/c['actuals']['crash5SelectedRouting']['archive']['path'];a=json.loads(archive.read_bytes());files={x['rawPath']:x for x in a['files'] if any(Path(x['rawPath']).is_relative_to(Path(z)) for z in frozen)}
actual_files={str(f) for z in frozen for f in Path(z).rglob('*') if f.is_file()};assert actual_files==set(files),'Frozen root file set changed'
for path,entry in files.items():
 b=Path(path).read_bytes();assert len(b)==entry['bytes'] and hashlib.sha256(b).hexdigest()==entry['sha256'],'Frozen root bytes changed'
rec=dict(kind='READ_ONLY_FROZEN_CURRENT_CLOUD_ROOTS_SEPARATION',checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=actual,mode=mode,newMutableRoot=root,physicalCloudUnchanged=True,frozenRoots=frozen,verifiedFiles=len(files),unchanged=True,limits='Read-only current-cloud frozen fixtures only; no root/process intervention, no old66529 access, no continuous absence claim.')
pre=t/f'diagnostic1-{mode}-frozen-separation-{suffix}.json';assert not pre.exists();pre.write_text(json.dumps(rec,indent=2)+'\n')
observer=d/f'{stem}process-census-v2-20261018.py'
def census(side):
 out=t/f'census-crash-diagnostic1-{mode}-{side}-{suffix}.json';assert not out.exists();return subprocess.run([sys.executable,str(observer),'--phase',f'crash-diagnostic1-{mode}-{side}','--fixture-temp',root,'--known','10433:477854','--out',str(out)],cwd=r).returncode
assert census('before')==0,'No native on active/UNKNOWN/new mutable roots'
try:native=subprocess.run([sys.executable,str(controller),*args],cwd=r).returncode
finally:after=census('after')
print(json.dumps(dict(kind=kind,mode=mode,label=label,controllerExit=native,afterCensusExit=after)),flush=True)
sys.exit(native if native else after)
