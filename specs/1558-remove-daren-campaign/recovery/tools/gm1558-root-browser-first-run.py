"""UNRUN: bounded Chromium driver, requires an explicit fresh Root release JSON."""
from pathlib import Path
import base64, datetime, hashlib, json, os, shutil, signal, socket, subprocess, sys, time, traceback, urllib.request, uuid

REPO = Path('/workspace/The-Book-of-Eternity-Reborn-1558')
BRANCH = '1558-remove-daren-campaign-20261017'
gate_path = Path(sys.argv[1])
gate = json.loads(gate_path.read_text())
assert gate['kind'] == 'ROOT_RELEASED_G2_CHROMIUM_SAMPLE'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def git(*args):
    return subprocess.check_output(['git', *args], cwd=REPO, text=True).strip()

assert git('rev-parse', 'HEAD') == gate['actualSource']
assert not git('status', '--porcelain')
assert git('ls-remote', 'origin', 'refs/heads/' + BRANCH).split()[0] == gate['actualSource']
dll = Path(gate['productDll']['path'])
assert dll.is_relative_to(REPO) and sha(dll.read_bytes()) == gate['productDll']['sha256']
frontend = Path(gate['frontendRoot'])
assert frontend == REPO / 'BookOfEternityClient.WebFrontend/dist'
frontend_files = {row['path']: row for row in gate['frontendFiles']}
assert {str(p.relative_to(frontend)) for p in frontend.rglob('*') if p.is_file()} == frontend_files.keys()
for path, row in frontend_files.items():
    data = (frontend / path).read_bytes()
    assert len(data) == row['bytes'] and sha(data) == row['sha256']

output = Path('/tmp/gm1558-root-chromium-FIRST-' + gate['compiledSource'][:8] + '-20261017')
output.mkdir()  # Exclusive FIRST sentinel; no overwriting/retry.
fixture = Path('/tmp/boe-gm1558-root-chromium-' + uuid.uuid4().hex)
profile = Path('/tmp/boe-gm1558-root-chromium-profile-' + uuid.uuid4().hex)
old_profile = fixture / 'client_profile/qte_showcase_rewards.json'

def port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]

def get(origin, path):
    with urllib.request.urlopen(origin + path, timeout=2) as response:
        return response.read()

def ready(origin, path, process):
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        assert process.poll() is None, 'owned process exited before readiness'
        try:
            return get(origin, path)
        except (OSError, TimeoutError):
            time.sleep(.2)
    raise TimeoutError('owned loopback service readiness exceeded25seconds')

def image(path):
    data = path.read_bytes() if path.exists() else None
    return {'exists': data is not None, 'bytes': len(data) if data is not None else None,
            'sha256': sha(data) if data is not None else None,
            'bodyBase64': base64.b64encode(data).decode() if data is not None else None}

def durable():
    return {key: image(path) for key, path in {
        'oldProfile': old_profile,
        'soul': fixture / 'game_session/game_state/meta/soul_state.json',
    }.items()}

record = {'kind': 'ROOT_FIRST_ACTUAL_DESKTOP_CHROMIUM_SAMPLE', 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'gate': {'path': str(gate_path), 'sha256': sha(gate_path.read_bytes())},
          'actualSource': gate['actualSource'], 'compiledSource': gate['compiledSource'],
          'fixture': str(fixture), 'chromiumProfile': str(profile),
          'productDll': gate['productDll'], 'frontendFiles': gate['frontendFiles'],
          'status': 'STARTING', 'processes': []}
(output / 'record.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
processes = []
handles = []
created_roots = []
leader_identity = {}
observed_descendants = {}
ownership_errors = []
failure = None

def process_identity(pid):
    try:
        raw = Path('/proc', str(pid), 'stat').read_text()
        fields = raw[raw.rindex(')') + 2:].split()
        argv = Path('/proc', str(pid), 'cmdline').read_bytes().split(b'\0')
        return {'pid': pid, 'state': fields[0], 'ppid': int(fields[1]), 'pgid': int(fields[2]),
                'sid': int(fields[3]), 'birth': fields[19],
                'argv': [a.decode(errors='replace') for a in argv if a]}
    except FileNotFoundError:
        return None

def owned_census():
    # Only identities matching these newly owned sessions, descendants or unique
    # fixture/profile/output arguments enter the receipt. Foreign argv is never emitted.
    leaders = {process.pid for process, claim in processes}
    identities = []
    for directory in Path('/proc').iterdir():
        if not directory.name.isdigit():
            continue
        try:
            identity = process_identity(int(directory.name))
        except (PermissionError, ProcessLookupError) as error:
            if int(directory.name) in leaders or int(directory.name) in observed_descendants:
                ownership_errors.append({'pid': int(directory.name), 'reason': 'known owner identity unreadable: ' + repr(error)})
            continue
        if identity:
            identities.append(identity)
    related = set(leaders) | set(observed_descendants)
    selected = {}
    while True:
        old_count = len(selected)
        for identity in identities:
            text = '\0'.join(identity['argv'])
            if (identity['pid'] in related or identity['ppid'] in related or
                identity['pgid'] in leaders or identity['sid'] in leaders or
                any(str(root) in text for root in (fixture, profile, output))):
                selected[identity['pid']] = identity
                related.add(identity['pid'])
        if len(selected) == old_count:
            break
    for pid, identity in selected.items():
        prior = observed_descendants.get(pid)
        if prior and prior['birth'] != identity['birth']:
            ownership_errors.append({'pid': pid, 'reason': 'observed PID identity changed; no signal or data removal authorized'})
        observed_descendants.setdefault(pid, identity)
    return [identity for identity in selected.values() if identity['state'] != 'Z']

def public_identity(identity):
    if identity is None:
        return None
    return {**{key: value for key, value in identity.items() if key != 'argv'},
        'argvSha256': sha('\0'.join(identity['argv']).encode()),
        'ownedArgvClaims': [argument for argument in identity['argv']
            if any(str(root) in argument for root in (fixture, profile, output))]}

def safe_census():
    try:
        return owned_census()
    except Exception as error:
        ownership_errors.append({'reason': 'terminal census failed: ' + repr(error)})
        return [{'unknownCensus': repr(error)}]

def launch(name, command, claim):
    stream = (output / (name + '.log')).open('wb')
    handles.append(stream)
    process = subprocess.Popen(command, cwd=REPO, stdout=stream, stderr=subprocess.STDOUT, start_new_session=True)
    processes.append((process, claim))
    leader_identity[process.pid] = None
    try:
        identity = process_identity(process.pid)
        leader_identity[process.pid] = identity
    except Exception as error:
        ownership_errors.append({'pid': process.pid, 'reason': 'launch identity unavailable: ' + repr(error)})
        raise
    record['processes'].append({'name': name, 'pid': process.pid, 'command': command, 'uniqueArgvClaim': claim,
        'launchIdentity': public_identity(identity)})
    return process

def signal_live_leader(process, claim, sig):
    if process.poll() is not None:
        return
    expected = leader_identity.get(process.pid)
    try:
        actual = process_identity(process.pid)
    except Exception as error:
        ownership_errors.append({'pid': process.pid, 'reason': 'leader identity unreadable; not signalled: ' + repr(error)})
        return
    if (expected is None or actual is None or actual['birth'] != expected['birth'] or
        actual['pgid'] != process.pid or actual['sid'] != process.pid or
        not any(claim in argument for argument in actual['argv'])):
        ownership_errors.append({'pid': process.pid, 'reason': 'live leader birth/session/unique argv mismatch; not signalled'})
        return
    # No process-group or terminal-leader signals. Popen still owns this live
    # child and has not reaped it, so its checked PID cannot be reused here.
    try:
        os.kill(process.pid, sig)
    except OSError as error:
        ownership_errors.append({'pid': process.pid, 'reason': 'checked leader signal failed: ' + repr(error)})

try:
    fixture.mkdir(); created_roots.append(fixture)
    profile.mkdir(); created_roots.append(profile)
    old_profile = fixture / 'client_profile/qte_showcase_rewards.json'
    old_profile.parent.mkdir()
    old_profile.write_bytes(bytes([0xef, 0xbb, 0xbf, 0, 0xff, 41]))
    # Explicit source-derived owned browser fixture, not a captured user save and
    # not an invocation/full reproduction of PortableSaveFixture.Seed.
    soul = fixture / 'game_session/game_state/meta/soul_state.json'
    soul.parent.mkdir(parents=True)
    soul.write_text(json.dumps({'soulName': 'Тестовая Душа', 'currentRealm': 'Mortal World', 'currentIncarnation': 1,
        'inkFeathers': {'current': 17, 'total': 29}, 'clientRewardGrants': {'darenQteShowcase': {
            'source': 'daren_qte_showcase', 'tierId': 'perfect_shadow', 'inkFeatherBonus': 6,
            'grantedAtUtc': '2026-06-11T05:00:00Z', 'historicalUnknownField': 'retain-owned-data'},
            'unrelatedGrant': 'unchanged'}}, ensure_ascii=False, separators=(',', ':')) + '\n')
    seed_sources = [REPO / 'BookOfEternityClient.TestSupport/PortableSaveFixture.cs',
        REPO / 'BookOfEternityClient.IntegrationTests/DarenRetirementSaveTests.cs']

    app_port = port()
    debug_port = port()
    while debug_port == app_port:
        debug_port = port()
    app_origin = 'http://127.0.0.1:' + str(app_port)
    debug_origin = 'http://127.0.0.1:' + str(debug_port)
    record['seedProvenance'] = {'kind': 'SOURCE_DERIVED_OWNED_MINIMAL_SOUL_NOT_USER_SAVE_OR_FULL_PORTABLE_SEED',
        'sources': [{'path': str(p.relative_to(REPO)), 'sha256': sha(p.read_bytes())} for p in seed_sources],
        'limits': 'Current soul scalar shape and earned/grant fields only; genuine host bootstrap, no full-world/save-load compatibility claim.'}
    record['beforeStartup'] = durable()
    record.update(appOrigin=app_origin, debugOrigin=debug_origin)
    dotnet = shutil.which('dotnet')
    assert dotnet, 'activated dotnet runtime required'
    app = launch('host', [dotnet, str(dll), str(fixture), '--web', '--web-url', app_origin], str(fixture))
    served_index = ready(app_origin, '/', app)
    assert sha(served_index) == frontend_files['index.html']['sha256'], 'served index differs from fresh React build'
    record['servedFrontend'] = []
    for path, row in frontend_files.items():
        served = get(app_origin, '/' + path)
        assert sha(served) == row['sha256'], 'served fresh frontend mismatch: ' + path
        record['servedFrontend'].append({'path': path, 'bytes': len(served), 'sha256': sha(served)})
    record['beforeInteraction'] = durable()
    assert record['beforeInteraction'] == record['beforeStartup'], 'host bootstrap altered seeded soul/profile'
    chromium = launch('chromium', ['/usr/bin/chromium', '--headless', '--no-sandbox', '--disable-dev-shm-usage',
        '--no-first-run', '--no-default-browser-check', '--user-data-dir=' + str(profile),
        '--remote-debugging-address=127.0.0.1', '--remote-debugging-port=' + debug_origin.rsplit(':', 1)[1], 'about:blank'], str(profile))
    ready(debug_origin, '/json/version', chromium)
    record['liveOwnerCensus'] = [public_identity(i) if 'argv' in i else i for i in safe_census()]
    assert not ownership_errors, 'live owner census could not qualify owned processes'
    script = Path('/tmp/gm1558-root-browser-smoke.mjs')
    client = Path('/tmp/gm1558-root-cdp-client.mjs')
    record['scripts'] = [{'path': str(p), 'sha256': sha(p.read_bytes())} for p in (script, client, Path(__file__))]
    node = launch('cdp', ['node', str(script), debug_origin, app_origin, str(output / 'browser')], str(output / 'browser'))
    record['preWaitCensus'] = [public_identity(i) if 'argv' in i else i for i in safe_census()]
    assert not ownership_errors, 'pre-wait owner census could not qualify owned processes'
    code = node.wait(timeout=130)
    record['cdpExit'] = code
    assert code == 0, 'FIRST Chromium flow failed; inspect preserved browser FIRST-failure and logs'
    browser_path = output / 'browser/result.json'
    browser_result = json.loads(browser_path.read_text())
    assert browser_result['status'] == 'PASS'
    record['browserResult'] = {'path': str(browser_path), 'sha256': sha(browser_path.read_bytes()),
        'status': browser_result['status'], 'snapshotCount': len(browser_result['snapshots']),
        'proofLimit': browser_result['proofLimit']}
    record['afterInteraction'] = durable()
    assert record['beforeInteraction'] == record['afterInteraction'], 'old profile/soul changed in actual browser flow'
    assert sha(dll.read_bytes()) == gate['productDll']['sha256']
    for path, row in frontend_files.items():
        assert sha((frontend / path).read_bytes()) == row['sha256']
    assert git('rev-parse', 'HEAD') == gate['actualSource'] and not git('status', '--porcelain')
    record['status'] = 'PASS'
except BaseException as error:
    failure = error
    record['status'] = 'FAIL'
    record['failure'] = repr(error)
    record['failureStack'] = traceback.format_exc()
    try:
        record['afterFailure'] = durable()
    except OSError as observation_error:
        record['afterFailureObservationError'] = repr(observation_error)
finally:
    record['cleanup'] = []
    record['cleanupBeforeCensus'] = [public_identity(i) if 'argv' in i else i for i in safe_census()]
    for process, claim in reversed(processes):
        try:
            signal_live_leader(process, claim, signal.SIGTERM)
            deadline = time.monotonic() + 8
            while process.poll() is None and time.monotonic() < deadline:
                time.sleep(.1)
            if process.poll() is None:
                signal_live_leader(process, claim, signal.SIGKILL)
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired as error:
                failure = failure or error
                record['status'] = 'FAIL'
            record['cleanup'].append({'pid': process.pid, 'settled': process.poll() is not None,
                'exit': process.returncode})
        except Exception as error:
            ownership_errors.append({'pid': process.pid, 'reason': 'owned cleanup incomplete: ' + repr(error)})
            record['cleanup'].append({'pid': process.pid, 'failure': repr(error), 'ownerNotQualifiedClosed': True})
            failure = failure or error
            record['status'] = 'FAIL'
    deadline = time.monotonic() + 4
    remaining = safe_census()
    while remaining and time.monotonic() < deadline:
        time.sleep(.1)
        remaining = safe_census()
    all_terminal = not remaining and not ownership_errors and all(process.poll() is not None for process, claim in processes)
    record['terminalCensus'] = {'allOwnedTerminal': all_terminal, 'remainingActive': [public_identity(i) if 'argv' in i else i for i in remaining],
        'ownershipErrors': ownership_errors, 'observedIdentities': [public_identity(i) for i in observed_descendants.values()]}
    if not all_terminal:
        record['status'] = 'FAIL'
        failure = failure or RuntimeError('owned/unknown process activity remains; all fixture data retained')
    for handle in handles:
        handle.close()
    for root in created_roots:
        assert root.parent == Path('/tmp') and root.name.startswith('boe-gm1558-root-chromium-')
        if not all_terminal:
            record['cleanup'].append({'root': str(root), 'retained': True, 'reason': 'owner closure not positively established'})
            continue
        try:
            shutil.rmtree(root)
            record['cleanup'].append({'root': str(root), 'physicallyAbsent': not root.exists()})
        except OSError as error:
            record['status'] = 'FAIL'
            record['cleanup'].append({'root': str(root), 'failure': repr(error)})
            failure = failure or error
    record['finishedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    record['files'] = [{'path': str(p.relative_to(output)), 'bytes': p.stat().st_size, 'sha256': sha(p.read_bytes())}
        for p in sorted(output.rglob('*')) if p.is_file() and p.name != 'record.json']
    (output / 'record.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'status': record['status'], 'record': str(output / 'record.json'), 'sha256': sha((output / 'record.json').read_bytes())}))
if failure:
    raise SystemExit(1)
