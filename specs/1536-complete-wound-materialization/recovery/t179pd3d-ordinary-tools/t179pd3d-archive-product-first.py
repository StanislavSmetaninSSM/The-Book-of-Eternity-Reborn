"""UNRUN read-only exact FIRST product Late2 or ordinary52 raw archive; no retry, execution or classification."""
from pathlib import Path
import json,sys,hashlib,gzip,datetime
record_path=Path(sys.argv[1]);destination=Path(sys.argv[2]);assert not destination.exists();destination.mkdir(parents=True)
record=json.loads(record_path.read_text());assert record['kind']=='FIRST_PD3D_PRODUCT_RAW_NOT_EVIDENCE_ACCEPTANCE'
def h(b):return hashlib.sha256(b).hexdigest()
paths=[record_path,Path(record['stdoutPath']),Path(record['processRecord']),Path(record['firstSentinel']),record_path.parent/'source-binding.json']
for d in record['resultDirectories']:paths.extend(p for p in sorted(Path(d).rglob('*')) if p.is_file())
files=[]
for i,p in enumerate(sorted(set(paths))):
 b=p.read_bytes();name=f'{i:03d}-'+p.name+'.gz';compressed=gzip.compress(b,compresslevel=9,mtime=0);(destination/name).write_bytes(compressed);assert gzip.decompress((destination/name).read_bytes())==b
 files.append(dict(rawPath=str(p),storedPath=name,bytes=len(b),sha256=h(b),storedBytes=len(compressed),storedSha256=h(compressed)))
j=dict(kind='EXACT_FIRST_PD3D_PRODUCT_RAW_ARCHIVE_NOT_EVIDENCE_VERDICT',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=record['actualSource'],compiledSource=record['compiledSource'],nativeExit=record['nativeExit'],rawChainCount=len(files),files=files,helperText=Path(__file__).read_text(),scope='Read-only complete original native stdout/process/sourcecontext/sentinel/runner/TRX files. No runtime/delete/retry/qualification or inferred case outcome.')
(destination/'archive.json').write_text(json.dumps(j,indent=2)+'\n');print(json.dumps(dict(archive=str(destination/'archive.json'),sha256=h((destination/'archive.json').read_bytes()),rawChains=len(files))))
