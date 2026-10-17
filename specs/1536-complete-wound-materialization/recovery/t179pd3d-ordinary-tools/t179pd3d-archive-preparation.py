"""Read-only exact FIRST zero-test raw archive. No runtime, cleanup, retry or classification."""
from pathlib import Path
import json,sys,hashlib,gzip,datetime
record_paths=[Path(x) for x in sys.argv[1:-1]];destination=Path(sys.argv[-1]);assert record_paths and not destination.exists();destination.mkdir(parents=True)
def h(b):return hashlib.sha256(b).hexdigest()
files=[];paths=[];records=[]
for p in record_paths:
 j=json.loads(p.read_text());assert j['kind']=='FIRST_PD3D_ORDINARY_ZERO_EXECUTION_PREPARATION_NOT_RUNTIME_ACCEPTANCE';records.append(j)
 paths.extend([p,Path(j['stdoutPath']),Path(j['processRecord']),Path(j['firstSentinel']),p.parent/'source-binding.json'])
 for d in j['resultDirectories']:paths.extend(q for q in sorted(Path(d).rglob('*')) if q.is_file())
for i,p in enumerate(sorted(set(paths))):
 b=p.read_bytes();name=f'{i:03d}-'+p.name+'.gz';compressed=gzip.compress(b,compresslevel=9,mtime=0);(destination/name).write_bytes(compressed);assert gzip.decompress((destination/name).read_bytes())==b
 files.append(dict(rawPath=str(p),storedPath=name,bytes=len(b),sha256=h(b),storedBytes=len(compressed),storedSha256=h(compressed)))
j=dict(kind='EXACT_PD3D_ORDINARY_PREPARATION_RAW_ARCHIVE_NOT_SOURCE_OR_RUNTIME_ACCEPTANCE',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSources=[x['actualSource'] for x in records],compiledSources=[x['compiledSource'] for x in records],nativeExits=[x['nativeExit'] for x in records],actualTestExecutions=[x['actualTestsExecuted'] for x in records],rawChainCount=len(files),files=files,helperText=Path(__file__).read_text(),scope='All original FIRST stdout/record/source context/process/sentinel/runner-result files, deterministic gzip exact roundtrip; no modification of raw or physical managed outputs.')
(destination/'archive.json').write_text(json.dumps(j,indent=2)+'\n');print(json.dumps(dict(archive=str(destination/'archive.json'),sha256=h((destination/'archive.json').read_bytes()),rawChains=len(files))))
