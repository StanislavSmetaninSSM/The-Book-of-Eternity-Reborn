"""One reviewed cold console Load. No bridge/daemon, seed export or canonical copy."""
import hashlib,json,os,pathlib,shutil,stat,subprocess,sys

def digest(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def inventory(root):
    result={}
    for p in pathlib.Path(root).rglob('*'):
        mode=p.lstat()
        if stat.S_ISDIR(mode.st_mode):continue
        if not stat.S_ISREG(mode.st_mode) or mode.st_nlink!=1:raise ValueError('Linked/special input: '+str(p))
        result[p.relative_to(root).as_posix()]={'sha256':digest(p),'mode':stat.S_IMODE(mode.st_mode)}
    return result

def cold_run_succeeded(exit_code,guardian,probe):
    """Process reaping is not successful navigation or logical closure."""
    if type(exit_code) is not int or exit_code!=0 or type(guardian) is not dict or type(probe) is not dict:return False
    if guardian.get('echild') is not True or guardian.get('deadline') is not False:return False
    if any(type(guardian.get(k)) is not int or guardian[k]!=0 for k in ('driverExitCode','emergencySignals','failures')):return False
    if any(probe.get(k) is not True for k in ('Success','EOF','ChildRestoredConfiguredBaseline','OwnerRestoredOriginal','TerminalNormalClose')):return False
    if probe.get('Failure') is not None or probe.get('CleanupFailures')!=[]:return False
    return all(type(probe.get(k)) is int and probe[k]==0 for k in ('ChildExitCode','TerminalExitCode'))

def main():
    input_path=pathlib.Path(sys.argv[1]);input_sha=os.environ['BOE_REVIEWED_COLD_INPUT_SHA256']
    if digest(input_path)!=input_sha:raise ValueError('Input pin mismatch')
    spec=json.loads(input_path.read_bytes());repo=pathlib.Path(spec['Repo']);root=pathlib.Path(spec['Destination'])
    def check():
        if digest(input_path)!=input_sha:raise ValueError('Immutable input changed')
        for name,sha in spec['FullSourcePins'].items():
            if digest(repo/name)!=sha:raise ValueError('Source drift: '+name)
        skip={'.git','bin','obj','TestResults','node_modules','__pycache__'}
        names={str(p.relative_to(repo)) for p in repo.rglob('*') if p.is_file() and not any(v in skip for v in p.relative_to(repo).parts)}
        if names!=set(spec['FullSourcePins']):raise ValueError('Source membership drift')
        for name,sha in spec['UnitOutputs'].items():
            if digest(repo/name)!=sha:raise ValueError('Owning output drift')
        for value in spec['Tools'].values():
            if digest(value['path'])!=value['sha256']:raise ValueError('Tool drift')
        for path,sha in ((spec['Node'],spec['NodeSHA256']),(spec['Guardian']['binaryPath'],spec['Guardian']['binarySHA256']),(spec['Archive'],spec['ArchiveSHA256']),(spec['AcceptedDataOutcome'],spec['AcceptedDataSHA256'])):
            if digest(path)!=sha:raise ValueError('Admitted input drift: '+path)
        for path,rows in ((spec['Ship'],spec['ShipRows']),(spec['TerminalPrefix'],spec['TerminalRows']),(spec['QuarantinedPlay'],spec['QuarantinedRows'])):
            if inventory(path)!=rows:raise ValueError('Input inventory drift: '+path)
    check()
    if root.exists() or root.is_symlink():raise ValueError('Cold destination must be absent; no retry')
    os.umask(0o077);root.mkdir(mode=0o700)
    runtime=root/'runtime';shutil.copytree(spec['Ship'],runtime)
    # Copy immutable producer bytes only; private mode normalization is explicit.
    for p in runtime.rglob('*'):
        if p.is_dir():p.chmod(0o700)
        else:p.chmod(0o700 if p.stat().st_mode&0o100 else 0o600)
    normalized={k:{'sha256':v['sha256'],'mode':0o700 if v['mode']&0o100 else 0o600} for k,v in spec['ShipRows'].items()}
    if inventory(runtime)!=normalized:raise ValueError('Copied runtime mismatch')
    play=root/'play';session=play/'game_session';saves=session/'saves/manual_saves';saves.mkdir(parents=True,mode=0o700)
    archive=saves/spec['ArchiveName']
    with archive.open('xb') as f:f.write(pathlib.Path(spec['Archive']).read_bytes())
    if digest(archive)!=spec['ArchiveSHA256']:raise ValueError('Copied exact ZIP mismatch')
    with (session/'config.json').open('x') as f:json.dump(spec['BootstrapConfig'],f)
    private=root/'env'
    for name in ('home','config','data','state','cache','tmp'):(private/name).mkdir(parents=True,mode=0o700)
    env=os.environ.copy();env.update({'HOME':str(private/'home'),'XDG_CONFIG_HOME':str(private/'config'),'XDG_DATA_HOME':str(private/'data'),'XDG_STATE_HOME':str(private/'state'),'XDG_CACHE_HOME':str(private/'cache'),'TMPDIR':str(private/'tmp'),'TMP':str(private/'tmp'),'TEMP':str(private/'tmp'),'DOTNET_CLI_TELEMETRY_OPTOUT':'1','POWERSHELL_TELEMETRY_OPTOUT':'1','POWERSHELL_UPDATECHECK':'Off','PYTHONDONTWRITEBYTECODE':'1','TERM':'dumb','PATH':str(pathlib.Path(spec['Tools']['dotnet']['path']).parent)+os.pathsep+env.get('PATH','')})
    # Unexpected inherited game diagnostics must not route writes elsewhere.
    if any(k.startswith('BOE_') and k!='BOE_REVIEWED_COLD_INPUT_SHA256' for k in env):raise ValueError('Unexpected inherited BOE environment; no launch')
    argv=[spec['Guardian']['binaryPath'],'--relay-turn',str(root/'guardian.json'),'60000',spec['Tools']['python3']['path'],str(repo/'tests/fixtures/SelectionPrompt/terminal/cold_load_console.py'),spec['Node'],spec['TerminalPrefix'],str(runtime/'BookOfEternityClient.dll'),str(play),str(root/'probe'),spec['ArchiveName'],spec['ArchiveSHA256']]
    (root/'admitted.json').write_text(json.dumps({'InputSHA256':input_sha,'Argv':argv,'PrivateEnvironment':{k:v for k,v in env.items() if k in ('HOME','XDG_CONFIG_HOME','XDG_DATA_HOME','XDG_STATE_HOME','XDG_CACHE_HOME','TMPDIR','PATH','DOTNET_CLI_TELEMETRY_OPTOUT','TERM')}},indent=2)+'\n')
    # Original owned operation only; no timeout retry or second invocation.
    with (root/'driver.stdout').open('xb') as stdout,(root/'driver.stderr').open('xb') as stderr:
        result=subprocess.run(argv,cwd=repo,env=env,stdout=stdout,stderr=stderr)
    (root/'exit.txt').write_text(str(result.returncode)+'\n')
    check()
    if inventory(runtime)!=normalized or digest(archive)!=spec['ArchiveSHA256']:raise ValueError('Closing package/archive drift')
    guardian=json.loads((root/'guardian.json').read_bytes());probe=json.loads((root/'probe/result.json').read_bytes())
    route_ok=cold_run_succeeded(result.returncode,guardian,probe)
    if route_ok:
        proof=json.loads((root/'probe/loaded-proof.json').read_bytes())
        if proof.get('ArchiveSHA256')!=spec['ArchiveSHA256'] or proof.get('ArchiveName')!=spec['ArchiveName']:raise ValueError('Actual loaded archive proof differs')
    (root/'closing.json').write_text(json.dumps({'InputSHA256':input_sha,'OldQuarantinedPlayUnchanged':True,'RuntimeUnchanged':True,'ArchiveSHA256':digest(archive),'GuardianExit':result.returncode,'RouteSucceeded':route_ok})+'\n')
    if not route_ok:raise RuntimeError('Cold operation failed; retained, no retry')

if __name__=='__main__':main()
