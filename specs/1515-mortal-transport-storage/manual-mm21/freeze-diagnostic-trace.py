"""Offline exact observed trace freezer. Never starts, repairs or admits a world."""
import argparse,hashlib,json,sys
from pathlib import Path
REPO=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(REPO/'tests/fixtures/ProductionMain'))
from relay_phase_evidence import strict_settings, _restored_plain_path, summarize_observed_diagnostic_trace

def freeze(phase, profile_sha, probe_sha, journal_sha, output):
    phase=_restored_plain_path(Path(phase),True)
    def pinned(name,digest,array=False):
        raw=_restored_plain_path(phase/name).read_bytes()
        if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Frozen input hash differs: '+name)
        return strict_settings(b'{"Journal":'+raw+b'}')['Journal'] if array else strict_settings(raw)
    profile=pinned('launch-profile.json',profile_sha)
    if profile.get('PhysicalTrace')!={'Route':'external','Directory':str(phase/'physical-trace')}:
        raise ValueError('Pinned phase external trace path differs')
    probe=pinned('probe-result.json',probe_sha)
    journal=pinned('rpc-journal.json',journal_sha,True)
    exit_raw=_restored_plain_path(phase/'launch.exit').read_text().strip()
    if exit_raw not in ('0','1','2','124','137','143'):raise ValueError('Original launch must be settled')
    clients=[r for r in journal if r.get('Entrypoint')=='client']
    if len(clients)!=1 or type(clients[0].get('OriginalPid')) is not int:raise ValueError('Unique actual client launch PID required')
    owner=dict(probe['OriginalRunningRecord']['Identity'])
    if owner['Backend']!='LinuxSupervisor':raise ValueError('Only actual LinuxSupervisor serialized owner supported')
    owner['Backend']=2 # Exact declared enum representation, no identity normalisation.
    if probe['BasePath']!=owner['RootKey']:raise ValueError('Canonical base differs')
    summary=summarize_observed_diagnostic_trace(phase/'physical-trace',profile['StopNonce'],probe['BasePath'],owner,clients[0]['OriginalPid'])
    summary['FrozenInputs']={'Profile':profile_sha,'Probe':probe_sha,'Journal':journal_sha,'LaunchExit':hashlib.sha256((phase/'launch.exit').read_bytes()).hexdigest()}
    summary['OriginalLaunchExit']=int(exit_raw)
    summary['RouteSuccess']=probe.get('Success') is True
    # Diagnostic settlement is distinct from game acceptance, manual Save and normal cleanup.
    with Path(output).open('x',encoding='utf-8') as target:json.dump(summary,target,indent=2);target.write('\n')
    return summary

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for name in ('phase','profile-sha','probe-sha','journal-sha','output'):parser.add_argument('--'+name,required=True)
    args=parser.parse_args();freeze(args.phase,args.profile_sha,args.probe_sha,args.journal_sha,args.output)
