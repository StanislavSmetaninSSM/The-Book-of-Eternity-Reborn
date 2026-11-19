"""Prepare a fresh T016 package only. Never launches a client, relay or GM."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import subprocess
import time
from relay_phase_evidence import (require_native_bootstrap_path_budget,
    relay_inventory, relay_package_preflight, strict_settings)

STAGES=('native','publish-client','publish-bridge','publish-helper','export','deploy','verify')


def digest(path):
    """Hash actual file bytes at the current freshness boundary."""
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def safe_member(root, relative):
    """Resolve one exact relative member without following any linked component."""
    if not isinstance(relative,str):raise ValueError('Unsafe inventory path')
    name=PurePosixPath(relative)
    if not relative or name.is_absolute() or str(name)!=relative or '\\' in relative or any(x in ('.','..') for x in name.parts):
        raise ValueError('Unsafe inventory path')
    root=Path(root).absolute()
    for part in (root,*root.parents):
        if part.is_symlink():raise ValueError('Inventory link')
    path=root
    for part in name.parts:
        path=path/part
        if path.is_symlink():raise ValueError('Inventory link')
    return path


def inventory(root):
    """Return sorted exact file DATA, refusing links, special files and unsafe writes."""
    root=Path(root)
    if root.is_symlink() or not root.is_dir():raise ValueError('Inventory root/link')
    rows=[]
    for path in sorted(root.rglob('*')):
        mode=path.lstat().st_mode
        if stat.S_ISLNK(mode):raise ValueError('Inventory link')
        if mode & 0o022:raise ValueError('Unsafe writable mode')
        if stat.S_ISDIR(mode):continue
        if not stat.S_ISREG(mode):raise ValueError('Inventory special file')
        relative=path.relative_to(root).as_posix();safe_member(root,relative)
        rows.append({'path':relative,'size':path.stat().st_size,'sha256':digest(path),'mode':stat.S_IMODE(mode)})
    return rows


def pins_match(root, pins):
    """Check declared existing build members; no permission follows from these DATA."""
    if not isinstance(pins,dict) or not pins:return False
    for relative,expected in pins.items():
        path=safe_member(root,relative)
        if path.exists() and path.stat().st_mode & 0o022:raise ValueError('Unsafe writable mode')
        if not path.is_file() or not isinstance(expected,str) or len(expected)!=64 or digest(path)!=expected:return False
    return True


def admit_inputs(destination, source_root, output_root, source_pins, output_pins):
    """Refuse occupied destinations and stale explicit build inputs before writes."""
    destination=Path(destination)
    if os.path.lexists(destination):return {'admitted':False,'reason':'destination-exists'}
    safe_member(destination.parent,destination.name)
    if not pins_match(source_root,source_pins) or not pins_match(output_root,output_pins):
        return {'admitted':False,'reason':'input-drift'}
    return {'admitted':True,'reason':None}


def prepare_stages(run_stage):
    """Stop at the first unsuccessful or unsettled stage; mocks cannot issue readiness."""
    for stage in STAGES:
        result=run_stage(stage)
        if (type(result.get('exitCode')) is not int or result['exitCode']!=0 or
            any(result.get(k) is not True for k in ('settled','stdoutEof','stderrEof'))):
            return {'prepared':False,'failedStage':stage}
    return {'prepared':False,'failedStage':None} # Final real-file verifier alone issues the manifest.


def guardian_tool(repo, descriptor):
    """Verify separately bootstrapped guardian bytes/source/compiler provenance.

Bootstrap is an explicit input, not work claimed to have supervised itself.
No PID, generation or authority is imported from an earlier process.
"""
    binary=Path(descriptor['binaryPath']);provenance_path=Path(descriptor['provenancePath'])
    for path in (binary,provenance_path):
        if not path.is_absolute():raise ValueError('Guardian tool path must be absolute')
        safe_member(path.parent,path.name)
        if not path.is_file() or path.stat().st_mode & 0o022:raise ValueError('Unsafe guardian tool')
    if not os.access(binary,os.X_OK) or digest(binary)!=descriptor['binarySHA256']:raise ValueError('Guardian binary pin')
    if digest(provenance_path)!=descriptor['provenanceSHA256']:raise ValueError('Guardian provenance pin')
    provenance=strict_settings(provenance_path.read_bytes())
    source='tests/fixtures/LinuxHost/host-guardian.c'
    if digest(safe_member(repo,source))!=descriptor['sourceSHA256']:raise ValueError('Guardian source pin')
    assets=[a for a in provenance['assets'] if a.get('binary')=='host-guardian']
    if len(assets)!=1 or assets[0].get('source')!=source or assets[0].get('sourceSha256')!=descriptor['sourceSHA256'] or assets[0].get('binarySha256')!=descriptor['binarySHA256']:
        raise ValueError('Guardian provenance asset')
    invocation=Path(provenance['compiler'])
    if not invocation.is_absolute():raise ValueError('Guardian compiler invocation must be absolute')
    compiler=Path(descriptor['compilerPath']).resolve(strict=True)
    if str(compiler)!=str(invocation.resolve(strict=True)) or digest(compiler)!=descriptor['compilerSHA256']:
        raise ValueError('Guardian compiler provenance')
    if not provenance.get('flags') or subprocess.check_output([str(invocation),'--version'],text=True,timeout=10).strip()!=provenance['compilerVersion']:
        raise ValueError('Guardian compiler version/flags')
    return binary


def guardian_report_result(guardian_exit, report):
    """Interpret only the existing exact guardian report; malformed DATA is unknown."""
    if not isinstance(report,dict):return False,False
    counts=('driverExitCode','emergencySignals','failures','reaped')
    well_formed=(set(report)=={'echild','driverExitCode','emergencySignals','reaped','failures','deadline'} and
                 all(type(report.get(k)) is int for k in counts) and
                 type(report.get('deadline')) is bool and report.get('echild') is True and
                 report['reaped']>=1 and report['emergencySignals']>=0 and report['failures']>=0)
    settled=well_formed and report['failures']==0 and type(guardian_exit) is int and guardian_exit==0
    success=settled and report['driverExitCode']==0 and report['emergencySignals']==0 and report['deadline'] is False
    return settled,success


def run_owned(argv, cwd, log_root, stage, guardian, seconds=180):
    """Use the existing child-pidfd guardian, never numeric PID/group signals.

Its longer-budget switch selects only the existing deadline cap, not a game
launch. File sinks plus guardian ECHILD prove writer closure; an outer timeout
or missing/malformed report leaves settlement UNKNOWN and issues no readiness.
"""
    log_root=Path(log_root);start=time.monotonic();report_path=log_root/(stage+'.guardian.json')
    if not 0.05<=seconds<=300 or report_path.exists():raise ValueError('Guardian deadline/report path')
    if not argv or not Path(argv[0]).is_absolute() or not Path(guardian).is_absolute():raise ValueError('Absolute executable required')
    result={'exitCode':-1,'settled':False,'stdoutEof':False,'stderrEof':False,'argv':argv,'guardianReport':str(report_path)}
    env=os.environ.copy();env.update(MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0')
    with (log_root/(stage+'.stdout')).open('xb') as out,(log_root/(stage+'.stderr')).open('xb') as err:
        try:
            proc=subprocess.Popen([str(guardian),'--live-turn',str(report_path),str(int(seconds*1000)),*argv],
                cwd=cwd,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err)
            result['guardianExitCode']=proc.wait(timeout=seconds+15)
            report=strict_settings(report_path.read_bytes());result['report']=report
            settled,success=guardian_report_result(result['guardianExitCode'],report)
            result.update(exitCode=0 if success else -1,settled=settled,stdoutEof=settled,stderrEof=settled)
        except Exception as error:
            result.update(exceptionType=type(error).__name__,exception=str(error),settlement='UNKNOWN')
            # No signal fallback. Retain the report/logs and let the actual guardian
            # continue its own ownership work if its observation deadline expired.
    result['elapsedSeconds']=time.monotonic()-start
    write_diagnostic(log_root/(stage+'.outcome.json'),result)
    return result


def native_join(repo, native, commit):
    """Join actual native producer manifests to source and binary bytes."""
    provenance=strict_settings((native/'build-provenance.json').read_bytes())
    package=strict_settings((native/'package-manifest.json').read_bytes())
    assets=provenance['assets']
    expected={'boe-lineage-supervisor':'native/linux/boe-lineage-supervisor.c',
              'host-guardian':'tests/fixtures/LinuxHost/host-guardian.c'}
    if len(assets)!=2 or {a['binary']:a['source'] for a in assets}!=expected:raise ValueError('Native asset set')
    for row in assets:
        if digest(safe_member(repo,row['source']))!=row['sourceSha256'] or digest(safe_member(native,row['binary']))!=row['binarySha256']:
            raise ValueError('Native hash join')
        if not os.access(native/row['binary'],os.X_OK):raise ValueError('Native executable mode')
    supervisor=next(a for a in assets if a['binary']=='boe-lineage-supervisor')
    if (package['sourceCommit']!=commit or package['sourceSha256']!=supervisor['sourceSha256'] or
        package['binarySha256']!=supervisor['binarySha256'] or package['runtimeIdentifier']!='linux-x64' or
        package['guarantee']!='ordinary-same-namespace-lineage'):raise ValueError('Native package join')
    return inventory(native)


def published_join(repo, published, project, output_pins, native):
    """Bind every published member to its owning build output or fresh native input.

These executable SDK projects have no publish-only managed transform under
--no-build. Only the two explicitly injected native Content members differ.
Unexpected publish-generated files fail closed rather than being self-pinned.
"""
    prefix=project+'/bin/Debug/net8.0/'
    expected={name[len(prefix):]:value for name,value in output_pins.items() if name.startswith(prefix)}
    if not expected:raise ValueError('Missing producer inventory')
    for name in ('boe-lineage-supervisor','package-manifest.json'):
        expected['runtimes/linux-x64/native/'+name]=digest(native/name)
    rows=inventory(published)
    actual={row['path']:row['sha256'] for row in rows}
    if actual!=expected:raise ValueError('Published producer copy mismatch: '+project)
    return rows


def copied_source_join(source, copied):
    """Require copied relay DATA to match the actual tracked-source tree exactly."""
    expected={row['path']:row['sha256'] for row in inventory(source)}
    actual={row['path']:row['sha256'] for row in inventory(copied)}
    if not expected or actual!=expected:raise ValueError('Relay source copy mismatch')


def write_diagnostic(path, payload):
    """Persist one owned stage/terminal outcome outside Git, never readiness."""
    with Path(path).open('x') as stream:json.dump(payload,stream,indent=2)


def private_tree(root):
    """Restrict newly owned output trees, retaining executable file bits only."""
    for path in [Path(root),*Path(root).rglob('*')]:
        if path.is_symlink():raise ValueError('Output link')
        path.chmod(0o700 if path.is_dir() or path.stat().st_mode & 0o111 else 0o600)


def prepare(repo, destination, build_manifest):
    """Coordinate real publication/export once; write final readiness only after joins."""
    repo=Path(repo).absolute();dest=Path(destination).absolute();build_path=Path(build_manifest).absolute()
    build_raw=build_path.read_bytes();build=strict_settings(build_raw)
    if build.get('configuration')!='Debug' or build.get('framework')!='net8.0' or not build.get('toolVersions'):
        raise ValueError('Owning build configuration/tool versions required')
    commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True,timeout=10).strip()
    if commit!=build['sourceCommit']:raise ValueError('Owning build checkout HEAD mismatch')
    published=build.get('publishedCheckpoint','')
    if len(published)!=40 or any(c not in '0123456789abcdef' for c in published):raise ValueError('Published checkpoint required separately')
    dirty=subprocess.check_output(['git','status','--porcelain'],cwd=repo,text=True,timeout=10).splitlines()
    guardian=guardian_tool(repo,build['guardianTool'])
    source=build['sourcePins'];outputs=build['outputPins']
    tracked=subprocess.check_output(['git','ls-files','-z'],cwd=repo,timeout=10).decode().split('\0')
    tracked={name for name in tracked if name and (repo/name).is_file()}
    if set(source)!=tracked:raise ValueError('Owning build must pin the complete tracked source set')
    for project in ('BookOfEternityClient','BookOfEternityGMBridge','BookOfEternityClient.TestSupport'):
        directory=repo/project/'bin/Debug/net8.0'
        if not directory.is_dir():raise ValueError('Missing owning project outputs')
        for row in inventory(directory):
            name=(directory/row['path']).relative_to(repo).as_posix()
            if outputs.get(name)!=row['sha256']:raise ValueError('Incomplete owning output inventory')
    for tool,argv in {'dotnet':['dotnet','--version'],'pwsh':['pwsh','--version'],'cc':['cc','--version']}.items():
        actual=subprocess.check_output(argv,cwd=repo,timeout=10,text=True).strip()
        if build['toolVersions'].get(tool)!=actual:raise ValueError('Tool version drift: '+tool)
    if admit_inputs(dest,repo,repo,source,outputs)['admitted'] is not True:raise ValueError('Preparation input refusal')
    require_native_bootstrap_path_budget(dest/'p01'/'tmp')
    if not dest.parent.is_dir() or dest.parent.stat().st_mode & 0o022:raise ValueError('Unsafe destination parent')
    old_umask=os.umask(0o077);owned=False
    try:
        dest.mkdir(mode=0o700);owned=True;logs=dest/'logs';logs.mkdir();native=dest/'native';package=dest/'package'
        helper=dest/'receipt-helper';phase=dest/'p01';phase.mkdir();seed_path=dest/'seed-manifest.json'
        driver=repo/'tests/fixtures/ProductionMain/live-mortal-phase.py';evidence=driver.with_name('relay_phase_evidence.py')
        projects={'publish-client':('BookOfEternityClient',package/'BookOfEternityClient'),
                  'publish-bridge':('BookOfEternityGMBridge',package/'BookOfEternityGMBridge'),
                  'publish-helper':('BookOfEternityClient.TestSupport',helper)}
        outcomes={};verified={}
        executables={name:shutil.which(name) for name in ('dotnet','pwsh')}
        if not all(executables.values()):raise ValueError('Required executable missing')
        def stage(name):
            if name=='native':argv=['pwsh','-NoProfile','-File',str(repo/'scripts/build-linux-supervisor.ps1'),'-OutputDirectory',str(native),'-IncludeHostGuardian']
            elif name in projects:
                project,target=projects[name]
                argv=['dotnet','publish',str(repo/project/(project+'.csproj')),'-c','Debug','--no-build','--no-restore',
                      '-p:BoeNativePackageDirectory='+str(native),'-p:BoeRequireNativePackage=true','-nodeReuse:false','-p:UseSharedCompilation=false','-o',str(target)]
            elif name=='export':argv=['dotnet',str(helper/'BookOfEternityClient.TestSupport.dll'),'mortal-live-seed-export',str(dest/'play'),str(repo/'FileSystemExample/game_session'),str(seed_path)]
            else:
                session=dest/'play/game_session'
                if name=='deploy':
                    seed=strict_settings(seed_path.read_bytes())
                    if seed.get('acceptedGmTurn') is not False or seed.get('validationIssues')!=0 or seed.get('setupTurn')!=41:raise ValueError('Seed export contract')
                    if (session/'_runtime').exists():raise ValueError('Unexpected pre-deploy runtime')
                    shutil.copytree(package,session/'_runtime');shutil.copytree(repo/'tools/gm-relay',phase/'relay')
                    (phase/'empty-user.npmrc').write_bytes(b'');(phase/'empty-global.npmrc').write_bytes(b'')
                    private_tree(dest)
                    pins={'RuntimePins':relay_inventory(session/'_runtime'),'RelayPins':relay_inventory(phase/'relay'),
                          'ReceiptHelperPins':relay_inventory(helper),'DriverSHA256':digest(driver),
                          'EvidenceModuleSHA256':digest(evidence),'ConsoleRouteModuleSHA256':digest(driver.with_name('mortal_console_routes.py'))}
                    (phase/'relay-phase-package.json').write_text(json.dumps(pins,indent=2)+'\n')
                else:
                    if not pins_match(repo,source) or not pins_match(repo,outputs):raise ValueError('Closing input drift')
                    for project,target in projects.values():published_join(repo,target,project,outputs,native)
                    copied_source_join(repo/'tools/gm-relay',phase/'relay')
                    if inventory(package)!=inventory(session/'_runtime'):raise ValueError('Runtime deployment drift')
                    relay_package_preflight(phase,session/'_runtime',driver)
                    if strict_settings((phase/'relay-phase-package.json').read_bytes())['ReceiptHelperPins']!=relay_inventory(helper):raise ValueError('Helper deployment drift')
                    for folder in ('input','output','ready'):
                        path=session/folder
                        if path.is_file() or (path.is_dir() and any(path.iterdir())):raise ValueError('Unexpected runtime contents')
                    for path in ('queue','game_state/control/pending_turn_snapshot','game_state/control/pending_turn_snapshot.json','game_state/control/pending_turn_snapshot.authority.json','game_state/control/gm_bridge_status.json','game_state/control/validation_repair_request.json','game_state/control/gm_turn_helper.bootstrap.ps1'):
                        if (session/path).exists():raise ValueError('Unexpected live artifact')
                    if (dest/'play/.boe_runtime/gm-runs').exists() or (phase/'queue').exists():raise ValueError('Unexpected owner/queue')
                    verified.update(native=native_join(repo,native,commit),runtime=inventory(package),helper=inventory(helper),relay=inventory(phase/'relay'),seed=inventory(session))
                return {'exitCode':0,'settled':True,'stdoutEof':True,'stderrEof':True}
            argv[0]=executables[argv[0]]
            result=run_owned(argv,repo,logs,name,guardian);outcomes[name]=result
            if name=='native' and result['exitCode']==0:native_join(repo,native,commit)
            return result
        def observed_stage(name):
            try:
                result=stage(name)
                outcomes[name]=result
                if not (logs/(name+'.outcome.json')).exists():write_diagnostic(logs/(name+'.outcome.json'),result)
                return result
            except BaseException as error:
                failure={'stage':name,'exceptionType':type(error).__name__,'exception':str(error),'prepared':False}
                try:
                    path=logs/(name+'.exception.json')
                    write_diagnostic(path,failure)
                except Exception as diagnostic_error:error.add_note('Diagnostic write failed: '+str(diagnostic_error))
                raise
        result=prepare_stages(observed_stage)
        if result['failedStage'] is not None:raise ValueError('Preparation failed: '+result['failedStage'])
        manifest={'schemaVersion':1,'kind':'mortal-live-preparation','prepared':True,'checkoutHead':commit,'publishedCheckpoint':published,'checkoutChanges':dirty,
                  'configuration':'Debug','framework':'net8.0','toolVersions':build['toolVersions'],'guardianBootstrap':build['guardianTool'],
                  'owningBuildManifest':str(build_path),'owningBuildManifestSHA256':hashlib.sha256(build_raw).hexdigest(),
                  'sourcePins':source,'outputPins':outputs,'inventories':verified,'stages':outcomes,
                  'seedManifestSHA256':digest(seed_path),'packageManifestSHA256':digest(phase/'relay-phase-package.json')}
        with (dest/'preparation.json').open('x') as stream:json.dump(manifest,stream,indent=2)
        return manifest
    except BaseException as error:
        if owned:
            try:write_diagnostic(dest/'preparation-failed.json',{'prepared':False,'exceptionType':type(error).__name__,'exception':str(error)})
            except Exception as diagnostic_error:error.add_note('Terminal diagnostic write failed: '+str(diagnostic_error))
        raise
    finally:os.umask(old_umask)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo',required=True);parser.add_argument('--destination',required=True);parser.add_argument('--build-manifest',required=True)
    args=parser.parse_args();prepare(args.repo,args.destination,args.build_manifest)
