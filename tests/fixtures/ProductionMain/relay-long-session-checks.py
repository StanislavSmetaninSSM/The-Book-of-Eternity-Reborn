"""#1559 current-source causal fixtures. No provider or accepted gameplay claim.

The host seeds an initialized owned generation. This driver owns only its PTY,
relay and exact packet files; the outer existing guardian qualifies all children.
"""
import errno
import hashlib
import json
import os
from pathlib import Path
import pty
import select
import signal
import subprocess
import sys
import termios
import time

REPO = Path(__file__).resolve().parents[3]
MODE, ROOT, DIAG = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
SESSION, QUEUE = ROOT / 'game_session', ROOT / 'relay-queue'
DIAG.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(REPO / 'tools/gm-relay'))
import relay_contract as contract


def digest(value):
    return hashlib.sha256(value).hexdigest()


def put(relative, value):
    path = SESSION / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(value, ensure_ascii=False).encode('utf8')
    path.write_bytes(data)
    return data


def seed(number, repair=False):
    turn = dict(sessionId='owned-1559-session', requestId='owned-request-' + str(number), turnNumber=number)
    put('input/turn_request.json', turn)
    put('game_state/control/pending_turn_snapshot.json', turn)
    put('game_state/control/pending_turn_snapshot.authority.json', {})
    put('game_state/meta/soul_state.json', dict(currentRealm='Chaos Sea'))
    if repair:
        put('game_state/control/validation_repair_request.json', dict(turn, attempt=number, metadataDiagnosticOnly=False, errors=['controlled transport fixture']))
    else:
        (SESSION / 'game_state/control/validation_repair_request.json').unlink(missing_ok=True)
    for name in (['ready/turn_error.json', 'game_state/control/validation_repair_ready.json'] if repair else ['ready/turn_complete.json', 'ready/turn_error.json', 'game_state/control/validation_repair_ready.json']):
        (SESSION / name).unlink(missing_ok=True)
    return turn


helper = REPO / 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1'
bootstrap = SESSION / 'game_state/control/gm_turn_helper.bootstrap.ps1'
bootstrap.parent.mkdir(parents=True, exist_ok=True)
bootstrap.write_text(". '" + str(helper).replace("'", "''") + "'\nInitialize-BoeGmTurnHelper -GameSessionPath '" + str(SESSION).replace("'", "''") + "'\n")
QUEUE.mkdir()
proof = dict(Case=MODE, RealMaintainedRelay=True, ActualHelper=str(helper), ModelRequests=0,
             Scope='CONTROLLED_PROTOCOL_NOT_CLIENT_ACCEPTANCE', CompletedRequests=[], Cleanup={})


def pure_response_bytes():
    turn = seed(1)
    directory = QUEUE / 'request-1-owned'
    directory.mkdir()
    prompt = 'Короткий текущий запрос Ж🙂'.encode()
    source = 'input/turn_request.json'
    paths = [source, 'game_state/control/pending_turn_snapshot.json', 'game_state/control/pending_turn_snapshot.authority.json']
    request_bytes = (SESSION / source).read_bytes()
    header = dict(QueueId=directory.name, Kind='turn', Model='no-provider', SessionPath=str(SESSION), RequestPath=source,
                  PromptSHA256=digest(prompt), RequestSHA256=digest(request_bytes), TurnRequestSHA256=digest(request_bytes),
                  TurnIdentity=turn, Witnesses={p: digest((SESSION / p).read_bytes()) for p in paths})
    (directory / 'request.json').write_text(json.dumps(header))
    (directory / 'prompt.txt').write_bytes(prompt)
    (directory / 'game-request.json').write_bytes(request_bytes)
    request = contract.read_request(QUEUE, directory)
    assert request.prompt == prompt and request.game_request == request_bytes
    message = 'x' * (1048576 if MODE == 'response-over-old-mib' else 1024)
    packet = json.dumps(dict(Completion='turn', Writes=[dict(Path='output/probe.json', ExpectedSHA256='missing', Data=dict(message=message))], FilesModified=['output/probe.json'])).encode()
    failure = None
    try:
        contract.publish_response(request, packet, adapter_id='controlled-existing-computer-writer')
    except contract.RelayError as error:
        failure = dict(Type=type(error).__name__, Message=str(error))
    proof.update(PrerequisitesComplete=True, PacketBytes=len(packet), PacketSHA256=digest(packet), PublicationFailure=failure,
                 NoCanonicalWrite=not (SESSION / 'output/probe.json').exists())
    (DIAG / 'scenario.json').write_text(json.dumps(proof, indent=2))
    assert failure is None, ('Existing request/witness prerequisites passed; exact valid response must not hit the old 1MiB cap', failure)
    assert (directory / 'response.json').read_bytes() == packet
    assert json.loads((directory / 'reply.json').read_bytes())['ResponseSHA256'] == digest(packet)


def terminal_case():
    master, slave = pty.openpty()
    initial = termios.tcgetattr(slave)
    os.set_blocking(master, False)
    process = None
    capture = bytearray()

    def pump():
        if select.select([master], [], [], .02)[0]:
            try:
                capture.extend(os.read(master, 65536))
            except OSError as error:
                if error.errno != errno.EIO:
                    raise

    def until(predicate, label, seconds=12):
        deadline = time.monotonic() + seconds
        while not predicate():
            pump()
            assert process.poll() is None, (label, 'original relay exited', process.returncode)
            assert time.monotonic() < deadline, (label, 'deadline', bytes(capture[-4096:]))

    def send(data):
        view = memoryview(data)
        deadline = time.monotonic() + 8
        while view:
            assert time.monotonic() < deadline, 'Owned PTY input transfer deadline'
            pump()
            if select.select([], [master], [], .02)[1]:
                try:
                    view = view[os.write(master, view[:4096]):]
                except BlockingIOError:
                    pass

    try:
        seed(1)
        process = subprocess.Popen([sys.executable, '-B', str(REPO / 'tools/gm-relay/relay_cli.py'), '--session', str(SESSION), '--queue', str(QUEUE), '--model', 'no-provider'], stdin=slave, stdout=slave, stderr=slave)
        until(lambda: b'NEUTRAL READY' in capture, 'initial actual presentation')
        count = 12 if MODE == 'fourth-submit' else 1
        turn_number = 0
        for number in range(1, count + 1):
            repair = MODE == 'fourth-submit' and number in (2, 5, 8, 11)
            if not repair:
                turn_number += 1
            turn = seed(turn_number, repair)
            prompt = ('REPAIR MODE for rejected turn #' if repair else 'Current ordinary turn #') + str(number)
            if MODE == 'prompt-over-old-lines':
                prompt += '\n' + '\n'.join('current context ' + str(i) for i in range(121))
            elif MODE == 'prompt-over-old-characters':
                prompt += ' ' + 'Ж' * 65001
            raw = prompt.encode()
            offset = len(capture)
            send(b'\x1b[200~' + raw + b'\x1b[201~')
            until(lambda: b'RELAY> ' in capture[offset:] or (QUEUE / 'failure.json').exists(), 'completed paste or explicit refusal')
            pump()
            if (QUEUE / 'failure.json').exists():
                proof.update(Failure=json.loads((QUEUE / 'failure.json').read_bytes()), ReachedSubmission=number, PromptBytes=len(raw), PromptSHA256=digest(raw))
                assert False, ('Valid UTF8 prompt refused by old shape bound after initial real presentation', proof['Failure'])
            send(b'\r')
            until(lambda: len(list(QUEUE.glob('request-*/game-request.json'))) == number or (QUEUE / 'failure.json').exists(), 'original current request or lifetime refusal')
            if (QUEUE / 'failure.json').exists():
                proof.update(Failure=json.loads((QUEUE / 'failure.json').read_bytes()), ReachedSubmission=number)
                assert False, ('Prior real helper completions reached the next submission; old lifetime cap', proof['Failure'])
            directory = sorted(QUEUE.glob('request-*'), key=lambda p: int(p.name.split('-')[1]))[-1]
            request = contract.read_request(QUEUE, directory)
            assert request.prompt == raw and dict(request.turn_identity) == turn
            writes = []
            if MODE in ('many-writes', 'partial-apply'):
                total = 33 if MODE == 'many-writes' else 2
                writes = [dict(Path='output/relay_probe_' + str(i) + '.json', ExpectedSHA256='missing', Data=dict(message='Controlled helper output ' + str(i))) for i in range(total)]
                if MODE == 'partial-apply':
                    writes.append(dict(Path='input/turn_request.json', ExpectedSHA256=digest((SESSION / 'input/turn_request.json').read_bytes()), Data=turn))
            original_input = (SESSION / 'input/turn_request.json').read_bytes()
            packet = json.dumps(dict(Completion='repair' if repair else 'turn', Writes=writes, FilesModified=[x['Path'] for x in writes if not x['Path'].startswith('input/')])).encode()
            contract.publish_response(request, packet, adapter_id='controlled-current-packet')
            until(lambda: (directory / 'execution.json').exists(), 'actual fixed consumer completion', 20)
            execution = json.loads((directory / 'execution.json').read_bytes())
            assert execution['Executed'] and execution['ChildExited'] and execution['IoDrained'], execution
            if MODE == 'partial-apply':
                assert execution['ExitCode'] != 0, 'Protected third target must be rejected'
                changed = [x['Path'] for x in writes[:2] if (SESSION / x['Path']).exists()]
                proof.update(PrerequisitesComplete=True, ActualRejectedThirdMember='input/turn_request.json', ActualMutationPaths=changed, Execution=execution)
                proof.update(OriginalInputUnchanged=(SESSION / 'input/turn_request.json').read_bytes() == original_input,
                             TerminalAbsent=not (SESSION / 'ready/turn_complete.json').exists())
                assert proof['OriginalInputUnchanged'] and proof['TerminalAbsent'], proof
                assert not changed, ('Rejected whole response must leave every beforeimage unchanged; sequential baseline mutated prior members', changed)
                return
            assert execution['ExitCode'] == 0, execution
            signal_path = SESSION / ('game_state/control/validation_repair_ready.json' if repair else 'ready/turn_complete.json')
            signal_value = json.loads(signal_path.read_text(encoding='utf-8-sig'))
            assert all(signal_value[k] == v for k, v in turn.items())
            proof['CompletedRequests'].append(dict(Ordinal=number, Kind='repair' if repair else 'turn', TurnIdentity=turn, PromptSHA256=digest(raw), ResponseSHA256=digest(packet), ActualHelperTerminal=signal_value, Execution=execution))
            offset = len(capture)
            until(lambda: b'NEUTRAL READY' in capture[offset:] or bytes(capture).rstrip().endswith(b'RELAY>'), 'settled actual ready')
    finally:
        if process is not None:
            contract.request_close(QUEUE)
            deadline = time.monotonic() + 5
            while not (QUEUE / 'closed.json').exists() and time.monotonic() < deadline:
                pump()
            # An expected refusal must still stop/join its owned process; no close ACK is invented.
            process.send_signal(signal.SIGTERM)
            process.wait(timeout=5)
            proof['Cleanup'].update(RelayExit=process.returncode, CloseObserved=(QUEUE / 'closed.json').exists())
        proof['Cleanup']['TerminalRestored'] = termios.tcgetattr(slave) == initial
        (DIAG / 'relay.raw').write_bytes(capture)
        (DIAG / 'scenario.json').write_text(json.dumps(proof, indent=2))
        os.close(master)
        os.close(slave)
        assert proof['Cleanup']['TerminalRestored']


if MODE in ('response-over-old-mib', 'small-response'):
    pure_response_bytes()
else:
    terminal_case()
print(json.dumps(dict(Case=MODE, Passed=True, ModelRequests=0, Scope='PROTOCOL_ONLY')))
