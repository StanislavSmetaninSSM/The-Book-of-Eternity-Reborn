"""Offline exact observed trace freezer. Never starts, repairs or admits a world."""
import argparse,hashlib,json,sys,stat,tempfile,datetime
from pathlib import Path
REPO=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(REPO/'tests/fixtures/ProductionMain'))
from relay_phase_evidence import strict_settings, _restored_plain_path, summarize_observed_diagnostic_trace, parse_observed_diagnostic_trace_row

def summarize_sealed_trace(directory, nonce, root, owner, client_pid, probe, journal):
    """Offline-only local shutdown classification; does not alter runtime parser."""
    def require(ok):
        if not ok:raise ValueError('Unqualified local post-shutdown trace')
    def utc(value):
        parsed=datetime.datetime.fromisoformat(value.replace('Z','+00:00'))
        require(parsed.utcoffset()==datetime.timedelta(0));return parsed
    directory=_restored_plain_path(directory,True);require(stat.S_IMODE(directory.stat().st_mode)==0o700)
    sealed={}
    for path in sorted(directory.iterdir()):
        path=_restored_plain_path(path);require(stat.S_IMODE(path.stat().st_mode)==0o600)
        sealed[path.name]=path.read_bytes() # exactly one immutable read per original file
    require(sealed.get('owner-nonce')==nonce.encode('ascii'))
    markers={};physical={}
    for name,raw in sealed.items():
        if name=='owner-nonce':continue
        if raw.startswith(b'BOE_MAIN_OPERATION_MARKER '):
            require(raw.endswith(b'\n') and raw.count(b'\n')==1)
            row=strict_settings(raw.split(b' ',1)[1]);markers[name]=row
        else:
            kind,row=parse_observed_diagnostic_trace_row(name,raw,nonce,root,None,owner)
            require(kind=='physical');physical[name]=row
    local_pids={r['ProcessId'] for r in markers.values() if r.get('Stage') in ('helper-before-ready','helper-ready-written') and r.get('OriginalClose') is None}
    excluded=set();local_proofs=[]
    if local_pids:
        original=probe['OriginalRunningRecord']['Identity'];stopped=probe.get('StoppedRecord',{});evidence=stopped.get('StopEvidence',{})
        require(stopped.get('Disposition')=='Stopped' and stopped.get('Identity')==original and evidence.get('Identity')==original and evidence.get('Kind')=='OwnedScopeEmpty' and evidence.get('ObservedBootId')==original['BootId'])
        requests=[r for r in journal if r.get('RPC',{}).get('command')=='shutdown' and r.get('Attempted') is True]
        require(len(requests)==1 and probe.get('ShutdownAttempts')==1)
        request=requests[0];expected={k[0].lower()+k[1:]:v for k,v in owner.items()}
        require(request['RPC'].get('rootKey')==root and request['RPC'].get('expectedMainIdentity')==expected)
        replies=[r for r in journal if r.get('RequestId')==request['RequestId'] and r.get('RPCOutcome')=='completed']
        require(len(replies)==1 and replies[0]['At']>=request['At']);receipt=probe['ShutdownReceipt']
        require(replies[0]['Response']==receipt and receipt.get('ok') is True)
        status=receipt['status'];stop=status['terminalStop']
        require(status['terminalRunId']==owner['RunId'] and status['terminalUncertain'] is False and status['terminalOwnerRetained'] is False)
        require(stop['identity']['runId']==owner['RunId'] and stop['state']=='stopped-within-scope' and stop['cleanupComplete'] is True and stop['authorityRetained'] is False)
        boundary=utc(status['updatedAtUtc'])
        for pid in sorted(local_pids):
            require(type(pid) is int and pid>0 and pid!=client_pid)
            selected=sorted([(name,row) for name,row in markers.items() if row.get('ProcessId')==pid],key=lambda pair:pair[1]['CaptureMonotonicTimestamp'])
            require([r['Stage'] for _,r in selected]==['canonical-current-generation-lease-acquired','helper-before-ready','helper-ready-written'])
            for name,row in selected:
                require(set(row)=={'Nonce','Occurrence','Root','Stage','ProcessId','ObservedAtUtc','CaptureMonotonicTimestamp','OriginalPin','OriginalClose'})
                require(row['Nonce']==nonce and row['Root']==root and row['OriginalPin'] is None and row['OriginalClose'] is None)
                require(type(row['Occurrence']) is int and row['Occurrence']>0 and type(row['CaptureMonotonicTimestamp']) is int and row['CaptureMonotonicTimestamp']>0)
                require(name==f"{pid}-marker-{row['Occurrence']}-{row['Stage']}.json" and utc(row['ObservedAtUtc'])>boundary)
            require(len({r['Occurrence'] for _,r in selected})==3)
            groups={}
            for row in physical.values():
                if row['ProcessId']==pid:
                    require(row['OriginalPin'] is None and utc(row['CapturedAtUtc'])>boundary)
                    groups.setdefault(row['Occurrence'],[]).append(row)
            leases=[]
            for group in groups.values():
                group.sort(key=lambda row:row['CaptureMonotonicTimestamp'])
                require([r['Stage'] for r in group]==['physical-opened','lease-granted','physical-dispose-completed'])
                leases.append(group)
            leases.sort(key=lambda group:group[0]['CaptureMonotonicTimestamp'])
            require(len(leases)==3 and [g[0]['Purpose'] for g in leases] in (['SessionMutation','SessionMutation','SessionFinalization'],['SessionMutation','PublicationReadQuiescence','SessionFinalization']))
            canonical,before,ready=[r['CaptureMonotonicTimestamp'] for _,r in selected]
            require(leases[0][1]['CaptureMonotonicTimestamp']<=canonical<=leases[0][2]['CaptureMonotonicTimestamp']<=before<ready<=leases[1][0]['CaptureMonotonicTimestamp'])
            require(leases[1][2]['CaptureMonotonicTimestamp']<=leases[2][0]['CaptureMonotonicTimestamp'])
            excluded.update(name for name,_ in selected)
            local_proofs.append({'ObservedPid':pid,'Markers':3,'PhysicalTriples':3,'AfterSuccessfulTerminalStopUtc':status['updatedAtUtc'],'Class':'local-post-shutdown-no-remote-projection','OSLineageProved':False})
    # Byte-exact private DATA view; all physical rows retained. No original file writes/re-reads.
    with tempfile.TemporaryDirectory(prefix='boe-trace-view-') as temporary:
        view=Path(temporary);view.chmod(0o700)
        for name,raw in sealed.items():
            if name in excluded:continue
            with (view/name).open('xb') as target:target.write(raw)
            (view/name).chmod(0o600)
        summary=summarize_observed_diagnostic_trace(view,nonce,root,owner,client_pid)
    original_hashes={name:hashlib.sha256(raw).hexdigest() for name,raw in sealed.items()}
    require(set(summary['Files']).isdisjoint(excluded) and set(summary['Files'])|excluded==set(sealed))
    require(all(original_hashes[name]==digest for name,digest in summary['Files'].items()))
    summary['RemoteOrJoinedHealthyMarkers']=summary['HealthyMarkers'];summary['HealthyMarkers']+=len(excluded)
    summary['LocalPostShutdownHelpers']=len(local_proofs);summary['LocalPostShutdownProofs']=local_proofs
    summary['Files']=original_hashes;summary['ObservedBytes']=sum(len(raw) for name,raw in sealed.items() if name!='owner-nonce')
    require(len(sealed)==summary['PhysicalEvents']+summary['HealthyMarkers']+1)
    return summary

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
    summary=summarize_sealed_trace(phase/'physical-trace',profile['StopNonce'],probe['BasePath'],owner,clients[0]['OriginalPid'],probe,journal)
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
