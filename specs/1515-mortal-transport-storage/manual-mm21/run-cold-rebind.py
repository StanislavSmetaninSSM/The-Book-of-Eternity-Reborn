"""Reviewed closed-instance preparation only. No subprocess, game or relay call."""
import hashlib,importlib.util,json,os,pathlib,stat,sys
HERE=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('closed_rebind',HERE/'rebind-cold-settings.py');helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)

def sha(raw):return hashlib.sha256(raw).hexdigest()
def rows(root):
    if root!=root.resolve() or not stat.S_ISDIR(root.lstat().st_mode):raise ValueError('Invalid inventory root')
    result={};dirs={'.':stat.S_IMODE(root.lstat().st_mode)}
    for p in root.rglob('*'):
        s=p.lstat();name=p.relative_to(root).as_posix()
        if stat.S_ISDIR(s.st_mode):dirs[name]=stat.S_IMODE(s.st_mode)
        elif stat.S_ISREG(s.st_mode) and s.st_nlink==1:result[name]={'sha256':sha(p.read_bytes()),'mode':stat.S_IMODE(s.st_mode)}
        else:raise ValueError('Linked or special entry: '+str(p))
    return {'Files':result,'Directories':dirs}

def main():
    input_path=pathlib.Path(sys.argv[1]);raw=input_path.read_bytes()
    expected=os.environ['BOE_REVIEWED_REBIND_INPUT_SHA256']
    if sha(raw)!=expected:raise ValueError('Input hash mismatch')
    data=helper._evidence.strict_settings(raw);root=pathlib.Path(data['Root']);phase=root/'recovered-live';repo=pathlib.Path(data['Repo'])
    play=root/'play';config=play/'game_session/config.json'
    result_path=pathlib.Path(data['ResultPath'])
    if os.path.lexists(result_path) or result_path!=result_path.resolve():raise ValueError('Result must be absent and canonical')
    if pathlib.Path(data['ArchiveName']).name!=data['ArchiveName']:raise ValueError('Archive must be a basename')
    frozen=pathlib.Path(data['ColdOutcome']);outcome_raw=frozen.read_bytes()
    if sha(outcome_raw)!=data['ColdOutcomeSHA256']:raise ValueError('Cold outcome mismatch')
    outcome=json.loads(outcome_raw)
    if outcome['OriginalWrapperExit']!=0 or outcome['ProbeSuccess'] is not True:raise ValueError('Cold not accepted')
    for name,digest in outcome['Artifacts'].items():
        if sha(pathlib.Path(name).read_bytes())!=digest:raise ValueError('Cold evidence drift: '+name)
    def source_check():
        if sha(input_path.read_bytes())!=expected:raise ValueError('Input drift')
        for name,digest in data['SourcePins'].items():
            if sha((repo/name).read_bytes())!=digest:raise ValueError('Source drift: '+name)
        if rows(root/'runtime')!=data['RuntimeRows']:raise ValueError('Runtime drift')
    source_check()
    guardian=json.loads((root/'guardian.json').read_bytes());probe=json.loads((root/'probe/result.json').read_bytes())
    exit_code=int((root/'exit.txt').read_text())
    if not helper._cold.cold_run_succeeded(exit_code,guardian,probe):raise ValueError('Actual cold closure not established')
    helper.require_closed_rebind(root,phase,guardian,probe)
    if rows(play)!=data['PlayRows']:raise ValueError('Retained play inventory drift')
    archive=play/'game_session/saves/manual_saves'/data['ArchiveName']
    if sha(archive.read_bytes())!=data['ArchiveSHA256']:raise ValueError('Archive drift')
    before=config.read_bytes();patch=helper.build_closed_settings_patch(before,data['ConfigSHA256'],root,phase,data['Pipe'])
    if sha(patch)!=data['PatchSHA256'] or patch!=pathlib.Path(data['PatchPath']).read_bytes():raise ValueError('Reviewed settings diff drift')
    names={'.gitignore','README.md','input-profile.json','relay_cli.py','relay_contract.py','relay_platform.py','relay_worker.py','relay-apply-response.ps1'}
    if set(data['RelayPins'])!=names:raise ValueError('Wrong relay membership')
    blobs={}
    for name,digest in data['RelayPins'].items():
        path=repo/'tools/gm-relay'/name;s=path.lstat()
        if not stat.S_ISREG(s.st_mode) or s.st_nlink!=1:raise ValueError('Invalid relay source')
        blobs[name]=path.read_bytes()
        if sha(blobs[name])!=digest:raise ValueError('Relay drift')
    # Exclusive assets first. Failed partial preparation is retained and cannot be reused.
    os.umask(0o077);phase.mkdir(mode=0o700);(phase/'relay').mkdir(mode=0o700);(phase/'queue').mkdir(mode=0o700)
    for name,blob in blobs.items():
        with (phase/'relay'/name).open('xb') as stream:stream.write(blob)
    if rows(play)!=data['PlayRows']:raise ValueError('Play changed before settings write')
    helper.write_closed_settings_patch(config,data['ConfigSHA256'],patch)
    source_check()
    wanted=json.loads(json.dumps(data['PlayRows']));wanted['Files']['game_session/config.json']={'sha256':data['PatchSHA256'],'mode':0o600}
    if rows(play)!=wanted:raise ValueError('Closing DATA drift')
    expected_phase={'Directories':{'.':0o700,'relay':0o700,'queue':0o700},'Files':{'relay/'+name:{'sha256':digest,'mode':0o600} for name,digest in data['RelayPins'].items()}}
    if rows(phase)!=expected_phase:raise ValueError('Closing phase drift')
    if sha(archive.read_bytes())!=data['ArchiveSHA256']:raise ValueError('Closing archive drift')
    result={'Success':True,'InputSHA256':expected,'ColdOutcomeSHA256':data['ColdOutcomeSHA256'],'ConfigBefore':data['ConfigSHA256'],'ConfigAfter':data['PatchSHA256'],'ArchiveSHA256':data['ArchiveSHA256'],'NonConfigPlayUnchanged':True,'Hosted':False,'PhaseRows':expected_phase}
    with pathlib.Path(data['ResultPath']).open('x') as stream:json.dump(result,stream,indent=2);stream.write('\n')

if __name__=='__main__':main()
