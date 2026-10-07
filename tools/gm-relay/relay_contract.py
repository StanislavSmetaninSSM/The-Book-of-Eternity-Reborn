"""Bounded local worker contract. Queue evidence is never main-run authority."""
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import tempfile
from types import MappingProxyType

LIMIT = 1048576


class RelayError(Exception):
    pass


class RelayPending(RelayError):
    """Original request publication is not complete; no response is ready."""


class RelayMismatch(RelayError):
    """Bytes/identity/capability do not match the retained request."""


class RelayClosed(RelayError):
    """Execution closure was requested; publication cannot authorize execution."""


class RelayAnswered(RelayError):
    """A response winner already exists, including unresolved partial publication."""


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_bounded(path, *, pending=False):
    try:
        with Path(path).open('rb') as stream:
            data = stream.read(LIMIT + 1)
    except FileNotFoundError as ex:
        raise (RelayPending if pending else RelayMismatch)('Required file missing: ' + Path(path).name) from ex
    if len(data) > LIMIT:
        raise RelayMismatch('Packet/input exceeded 1048576 bytes')
    return data


def _object(data):
    try:
        value = json.loads(data)
    except (ValueError, UnicodeError) as ex:
        raise RelayMismatch('Malformed JSON packet') from ex
    if not isinstance(value, dict):
        raise RelayMismatch('Expected JSON object')
    return value


def atomic_write_once(path, data):
    """Same-directory atomic publication; never replace a winner or clean its data."""
    path = Path(path)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=path.name + '.tmp.', dir=path.parent, delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.link(temporary, path)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def _json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8')


def _open(queue):
    if (queue / 'close-request.json').exists() or (queue / 'closed.json').exists():
        raise RelayClosed('Queue closure requested; never replay')


@dataclass(frozen=True)
class RelayRequest:
    queue: Path
    directory: Path
    header_bytes: bytes
    prompt: bytes
    game_request: bytes
    model: str
    turn_identity: object


def read_request(queue: Path, request_dir: Path) -> RelayRequest:
    queue, directory = Path(queue).resolve(), Path(request_dir).resolve()
    if directory.parent != queue or not directory.name.startswith('request-'):
        raise RelayMismatch('Request is not in its original queue')
    _open(queue)
    header_bytes = read_bounded(directory / 'request.json', pending=True)
    header = _object(header_bytes)
    prompt = read_bounded(directory / 'prompt.txt', pending=True)
    game_request = read_bounded(directory / 'game-request.json', pending=True)
    kind = header.get('Kind')
    source = 'input/turn_request.json' if kind == 'turn' else 'game_state/control/validation_repair_request.json'
    expected_paths = {source, 'input/turn_request.json', 'game_state/control/pending_turn_snapshot.json',
                      'game_state/control/pending_turn_snapshot.authority.json'}
    witnesses = header.get('Witnesses')
    model = header.get('Model')
    if (header.get('QueueId') != directory.name or kind not in ('turn', 'repair') or
            header.get('RequestPath') != source or not isinstance(model, str) or not model or
            not isinstance(witnesses, dict) or set(witnesses) != expected_paths or
            header.get('PromptSHA256') != sha(prompt) or header.get('RequestSHA256') != sha(game_request) or
            header.get('RequestSHA256') != witnesses[source] or
            header.get('TurnRequestSHA256') != witnesses['input/turn_request.json']):
        raise RelayMismatch('Original request header/bytes mismatch')
    try:
        prompt.decode('utf8')
        session = Path(header['SessionPath'])
    except (KeyError, TypeError, UnicodeError) as ex:
        raise RelayMismatch('Invalid session/prompt') from ex
    if not session.is_absolute():
        raise RelayMismatch('Original session path must be absolute')
    originals = {}
    for path, witness in witnesses.items():
        originals[path] = read_bounded(session / path)
        if sha(originals[path]) != witness:
            raise RelayMismatch('Original request/pending changed: ' + path)
    turn = _object(originals['input/turn_request.json'])
    pending = _object(originals['game_state/control/pending_turn_snapshot.json'])
    identity = header.get('TurnIdentity')
    if (not isinstance(identity, dict) or set(identity) != {'sessionId', 'requestId', 'turnNumber'} or
            any(turn.get(key) != value or pending.get(key) != value for key, value in identity.items())):
        raise RelayMismatch('Original turn/pending identity mismatch')
    return RelayRequest(queue, directory, header_bytes, prompt, game_request, model, MappingProxyType(identity))


def publish_response(request: RelayRequest, packet: bytes, *, adapter_id: str):
    directory = request.directory
    # Partial publication is already a winner; never complete/retry an unknown response.
    if (directory / 'response.json').exists() or (directory / 'reply.json').exists():
        raise RelayAnswered('Original response already published or unresolved')
    _open(request.queue)
    if not isinstance(packet, bytes) or len(packet) > LIMIT:
        raise RelayMismatch('Response must be bounded exact bytes')
    _object(packet)
    if not isinstance(adapter_id, str) or not adapter_id.strip() or len(adapter_id) > 256:
        raise RelayMismatch('Required bounded adapter identity')
    current = read_request(request.queue, directory)
    if current != request:
        raise RelayMismatch('Retained original request changed')
    header = _object(request.header_bytes)
    reply = {key: header[key] for key in ('QueueId', 'PromptSHA256', 'RequestSHA256', 'TurnRequestSHA256')}
    reply.update(ResponseSHA256=sha(packet), Model=request.model, AgentTask=adapter_id)
    try:
        atomic_write_once(directory / 'response.json', packet)
    except FileExistsError as ex:
        raise RelayAnswered('Another original response won publication') from ex
    # Closure racing publication leaves inert response bytes; it never permits execution/replay.
    _open(request.queue)
    try:
        atomic_write_once(directory / 'reply.json', _json_bytes(reply))
    except FileExistsError as ex:
        raise RelayAnswered('Reply publication already owned') from ex


def request_close(queue: Path):
    queue = Path(queue)
    try:
        atomic_write_once(queue / 'close-request.json', b'{}\n')
    except FileExistsError:
        pass


def read_close(queue: Path):
    path = Path(queue) / 'closed.json'
    if not path.exists():
        return None
    closed = _object(read_bounded(path))
    if any(closed.get(key) is not True for key in ('ExecutionDisabled', 'ChildExited', 'IoDrained')):
        raise RelayMismatch('Execution closure/I-O unconfirmed')
    return closed
