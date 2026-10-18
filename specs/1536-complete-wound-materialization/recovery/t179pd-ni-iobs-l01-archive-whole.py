"""UNRUN read-only complete original whole-product zero/actual raw archive. No execution, cleanup, retry or classification."""
from pathlib import Path
import sys,json,gzip,hashlib,datetime
records=[Path(x) for x in sys.argv[1:-1]];out=Path(sys.argv[-1]);assert records and not out.exists();out.mkdir(parents=True)
def h(b):return hashlib.sha256(b).hexdigest()
paths=[];original=[]
for p in records:
 j=json.loads(p.read_bytes());assert j['kind'] in {'FIRST_NI_IOBS_L01_WHOLE_ZERO_PREPARATION_RAW','FIRST_NI_IOBS_L01_WHOLE_ACTUAL_RAW_NOT_EVIDENCE'};original.append(j)
 paths.extend([p,Path(j['stdoutPath']),Path(j['processRecord']),Path(j['firstSentinel']),p.parent/'source-binding.json',p.parent/'ledger-context.json'])
 for name in ['observed-plan.json','exact-selected-scope.json']:
  if (p.parent/name).is_file():paths.append(p.parent/name)
 for d in j['resultDirectories']:paths.extend(q for q in sorted(Path(d).rglob('*')) if q.is_file())
 # Normal tests delete only their positively settled owned roots. If an actual
 # unresolved child retained its nonce container, freeze it without deletion.
 for root in j['namespaceAfter']['ownedRoots']:
  path=Path(root);assert path.exists();paths.extend(q for q in sorted(path.rglob('*')) if q.is_file())
files=[]
for i,p in enumerate(sorted(set(paths))):
 b=p.read_bytes();name=f'{i:05d}-'+p.name+'.gz';stored=gzip.compress(b,compresslevel=9,mtime=0);(out/name).write_bytes(stored);assert gzip.decompress((out/name).read_bytes())==b
 files.append(dict(rawPath=str(p),storedPath=name,bytes=len(b),sha256=h(b),storedBytes=len(stored),storedSha256=h(stored)))
archive=dict(kind='EXACT_NI_IOBS_L01_WHOLE_ORIGINAL_RAW_ARCHIVE_NOT_VERDICT',createdUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),actualSources=[x['actualSource'] for x in original],compiledSources=[x['compiledSource'] for x in original],nativeExits=[x['nativeExit'] for x in original],rawChainCount=len(files),files=files,helperText=Path(__file__).read_text(),scope='All original record/source/ledger/process/sentinel/stdout/runner/TRX/retained-child artifacts, gzip roundtrip; no raw edits, runtime, deletion, retry or verdict.')
(out/'archive.json').write_text(json.dumps(archive,indent=2)+'\n');print(json.dumps(dict(archive=str(out/'archive.json'),sha256=h((out/'archive.json').read_bytes()),rawChains=len(files))))
