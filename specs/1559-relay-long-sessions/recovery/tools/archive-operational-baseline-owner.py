"""Read-only archival of an already terminal FIRST owner; never invokes native code."""
from pathlib import Path
import json,sys,hashlib,gzip,datetime
record=Path(sys.argv[1]);destination=Path(sys.argv[2]);assert not destination.exists();destination.mkdir(parents=True)
raw_record=record.read_bytes();command=json.loads(raw_record);assert command['category'] in {'gm-relay-operational-live-preparation','gm-relay-operational-save-load'}
paths=[record,Path(command['stdoutPath']),Path(command['processRecord']),Path(command['firstLaunchSentinel'])]
for result in command['resultDirectories']:paths.extend(p for p in sorted(Path(result).rglob('*')) if p.is_file())
files=[]
def h(b):return hashlib.sha256(b).hexdigest()
for i,p in enumerate(sorted(set(paths))):
 data=p.read_bytes();name=str(i).zfill(3)+'-'+p.name+'.gz';out=destination/name;encoded=gzip.compress(data,compresslevel=9,mtime=0);out.write_bytes(encoded);assert gzip.decompress(out.read_bytes())==data
 files.append(dict(rawPath=str(p),storedPath=name,bytes=len(data),sha256=h(data),storedBytes=len(encoded),storedSha256=h(encoded)))
manifest=dict(kind='EXACT_FIRST_OWNER_ARCHIVE_NOT_CAUSAL_ACCEPTANCE',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=command['actualSource'],compiledSource=command['compiledSource'],productSource=command['productSource'],category=command['category'],recordPath=str(record),nativeExit=command['nativeExit'],rawChainCount=len(files),files=files,scope='Existing terminal raw only; no execution, cleanup/deletion, retry, source mutation or RED classification.',helperText=Path(__file__).read_text())
(destination/'archive.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps(dict(archive=str(destination/'archive.json'),sha256=h((destination/'archive.json').read_bytes()),rawChains=len(files),nativeExit=command['nativeExit'])))
