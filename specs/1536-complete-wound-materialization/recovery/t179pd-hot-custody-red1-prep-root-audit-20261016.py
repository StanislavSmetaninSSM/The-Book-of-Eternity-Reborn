from pathlib import Path
import json,gzip,hashlib,subprocess
R=Path('/workspace/The-Book-of-Eternity-Reborn');D=R/'specs/1536-complete-wound-materialization/recovery'
C='e0ed69e2b96f41437e8465ae41182b111dfc1150';S='e182b6745817bd34ff5247c34f8d469bb71754f1';P='t179pd-hot-custody-red1-prep-'
h=lambda b:hashlib.sha256(b).hexdigest()
chains=[];summaries={}
for phase in ('catalog','plan'):
 idx=json.loads((D/(P+phase+'-20261016-artifact-index.json')).read_text());assert idx['source_sha']==S and idx['native_exit_code']==0
 for a in idx['artifacts']:
  path=D/a['path'];stored=path.read_bytes();raw=gzip.decompress(stored) if path.suffix=='.gz' else stored
  assert h(stored)==a['stored_sha256'] and h(raw)==a['raw_sha256'] and len(raw)==a['raw_bytes']
  assert raw==(R/a['raw_path']).read_bytes();assert stored==subprocess.check_output(['git','show',C+':'+str(path.relative_to(R))],cwd=R)
  chains.append(dict(phase=phase,path=a['path'],raw_sha256=h(raw),stored_sha256=h(stored),carrier=C))
 s=idx['summary'];assert s['ExitCode']==0 and s['Tests']['Executed']==s['Tests']['Passed']==s['Tests']['Failed']==0 and not s['TimedOut'] and s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded'];assert s['Revision']['Head']==S and s['Revision']['ChangedFiles']==0
 summaries[phase]=s
assert len(chains)==12
assert summaries['plan']['Selection']['PlannedDescriptors']==2 and summaries['plan']['Selection']['PlannedCases']==3
plan=json.loads((D/(P+'plan-20261016-plan.json')).read_text());assert len(plan['Descriptors'])==2
methods={m['Name']:m['Cases'] for d in plan['Descriptors'] for m in d['ExpectedMethodCounts']};assert len(methods)==3 and sum(methods.values())==3 and all(v==1 for v in methods.values())
m=json.loads((D/'t179pd-hot-custody-red1-prep-binaries-20261016.json').read_text());assert m['source_sha']==S and len(m['binaries'])==4
for b in m['binaries']:assert h((R/b['path']).read_bytes())==b['sha256']
assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in subprocess.check_output(['git','diff','--name-only',S,C],cwd=R,text=True).splitlines())
result=dict(carrier=C,compiled_source=S,successful_raw_artifact_chains=chains,chain_count=len(chains),exact_methods=methods,planned_descriptors=2,planned_cases=3,actual_executions=0,binaries=m['binaries'],catalog_wall=summaries['catalog']['WallTime'],plan_wall=summaries['plan']['WallTime'],source_to_carrier_only_task_documents=True,scope='preparation verification only; no SOURCE/EVIDENCE or runtime release')
out=Path('/tmp/t179pd-hot-custody-red1-prep-root-audit-20261016.json');assert not out.exists();out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:result[k] for k in ('chain_count','planned_descriptors','planned_cases','actual_executions','catalog_wall','plan_wall')}))
