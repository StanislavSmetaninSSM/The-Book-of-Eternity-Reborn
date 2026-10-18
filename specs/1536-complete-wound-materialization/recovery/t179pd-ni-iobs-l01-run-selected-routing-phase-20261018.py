"""One explicitly invoked phase, original outcome then immediate after-census; no auto next/retry."""
from pathlib import Path
import sys,json,subprocess
r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery';t=Path('/tmp/1536-cloud-tools')
kind,mode,actual=sys.argv[1:4];packet=json.loads((d/'t179pd-ni-iobs-l01-source-packet-20261018.json').read_bytes())
assert kind in {'prep','actual'}
if kind=='prep':
 release=packet['namedZeroRelease'];assert release['status']=='SOURCE_ACCEPTED_SAVED_RECOVERED_ZERO4_ONLY'
 label=release['labels'][mode];controller=d/'t179pd-ni-iobs-l01-process-preparation-run.py';args=[mode,label,actual];tag={'plan':'whole','corrective-plan':'corrective','remaining-plan':'remaining'}.get(mode,mode)
 if mode!='catalog':
  catalog_record=Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/release['labels']['catalog']/'record.json'
  assert json.loads(catalog_record.read_bytes())['nativeExit']==0
  args.append(str(catalog_record))
else:
 release=packet['namedActualRelease'];assert release['mode']==mode and release['status']=='SEPARATELY_NAMED_AFTER_SAVED_ACCEPTANCE_RECOVERY_AND_IMMEDIATE_CLEAN_V2_CENSUS'
 label=release['label'];controller=d/'t179pd-ni-iobs-l01-process-first-run.py';args=[mode,label,actual,release['prepRecord']];tag=mode
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==actual
assert not subprocess.check_output(['git','status','--porcelain'],cwd=r)
assert not (Path('/tmp/t179pd-ni-iobs-l01-process-implementation-tools')/label).exists()
observer=d/'t179pd-ni-iobs-l01-process-census-v2-20261018.py';root='/tmp/ni-iobs-l01-cloud-20261018-813f1057c7cb45e18fff05e7690ca320'
def census(side):
 name=f'census-selected-routing-{tag}-{side}-813f1057.json';return subprocess.run([sys.executable,str(observer),'--phase',f'selected-routing-{tag}-{side}','--fixture-temp',root,'--known','10433:477854','--out',str(t/name)],cwd=r).returncode
assert census('before')==0,'No native release on nonclean before-boundary'
# Return handling and after census share this exact invocation, no model callback gap.
try:native=subprocess.run([sys.executable,str(controller),*args],cwd=r).returncode
finally:after=census('after')
print(json.dumps(dict(kind=kind,mode=mode,label=label,controllerExit=native,afterCensusExit=after)),flush=True)
sys.exit(native if native else after)
