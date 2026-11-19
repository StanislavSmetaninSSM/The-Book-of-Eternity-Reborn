import pathlib,json,hashlib,uuid,os
D=pathlib.Path('/workspace/shared/mm21-20261119');P=D/'p01';R=pathlib.Path('/workspace/shared/mortal-isolated-20261119');h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();inv=lambda root:{str(p.relative_to(root)):h(p) for p in root.rglob('*') if p.is_file()};before=inv(D/'play');os.umask(0o077)
a='Выхожу из покоев, ищу представителя совета и прошу проводить меня к его хозяйственному двору, показать припаркованную грузовую повозку и закрытую кладовую с разметочным мелом, объяснить владельца и порядок выдачи. Ничего не беру и не перемещаю'
assert not(P/'action.txt').exists();(P/'action.txt').write_bytes(a.encode());(P/'console-route.json').write_text('{"Route":"birth"}\n')
for n in ['config','data','state','cache','tmp','home']:(P/'launch-env'/n).mkdir(parents=True,mode=0o700)
failure=uuid.uuid4().hex;stop=uuid.uuid4().hex
profile={'Route':'birth','ExpectedTurn':42,'PreviousResult':None,'SeedSHA256':h(D/'seed-manifest.json'),'FailureNonce':failure,'StopNonce':stop,'Budgets':{'provider':600,'work':660,'clientCleanup':680,'driver':720,'guardian':750},'Driver':str(R/'tests/fixtures/ProductionMain/live-mortal-phase.py'),'ReceiptHelper':str(D/'receipt-helper/BookOfEternityClient.TestSupport.dll'),'ActionSHA256':h(P/'action.txt'),'RuntimeCount':len(inv(D/'play/game_session/_runtime')),'HelperCount':len(inv(D/'receipt-helper')),'PrivateDirectoriesCreated':'p01/launch-env/*; driver creates own p01/config,data,state,cache,tmp exactly once','OperationalConfigWritten':False}
(P/'launch-profile.json').write_text(json.dumps(profile,indent=2,ensure_ascii=False)+'\n')
script='''#!/bin/bash
set -euo pipefail
umask 077
ROOT=/workspace/shared/mm21-20261119
P="$ROOT/p01"
for marker in launch.log launch-origin.json guardian.json probe-result.json queue; do
 test ! -e "$P/$marker" && test ! -L "$P/$marker" || exit 71
done
exec > "$P/launch.log" 2>&1
export POWERSHELL_TELEMETRY_OPTOUT=1
export POWERSHELL_UPDATECHECK=Off
export DOTNET_CLI_TELEMETRY_OPTOUT=1
source /workspace/shared/isolated-restore-20261119/env.sh
export BOE_TERMINAL_NODE=/opt/codex/runtimes/codex-primary-runtime/dependencies/node/bin/node
export BOE_TERMINAL_PREFIX="$P/terminal-deps"
export PYTHONDONTWRITEBYTECODE=1
export TERM=dumb
export HOME="$P/launch-env/home" XDG_CONFIG_HOME="$P/launch-env/config" XDG_DATA_HOME="$P/launch-env/data" XDG_STATE_HOME="$P/launch-env/state" XDG_CACHE_HOME="$P/launch-env/cache" TMPDIR="$P/launch-env/tmp"
export TMP="$TMPDIR" TEMP="$TMPDIR"
export BOE_TEST_GAME_LOOP_FAILURE_NONCE=FAILURE_NONCE
export BOE_MAIN_STOP_DIAGNOSTIC_NONCE=STOP_NONCE
export BOE_TEST_DIAGNOSTIC_ROOT="$P"
export NPM_CONFIG_USERCONFIG="$P/empty-user.npmrc" NPM_CONFIG_GLOBALCONFIG="$P/empty-global.npmrc" NPM_CONFIG_CACHE="$P/launch-env/cache/npm" NPM_CONFIG_IGNORE_SCRIPTS=true
unset BOE_TEST_LOAD_OBSERVATION_NONCE
/usr/bin/python3 - "$P" <<'ORIGIN'
import datetime,json,os,pathlib,sys,time
p=pathlib.Path(sys.argv[1])/'launch-origin.json'
record={'OriginalDriverStartedAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'MonotonicSeconds':time.monotonic(),'BootId':pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip()}
with p.open('x') as f:json.dump(record,f);f.flush();os.fsync(f.fileno())
p.chmod(0o400)
ORIGIN
date -u --iso-8601=ns
cat /proc/sys/kernel/random/boot_id
printf 'original_shell_pid=%s\\n' "$$"
cat /proc/$$/stat
cd /workspace/shared/mortal-isolated-20261119
set +e
"$ROOT/native/host-guardian" --relay-turn "$P/guardian.json" 750000 /usr/bin/python3 tests/fixtures/ProductionMain/live-mortal-phase.py "$P" "$ROOT/seed-manifest.json" "$P/action.txt" "$ROOT/receipt-helper/BookOfEternityClient.TestSupport.dll"
guardian_exit=$?
set -e
printf '%s\\n' "$guardian_exit" > "$P/guardian-process.exit"
/usr/bin/python3 - "$P" "$guardian_exit" <<'ENDPY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]);g=int(sys.argv[2]);code=g if 1<=g<=255 else 98
try:
 r=json.loads((p/'guardian.json').read_text())
 if g==0 and type(r) is dict:
  driver=r.get('driverExitCode')
  clean=all(type(r.get(k)) is int and r[k]==0 for k in ('driverExitCode','emergencySignals','failures')) and r.get('echild') is True and r.get('deadline') is False
  code=0 if clean else (driver if type(driver) is int and 1<=driver<=255 else 98)
except Exception: code=g if 1<=g<=255 else 98
(p/'launch.exit').write_text(str(code)+'\\n')
raise SystemExit(code)
ENDPY
exit $?
'''.replace('=FAILURE_NONCE','= '+failure).replace('=STOP_NONCE','= '+stop).replace('= ','=')
(D/'launch-p01.sh').write_text(script);(D/'launch-p01.sh').chmod(0o700)
assert inv(D/'play')==before
j={'Kind':'inert-launch-profile','NoExecution':True,'WorldBefore':before,'WorldAfterExact':True,'SourcePins':{str(p.relative_to(R)):h(p) for p in [R/'tests/fixtures/ProductionMain/live-mortal-phase.py',R/'tests/fixtures/ProductionMain/relay_phase_evidence.py',R/'tests/fixtures/ProductionMain/mortal_console_routes.py']},'ProfileSHA256':h(P/'launch-profile.json'),'LaunchSHA256':h(D/'launch-p01.sh'),'ActionSHA256':h(P/'action.txt'),'PhaseInventory':inv(P)}
(D/'launch-preparation.json').write_text(json.dumps(j,indent=2)+'\n');print(json.dumps({k:v for k,v in j.items() if k.endswith('SHA256')},indent=2));print('LaunchPreparationSHA',h(D/'launch-preparation.json'))
