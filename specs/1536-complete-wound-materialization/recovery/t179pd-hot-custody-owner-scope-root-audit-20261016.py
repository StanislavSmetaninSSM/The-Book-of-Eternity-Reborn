from pathlib import Path
import subprocess,json,hashlib,datetime
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';S='f186abee075654859d536073d8a582f213af0529';h=lambda b:hashlib.sha256(b).hexdigest()
p=Path('/tmp/t179pd-hot-custody-red2-owner-scope-recovery-20261016.json');raw=p.read_bytes();j=json.loads(raw);assert j['sha']==S and j['clean'] and j['noAlternates'] and j['new_directory'] and j['restoredBuildOrRuntime']=='NOT_RUN'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip()==S and not subprocess.check_output(['git','status','--porcelain'],cwd=R,text=True)
keys=[['source_evidence.candidate_pins'],['source_pins'],['source_pins','current_governance_and_extra_pins'],['source_pins'],['source_evidence.author_historical_pins','source_evidence.candidate_pins']];counts=[52,105,113,21,42];required=set()
for scope,ks,count in zip(j['dependencyScopes'],keys,counts,strict=True):
 q=D/scope['report'];b=q.read_bytes();assert h(b)==scope['sha256'] and b==subprocess.check_output(['git','show',S+':'+str(q.relative_to(R))],cwd=R);data=json.loads(b);paths=set()
 for key in ks:
  value=data
  for part in key.split('.'):value=value[part]
  paths.update(a['path'] for a in value)
 assert len(paths)==scope['count']==count and paths==set(scope['paths']);required.update(paths)
assert len(required)==145 and required==set(j['concreteRequiredPaths']);files={a['path']:a for a in j['files']};assert len(files)==j['count']==1348 and required<=set(files)
clone=Path(j['directory']);assert clone.parent==Path('/tmp') and clone!=R and not(clone/'.git/objects/info/alternates').exists();assert not subprocess.check_output(['git','-C',str(clone),'status','--porcelain','--ignored'],text=True)
for name,a in files.items():
 b=(clone/name).read_bytes();assert len(b)==a['bytes'] and h(b)==a['sha256'] and b==(R/name).read_bytes()==subprocess.check_output(['git','show',S+':'+name],cwd=R)
old=json.loads((D/'t179pd-hot-custody-red2-owned-prepared-recovery-20261016.json').read_text());oldpaths={a['path'] for a in old['files']};assert len(required-oldpaths)==66
m=json.loads((D/'t179pd-hot-custody-red2-owned-prep-binaries-20261016.json').read_text());assert m['source_sha']=='d474cdd8546f8157b6ffd71840f268062ee05ed0'
for a in m['binaries']:assert h((R/a['path']).read_bytes())==a['sha256']
assert all(name.startswith('specs/1536-complete-wound-materialization/') for name in subprocess.check_output(['git','diff','--name-only',m['source_sha'],S],cwd=R,text=True).splitlines())
remote=subprocess.check_output(['git','ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012','refs/heads/main'],cwd=R,text=True);assert remote==j['remoteReadback']
out=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=S,receipt=dict(path=str(p),sha256=h(raw)),exact_recovered_files=len(files),independently_reconstructed_required_paths=len(required),scope_counts=counts,additional_required_paths_vs_1250=len(required-oldpaths),all_raw_sizes_hashes_writer_git_equal=True,clean_no_ignored_runtime_no_alternates=True,four_d474_binaries_unchanged=True,compiled_source_to_candidate_task_documents_only=True,remote_readback=remote,restored_build_or_runtime='NOT_RUN',scope='Checkpoint-specific root verification of selected concrete owner/policy recovery, not a full checkout/build/runtime or release decision')
q=Path('/tmp/t179pd-hot-custody-owner-scope-root-audit-20261016.json');assert not q.exists();q.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
