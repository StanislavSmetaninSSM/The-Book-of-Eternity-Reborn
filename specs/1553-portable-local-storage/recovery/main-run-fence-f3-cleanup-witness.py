from pathlib import Path
import json,re,hashlib,shutil
r=Path('/workspace/native-1553');e=r/'specs/1553-portable-local-storage/recovery/evidence'
folders=sorted({r/'TestResults/native-terminal'/p.parent.name for p in e.glob('t041-f3-*/native/*/guardian.json')})
h=lambda b:hashlib.sha256(b).hexdigest()
def snapshot(root):return {str(p.relative_to(root)):h(p.read_bytes()) for p in root.rglob('*') if p.is_file() and not str(p.relative_to(root)).startswith('.boe_runtime/locks/') and str(p.relative_to(root))!='.boe_runtime/worker-runs-v1/owner.lock'}
rows=[]
for p in folders:
 g=json.loads((p/'guardian.json').read_text());assert g['echild'] and not g['emergencySignals'] and not g['failures'] and not g['deadline']
 info=json.loads((p/'root.json').read_text());root=Path(info['Root']);assert root.parent==p and re.fullmatch(r'neutral-session-[0-9a-f]{32}',root.name)
 before=snapshot(root);runids=set();pipes=set()
 def scan(v):
  if isinstance(v,dict):
   for k,x in v.items():
    if k=='RunId' and isinstance(x,str) and re.fullmatch('[0-9a-f]{32}',x):runids.add(x)
    if k in ['Pipe','PipeName','pipeName'] and isinstance(x,str) and re.fullmatch('f3-[0-9a-f]{32}',x):pipes.add(x)
    scan(x)
  elif isinstance(v,list):
   for x in v:scan(x)
 for f in p.glob('*.json'):scan(json.loads(f.read_text()))
 removed=[]
 for ident in sorted(runids):
  t=Path('/tmp')/('boe-native-'+ident)
  if t.exists():assert t.is_dir() and not t.is_symlink();shutil.rmtree(t);removed.append(str(t))
 for pipe in sorted(pipes):
  t=Path('/tmp')/('CoreFxPipe_'+pipe)
  if t.exists():assert not t.is_dir() and not t.is_symlink();t.unlink();removed.append(str(t))
 assert before==snapshot(root)
 assert all(not (Path('/tmp')/('boe-native-'+i)).exists() for i in runids)
 assert all(not (Path('/tmp')/('CoreFxPipe_'+i)).exists() for i in pipes)
 rows.append(dict(Folder=str(p.relative_to(r)),GuardianEchild=True,KnownRunIds=sorted(runids),KnownPipeNames=sorted(pipes),Removed=removed,AllKnownPrivateResourcesAbsent=True,RootFileBytesUnchanged=True,RetainedRootFileCount=len(before)))
result=dict(SchemaVersion=1,Scope='Only exact current F3 isolated case witnesses after actual guardian ECHILD; private bootstrap and own f3 named-pipe socket paths',GuardianCases=len(rows),Rows=rows,LogicalUncertainAndCanonicalEvidenceRetained=True,NoPidSignalsOrUnrelatedResources=True,OldUnrelatedPid1ZombiesUntouched=True)
Path('/workspace/qualification-1553-main-fence-f3/final-fixture-cleanup.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(dict(Guardians=len(rows),RemovedPrivateResources=sum(len(x['Removed']) for x in rows),AllOwnRootBytesUnchanged=True,AllKnownPrivateResourcesAbsent=True)))
