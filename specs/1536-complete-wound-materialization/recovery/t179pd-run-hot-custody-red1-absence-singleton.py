from pathlib import Path
import subprocess,json,hashlib,sys,datetime
r=Path('/workspace/The-Book-of-Eternity-Reborn');category,prefix,actual=sys.argv[1:]
allowed={'fr063-joint-late-completion','fr063-hot-joint-attempt-ownership'}
assert category in allowed and prefix.startswith('t179pd-hot-custody-red1-absence-') and '/'not in prefix
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==actual
assert not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
m=json.loads((r/'specs/1536-complete-wound-materialization/recovery/t179pd-hot-custody-red1-absence-prep-binaries-20261016.json').read_text())
assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in subprocess.check_output(['git','diff','--name-only',m['source_sha'],actual],cwd=r,text=True).splitlines())
for b in m['binaries']:assert hashlib.sha256((r/b['path']).read_bytes()).hexdigest()==b['sha256']
p=Path('/tmp')/(prefix+'-attempt1');assert not p.with_suffix('.log').exists()
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1','-Category',category,'-NoBuild']
prefixes=['boe-gameengine-turnlifecycle-']
def directories():return {prefix:sorted(str(path) for path in Path('/tmp').glob(prefix+'*') if path.is_dir()) for prefix in prefixes}
before_directories=directories()
started=datetime.datetime.now(datetime.timezone.utc)
print('ACTUAL_CLEAN_HEAD',actual,'COMPILED',m['source_sha'],'CATEGORY',category,flush=True)
with p.with_suffix('.log').open('w') as log: result=subprocess.run(argv,cwd=r,stdout=log,stderr=subprocess.STDOUT)
e={'argv':argv,'native_exit':result.returncode,'actual_head':actual,'compiled_source':m['source_sha'],'category':category,'started_utc':started.isoformat(),'ended_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'binaries_unchanged':all(hashlib.sha256((r/b['path']).read_bytes()).hexdigest()==b['sha256'] for b in m['binaries'])}
after_directories=directories()
e['scoped_temp_prefixes_before']=before_directories
e['scoped_temp_prefixes_after']=after_directories
e['new_scoped_temp_residue']={prefix:sorted(set(after_directories[prefix])-set(before_directories[prefix])) for prefix in prefixes}
e['post_head_unchanged']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==actual
e['post_git_clean']=not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
p.with_suffix('.exit.json').write_text(json.dumps(e,indent=2)+'\n');print(json.dumps(e),flush=True);print('\n'.join(p.with_suffix('.log').read_text().splitlines()[-28:]));sys.exit(result.returncode)
