"""One reviewed packet, original queue only; never authors, retries or renews authority."""
import argparse,datetime,hashlib,json,os,pathlib,subprocess,sys,importlib.util,time
def sha(raw):return hashlib.sha256(raw).hexdigest()
def member(root,name):
    p=pathlib.PurePosixPath(name)
    if not name or p.is_absolute() or str(p)!=name or '..' in p.parts or '\\' in name:raise ValueError('Unsafe packet path')
    f=root
    for part in p.parts:
        f=f/part
        if f.is_symlink():raise ValueError('Linked packet target')
    return f
def matches(path,expected):
    if path.is_symlink():return False
    try:raw=path.read_bytes()
    except FileNotFoundError:return expected=='missing'
    return sha(raw)==expected

def validate_packet(session,raw,expected_sha):
    if sha(raw)!=expected_sha:raise ValueError('Candidate hash differs from reviewed bytes')
    packet=json.loads(raw);writes=packet['Writes']
    if not isinstance(writes,list) or not writes:raise ValueError('Missing writes')
    names=[w['Path'] for w in writes]
    if len(names)!=len(set(names)):raise ValueError('Duplicate writes')
    for w in writes:
        if not matches(member(session,w['Path']),w['ExpectedSHA256']):raise ValueError('Current baseline differs: '+w['Path'])

def require_storage_submission_ack(phase,request_bytes,identity,packet,expected):
    """Only the new P02 fixture requires the driver's witnessed pre-reply choice."""
    route_path=member(phase,'console-route.json');profile_path=member(phase,'launch-profile.json')
    route_bytes=route_path.read_bytes() if route_path.exists() else b'{}'
    route=json.loads(route_bytes)
    profile=json.loads(profile_path.read_bytes()) if profile_path.exists() else {}
    expected_route='ordinary-save-with-storage-retrieve'
    if expected_route not in (route.get('Route'),profile.get('Route')) and 'StorageDecisionObservedSHA256' not in expected:return
    if (phase.name!='p02' or route.get('Route')!=expected_route or profile.get('Route')!=expected_route
        or profile.get('ConsoleRouteSHA256')!=sha(route_bytes)):
        raise ValueError('Exact prepared P02 storage profile required')
    paths=[member(phase,name) for name in ('gm-storage-decision-before-reply.json',
        'gm-storage-decision-observed.json','gm-storage-decision.json')]
    if any(not path.is_file() for path in paths):raise ValueError('Exact driver pre-reply decision observation required')
    before,ack,after=[path.read_bytes() for path in paths]
    first,observed,final=map(json.loads,(before,ack,after))
    if (set(first)!={'Decision','Rationale','RequestSHA256','CanonicalPins','sessionId','requestId','turnNumber'} or
        first.get('Decision') not in ('Grant','Deny','Defer') or
        first.get('RequestSHA256')!=sha(request_bytes) or
        any(first.get(k)!=identity[k] for k in ('sessionId','requestId','turnNumber')) or
        observed.get('RequestSHA256')!=sha(request_bytes) or observed.get('DecisionSHA256')!=sha(before) or
        type(observed.get('RemainingSeconds')) not in (int,float) or not observed['RemainingSeconds']>=60 or
        expected.get('StorageDecisionObservedSHA256')!=sha(ack)):
        raise ValueError('Storage decision is not the exact observed original')
    exact={k:v for k,v in first.items() if k!='CanonicalPins'}
    exact.update(DecisionRecordSHA256=sha(before),ResponseSHA256=sha(packet))
    if final!=exact:raise ValueError('Reviewed storage decision/response join differs')

def main():
    a=argparse.ArgumentParser();a.add_argument('--root',required=True,type=pathlib.Path);a.add_argument('--phase',choices=['p01','p02','p03','recovered-live'],default='p01');a.add_argument('--request-dir',required=True,type=pathlib.Path);a.add_argument('--candidate',required=True,type=pathlib.Path);a.add_argument('--candidate-sha',required=True);a.add_argument('--expectations',required=True,type=pathlib.Path);a.add_argument('--expectations-sha',required=True);args=a.parse_args()
    ROOT=args.root
    if not ROOT.is_absolute() or ROOT!=ROOT.resolve() or any(p.is_symlink() for p in [ROOT,*ROOT.parents]):raise ValueError('Aliased original root')
    phase=ROOT/args.phase;session=ROOT/'play/game_session';q=args.request_dir
    for p in [q,args.candidate,args.expectations]:
        if not p.is_absolute() or p!=p.resolve():raise ValueError('Aliased input path')
    if q.parent!=phase/'queue':raise ValueError('Not exact original queue')
    raw=args.expectations.read_bytes()
    if sha(raw)!=args.expectations_sha:raise ValueError('Expected original identity record drift')
    expected=json.loads(raw);packet=args.candidate.read_bytes()
    origin_path=member(phase,'launch-origin.json');origin_bytes=origin_path.read_bytes()
    if sha(origin_bytes)!=expected['LaunchOriginSHA256']:raise ValueError('Original launch origin hash mismatch')
    origin=json.loads(origin_bytes)
    if origin['OriginalDriverStartedAtUtc']!=expected['OriginalDriverStartedAtUtc'] or origin['BootId']!=pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip():raise ValueError('Original launch clock/boot mismatch')
    if origin_path.stat().st_mode & 0o222:raise ValueError('Launch origin is not immutable')

    # The caller freezes these actual original identities at request discovery;
    # no main/status discovery here may substitute another owner.
    package=json.loads((phase/'relay-phase-package.json').read_text())
    for name,digest in package['RelayPins'].items():
        if sha(member(phase/'relay',name).read_bytes())!=digest:raise ValueError('Relay source drift')
    spec=importlib.util.spec_from_file_location('_mm21_verified_relay_contract',phase/'relay/relay_contract.py')
    contract=importlib.util.module_from_spec(spec);sys.modules[spec.name]=contract;spec.loader.exec_module(contract)
    current=contract.read_request(q.parent,q)
    if sha(current.header_bytes)!=expected['RequestHeaderSHA256'] or dict(current.turn_identity)!=expected['TurnIdentity']:raise ValueError('Original request mismatch')
    if json.loads(current.header_bytes)['SessionPath']!=str(session):raise ValueError('Wrong session root')
    record=json.loads((ROOT/'play/.boe_runtime/gm-runs/main.json').read_bytes())
    if record['Disposition']!='Running' or record['Identity']!=expected['OriginalOwnerIdentity'] or record['Identity']['RootKey']!=str(ROOT/'play'):raise ValueError('Original owner no longer Running')
    if record['Identity']['BootId']!=pathlib.Path('/proc/sys/kernel/random/boot_id').read_text().strip():raise ValueError('Boot changed')
    if any((phase/n).exists() for n in ['probe-result.json','launch.exit','submission-attempt.json']):raise ValueError('Completed or already attempted')
    if any((q/n).exists() for n in ['response.json','reply.json','started.json','execution.json']):raise ValueError('Already answered/executing')
    validate_packet(session,packet,args.candidate_sha)
    req=json.loads(current.game_request);now=datetime.datetime.now(datetime.timezone.utc);stamp=datetime.datetime.fromisoformat(req['timestamp'].replace('Z','+00:00'));driver_stamp=datetime.datetime.fromisoformat(expected['OriginalDriverStartedAtUtc'].replace('Z','+00:00'));remaining=min(600-(now-stamp).total_seconds(),660-(now-driver_stamp).total_seconds(),660-(time.monotonic()-origin['MonotonicSeconds']))
    if driver_stamp>stamp or stamp>now or not 60<=remaining<=600:raise ValueError('Less than 60s conservative provider reserve')
    require_storage_submission_ack(phase,current.game_request,dict(current.turn_identity),packet,expected)
    # Exclusive attempt marker precedes the ONLY worker invocation. Any uncertainty
    # leaves it in place; a second call refuses, including failed/partial answer.
    marker=phase/'submission-attempt.json'
    with marker.open('x') as f:json.dump({'candidateSHA256':args.candidate_sha,'request':str(q),'at':now.isoformat(),'reserve':remaining},f)
    snapshot=phase/'submission-packet.json'
    with snapshot.open('xb') as f:f.write(packet);f.flush();os.fsync(f.fileno())
    snapshot.chmod(0o400)
    if snapshot.read_bytes()!=packet:raise ValueError('Frozen packet readback mismatch')
    r=subprocess.run(['/usr/bin/python3',str(phase/'relay/relay_worker.py'),'answer',str(q),str(snapshot),'--adapter','dots-finish-relay'],capture_output=True,text=True,timeout=10)
    with (phase/'submission-result.json').open('x') as f:json.dump({'exit':r.returncode,'stdout':r.stdout,'stderr':r.stderr},f)
    print(r.stdout,r.stderr);return r.returncode
if __name__=='__main__':raise SystemExit(main())
