from pathlib import Path
import json,subprocess,hashlib,sys
root=Path('/workspace/The-Book-of-Eternity-Reborn'); branch='1536-wound-b2-cloud-20261012'; main='d0241e71e349fbe2020e4a41b6be7c627c81cbb4'
accepted,mdsha,jsonsha,recovery_path=sys.argv[1:]
source='8e0a5e40d28b103fa509f40dd7f1a5b293f6545c';stem='t179pd-hot-custody-worker-windows-source-review-20261016';base='specs/1536-complete-wound-materialization/recovery/'
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
def digest(b):return hashlib.sha256(b).hexdigest()
assert git('rev-parse','HEAD').decode().strip()==accepted and not git('status','--porcelain')
assert git('branch','--show-current').decode().strip()==branch
remote=git('ls-remote','origin','refs/heads/'+branch,'refs/heads/main').decode();assert accepted+'\trefs/heads/'+branch in remote and main+'\trefs/heads/main' in remote
receipt=json.loads(Path(recovery_path).read_text());assert receipt['sha']==accepted and receipt['deleted_paths']==[]
assert receipt['source']=='NEW GitHub raw exact-commit transport; no local content used as transport'
assert receipt['changed_count']>0 and receipt['count']>=receipt['changed_count']
for ext,sha in [('md',mdsha),('json',jsonsha)]:
 path=base+stem+'.'+ext;body=git('show',accepted+':'+path);assert body==(root/path).read_bytes() and digest(body)==sha
 assert any(r['path']==path and r['sha256']==sha for r in receipt['files'])
review=json.loads((root/(base+stem+'.json')).read_text());expected=[dict(category='fr063-hot-worker-ready-disposition',expectedCases=2,budgetMinutes=5,platform='windows-latest',attempt='FIRST_NATIVE_WINDOWS_ONLY')]
assert review['reviewedSource']==source and review['decision']=='ACCEPTED_SOURCE_FIRST_WINDOWS_WORKER2' and review['markdownSha256']==mdsha and review['releasedActuals']==expected
subprocess.run(['git','merge-base','--is-ancestor',source,accepted],cwd=root,check=True)
changed=git('diff','--name-only',source,accepted).decode().splitlines();assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in changed)
p=root/'tests/selections/1536-hot-worker-windows.json';assert not p.exists() and not git('ls-tree','-r','--name-only',accepted,'--',str(p.relative_to(root)))
release=dict(schemaVersion=1,decision=review['decision'],reviewedSource=source,acceptedCheckpoint=accepted,sourceReview={ext:dict(path=base+stem+'.'+ext,sha256=sha)for ext,sha in [('md',mdsha),('json',jsonsha)]},selections=[dict(category='fr063-hot-worker-ready-disposition',reason='#1536 T179-Pd.3b HS-WORKER-F01/O02 FIRST native WindowsJob worker2 after independent affected SOURCE and recovered acceptance parent. Two genuine actual process/apply routes; initial Linux2FAIL immutable, prior13PASS retained without replay.',contracts=['Existing ordinary WindowsJob only; real correlated worker apply/generated Ready versus accepted apply/failed Ready publication; HOT remains unavailable in both. Exactly2cases/5m/exclusive/frontendfalse, fresh BOTH/catalog and exact PlanOnly before NoBuild. No retry, synthetic authority, ordinary LinuxRelease activation, fullWindows/visual/F1/S/B/M3/T197 claim.'])])
p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(release,ensure_ascii=False,indent=2)+'\n')
assert git('status','--porcelain').decode().splitlines()==['?? tests/selections/1536-hot-worker-windows.json']
print('FIRST_WINDOWS_RELEASE_FILE_CREATED_ONLY; no commit/push/runtime performed',digest(p.read_bytes()))
