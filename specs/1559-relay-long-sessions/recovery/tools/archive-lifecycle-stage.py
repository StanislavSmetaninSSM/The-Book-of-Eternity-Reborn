"""Read-only exact terminal guard archive, including original native-terminal artifacts."""
from pathlib import Path
import json,sys,hashlib,gzip,datetime
record_path=Path(sys.argv[1]);destination=Path(sys.argv[2]);assert not destination.exists();destination.mkdir(parents=True)
record=json.loads(record_path.read_text());assert record['kind'] in {'FIRST_INACTIVE_LIFECYCLE_GREEN47_ORIGINAL_RAW_NOT_ACCEPTANCE','FIRST_INACTIVE_LIFECYCLE_PRODUCTION_ZERO_TEST_PREP_NOT_RUNTIME_ACCEPTANCE'}
def h(b):return hashlib.sha256(b).hexdigest()
paths=[record_path,Path(record['stdoutPath']),Path(record['processRecord']),Path(record['firstSentinel'])]
for directory in record['resultDirectories']:paths.extend(p for p in sorted(Path(directory).rglob('*')) if p.is_file())
for row in record.get('fixtureArtifactFiles',[]):
 p=Path(row['path']);b=p.read_bytes();assert len(b)==row['bytes'] and h(b)==row['sha256'];paths.append(p)
files=[]
for i,p in enumerate(sorted(set(paths))):
 b=p.read_bytes();name=f'{i:03d}-'+p.name+'.gz';encoded=gzip.compress(b,compresslevel=9,mtime=0);(destination/name).write_bytes(encoded);assert gzip.decompress((destination/name).read_bytes())==b
 files.append(dict(rawPath=str(p),storedPath=name,bytes=len(b),sha256=h(b),storedBytes=len(encoded),storedSha256=h(encoded)))
j=dict(kind='EXACT_ORIGINAL_GUARD_NATIVE_AND_FIXTURE_ARCHIVE_NOT_CAUSAL_VERDICT',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSource=record['actualSource'],compiledSource=record['compiledSource'],selection=record['selection'],nativeExit=record['nativeExit'],rawChainCount=len(files),fixtureArtifactCount=len(record.get('fixtureArtifactFiles',[])),fixtureArtifactDirectories=record.get('newFixtureArtifactDirectories',[]),files=files,helperText=Path(__file__).read_text(),scope='Read-only exact FIRST raw, no execution, cleanup/deletion, retry or classification.')
(destination/'archive.json').write_text(json.dumps(j,indent=2)+'\n');print(json.dumps(dict(archive=str(destination/'archive.json'),sha256=h((destination/'archive.json').read_bytes()),rawChains=len(files))))
