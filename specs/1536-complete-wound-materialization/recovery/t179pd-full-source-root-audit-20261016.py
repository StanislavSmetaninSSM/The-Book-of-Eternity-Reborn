from pathlib import Path
import json,subprocess,hashlib,datetime
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery';sha='bc719d3f6002965167c5915d841e56c5dc45449a';h=lambda b:hashlib.sha256(b).hexdigest()
p=Path('/tmp/t179pd-hot-custody-red2-full-source-recovery-20261016.json');raw=p.read_bytes();j=json.loads(raw);ex=json.loads(Path('/tmp/t179pd-hot-custody-red2-full-source-20261016.exit.json').read_text());assert ex['native_exit']==0 and ex['actual_head']==j['sha']==sha
assert j['new_directory'] and j['clean'] and j['noAlternates'] and j['noSparseCheckout'] and j['restoredBuildOrRuntime']=='NOT_RUN';assert len(j['files'])==j['count']==54149 and sum(a['bytes'] for a in j['files'])==j['tracked_bytes']==3556631142
files={a['path']:a for a in j['files']};assert len(files)==54149
for row in subprocess.check_output(['git','ls-tree','-rlz',sha],cwd=R).split(b'\0'):
 if not row:continue
 meta,name=row.split(b'\t',1);mode,kind,blob,size=meta.split();a=files[name.decode()];assert a['mode']==mode.decode() and a['git_blob']==blob.decode() and a['bytes']==int(size)
audit=json.loads((D/'t179pd-recovery-scope-root-cause-audit-20261016.json').read_text());assert len(audit['omitted_concrete_paths'])==17
for name in audit['omitted_concrete_paths']:
 a=files[name];b=(Path(j['directory'])/name).read_bytes();assert h(b)==a['sha256'] and b==(R/name).read_bytes()==subprocess.check_output(['git','show',sha+':'+name],cwd=R)
assert not subprocess.check_output(['git','-C',j['directory'],'status','--porcelain','--ignored'],text=True)
m=json.loads((D/'t179pd-hot-custody-red2-owned-prep-binaries-20261016.json').read_text())
for a in m['binaries']:assert h((R/a['path']).read_bytes())==a['sha256']
assert all(n.startswith('specs/1536-complete-wound-materialization/') for n in subprocess.check_output(['git','diff','--name-only',m['source_sha'],sha],cwd=R,text=True).splitlines())
assert subprocess.check_output(['git','ls-remote','origin','refs/heads/1536-wound-b2-cloud-20261012','refs/heads/main'],cwd=R,text=True)==j['remoteReadback']
out=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),candidate=sha,receipt_sha256=h(raw),native_exit=0,full_git_tree_metadata_entries_verified=54149,tracked_bytes=3556631142,previously_missing_paths_raw_writer_git_verified=17,full_raw_comparison='Root executed pinned helper verifies every raw body/mode against Git and writer; separate reviewer independently rechecks full bodies.',four_d474_binaries_unchanged=True,source_to_carrier_only_task_documents=True,no_build_or_runtime=True,scope='Root source-recovery verification only; independent R01 closure remains a separate decision')
q=Path('/tmp/t179pd-full-source-root-audit-20261016.json');assert not q.exists();q.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out))
