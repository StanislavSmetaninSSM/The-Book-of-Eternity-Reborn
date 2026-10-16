from pathlib import Path
import subprocess,json,datetime,sys
R=Path('/workspace/The-Book-of-Eternity-Reborn');sha='93a72d6df0f59f5b5768b007ca51ad8b712ce9f3';prefix=Path('/tmp/t179pd-hot-custody-red2-full-accepted-20261016');assert not prefix.with_suffix('.log').exists()
argv=['python','specs/1536-complete-wound-materialization/recovery/t179pd-fresh-github-full-tree-restore-20261016.py',sha,'/workspace/f1-r2-hot-custody-red2-full-accepted-github-20261016','/tmp/t179pd-hot-custody-red2-full-accepted-recovery-20261016.json'];start=datetime.datetime.now(datetime.timezone.utc)
with prefix.with_suffix('.log').open('w') as log:p=subprocess.run(argv,cwd=R,stdout=log,stderr=subprocess.STDOUT)
result=dict(argv=argv,native_exit=p.returncode,actual_head=sha,started_utc=start.isoformat(),ended_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='GitHub full current-tree source recovery ONLY; no build/discovery/tests/application runtime');prefix.with_suffix('.exit.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result));sys.exit(p.returncode)
