import json,os,shutil,signal,subprocess,sys
from pathlib import Path
repo=Path(sys.argv[1]); output=Path(sys.argv[2]);output.mkdir(parents=True,exist_ok=True)
failures=[]
for mode in ('correct','wrong-close','eof','drain-timeout','command-timeout'):
 root=output/mode;root.mkdir();binary=root/'bin';binary.mkdir()
 shutil.copyfile(repo/'tests/fixtures/ReadyCancellation/peer.py',binary/'dotnet');(binary/'dotnet').chmod(0o700)
 env=os.environ.copy();env.update(PATH=str(binary)+os.pathsep+env['PATH'],BOE_READY_TEST_MODE=mode,BOE_READY_TEST_LEDGER=str(root/'ledger.jsonl'),BOE_MAIN_STOP_DIAGNOSTIC_NONCE='8'*32)
 with (root/'stdout.raw').open('wb') as out,(root/'stderr.raw').open('wb') as err:
  proc=subprocess.Popen(['pwsh','-NoProfile','-File',str(repo/'tests/fixtures/ReadyCancellation/check.ps1'),'-Repo',str(repo),'-Evidence',str(root),'-Mode',mode],stdin=subprocess.DEVNULL,stdout=out,stderr=err,env=env,start_new_session=True)
  try:code=proc.wait(timeout=26)
  except subprocess.TimeoutExpired:
   os.killpg(proc.pid,signal.SIGKILL);proc.wait(timeout=5);failures.append(mode+': owned PS exceeded bound');continue
 if code:failures.append(mode+': shipped coordination assertions failed');continue
 events=[]
 for line in (root/'stderr.raw').read_text().splitlines():
  if line.startswith('BOE_MAIN_OPERATION_PS '):events.append(json.loads(line.split(' ',1)[1]))
 if mode!='command-timeout':
  completions=[x for x in events if x['stage']=='helper-open-completion-after-dispose']
  if len(completions)!=1:failures.append(mode+': missing retained completion evidence');continue
  if completions[0].get('lost') is not True:failures.append(mode+': cleanup restored lost admission')
  observed=completions[0].get('closeObserved') is True
  if observed != (mode=='correct'):failures.append(mode+': incorrect actual receipt qualification')
  if mode=='correct' and completions[0].get('terminalClose',{}).get('outcome')!=2:failures.append(mode+': cancellation outcome lost')
print(json.dumps({'cases':5,'failures':failures}))
sys.exit(bool(failures))
