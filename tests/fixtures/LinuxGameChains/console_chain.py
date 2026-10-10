"""Real Linux console/Bridge/daemon/relay, authored no-provider turns and cold restart.

The model is replaced by an explicit deterministic packet author. Game input,
main ownership, relay/helper, validation, application and persistence are real.
An outer host-guardian owns this driver and all of its descendant processes.
"""
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pty
import re
import select
import socket
import struct
import subprocess
import sys
import termios
import time
import traceback
import uuid

repo, out, ship = map(lambda s: Path(s).resolve(), sys.argv[1:4])
base = out / "play"
session = base / "game_session"
session.mkdir(parents=True)
queue = out / "queue-1"
queue.mkdir()
started = time.monotonic()
peers, events = [], []
result = {"Scenario": "C1", "Model": "deterministic-authored-fixture",
          "ModelCalls": 0, "AcceptedTurns": [], "ColdRestart": False,
          "ProductionEntrypoint": "BookOfEternityClient/Program.cs → GameEngine.RunAsync"}
env = dict(os.environ)
env.update(TERM="dumb", NO_COLOR="1")
for key, name in [("TMPDIR", "tmp"), ("XDG_CONFIG_HOME", "config"),
                  ("XDG_DATA_HOME", "data"), ("XDG_CACHE_HOME", "cache")]:
    directory = out / name
    directory.mkdir()
    env[key] = str(directory)
pipe = "gc-" + uuid.uuid4().hex[:12]
relay = repo / "tools/gm-relay"
command = "& '/usr/bin/python3' '" + str(relay / "relay_cli.py") + "' --session '" + str(session) + "' --queue '" + str(queue) + "' --model 'deterministic-no-provider'"
config = {"Language": "ru", "MusicEnabled": False, "SoundEnabled": False,
          "GenerateSceneImages": False, "ShowImagesInConsole": False,
          "GmBridgeEnabled": True, "GmBridgeBackend": "OwnedTerminal",
          "GmMainOwnerBackend": "NativeLineage", "GmBridgeAutoStart": False,
          "GmBridgePipeNameOverride": pipe, "GmCliLaunchCommand": command,
          "GmBridgeShellWorkingDirectory": str(session), "GmWorkerBridgeProfiles": [],
          "GmCliInputProfile": {"IdleMarker": "NEUTRAL READY", "PromptPrefix": "RELAY> ",
                                "WorkingMarker": "NEUTRAL WORKING", "BlockedMarkers": ["RELAY ERROR"],
                                "ObservationTimeoutMilliseconds": 15000}}
(session / "config.json").write_text(json.dumps(config, ensure_ascii=False, indent=2))


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(data):
    return hashlib.sha256(data).hexdigest()


class Peer:
    def __init__(self, name, argv, captured=False):
        self.name, self.captured = name, captured
        self.master, self.slave = pty.openpty()
        fcntl.ioctl(self.slave, termios.TIOCSWINSZ, struct.pack("HHHH", 25, 100, 0, 0))
        self.initial = termios.tcgetattr(self.slave)
        self.raw, self.echo, self.closed = bytearray(), bytearray(), set()

        def own():
            os.setsid()
            fcntl.ioctl(self.slave, termios.TIOCSCTTY, 0)
            os.tcsetpgrp(self.slave, os.getpid())

        self.process = subprocess.Popen(argv, cwd=ship, env=env, stdin=self.slave,
                                        stdout=subprocess.PIPE if captured else self.slave,
                                        stderr=subprocess.STDOUT if captured else self.slave, preexec_fn=own)
        self.streams = {self.master: "echo" if captured else "output"}
        if captured:
            self.streams[self.process.stdout.fileno()] = "output"
        peers.append(self)
        events.append({"Peer": name, "Argv": argv, "Pid": self.process.pid})

    def text(self, offset=0):
        return re.sub(r"\x1b\[[0-9;? ]*[A-Za-z~]", "", self.raw[offset:].decode("utf-8", "replace"))

    def send(self, text):
        assert self.process.poll() is None, self.name + " already exited"
        data = text.encode("utf-8")
        assert os.write(self.master, data) == len(data), "Unknown partial input; never retry"
        events.append({"Peer": self.name, "Input": text, "AtSeconds": time.monotonic() - started})


def pump():
    fds = [fd for p in peers for fd in p.streams if fd not in p.closed]
    readable = select.select(fds, [], [], .03)[0] if fds else []
    for p in peers:
        for fd, kind in p.streams.items():
            if fd not in readable:
                continue
            try:
                data = os.read(fd, 65536)
            except OSError as ex:
                if fd != p.master or ex.errno != errno.EIO:
                    raise
                data = b""
            if not data:
                p.closed.add(fd)
            else:
                target = p.echo if kind == "echo" else p.raw
                target.extend(data)
                assert len(target) < 4194304, "Owned output exceeds 4MiB"
    assert time.monotonic() - started < 240, "Driver work/cleanup budget exceeded"


def wait(predicate, label, seconds=20):
    deadline = time.monotonic() + seconds
    while not predicate():
        assert time.monotonic() < deadline, "Timeout: " + label
        pump()
    events.append({"Observed": label, "AtSeconds": time.monotonic() - started})


def fresh(peer, marker, offset=0, seconds=20):
    def observed():
        assert peer.process.poll() is None, peer.name + " exited before " + marker
        return marker in peer.text(offset)
    wait(observed, peer.name + ": " + marker, seconds)


def rpc(frame):
    assert frame["command"] in ["status", "shutdown"], "No readiness/ownership override"
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as sock:
        sock.settimeout(12)
        sock.connect(str(out / "tmp" / ("CoreFxPipe_" + pipe)))
        sock.sendall(json.dumps(frame).encode() + b"\n")
        reply = bytearray()
        while not reply.endswith(b"\n"):
            chunk = sock.recv(8192)
            assert chunk, "RPC outcome unknown; never replay"
            reply.extend(chunk)
            assert len(reply) < 524288
    data = json.loads(reply)
    events.append({"RPC": frame, "Reply": data})
    return data


def worker(*args):
    completed = subprocess.run(["/usr/bin/python3", str(relay / "relay_worker.py"), *map(str, args)],
                               env=env, capture_output=True, timeout=12)
    with (out / "worker.log").open("ab") as log:
        log.write(completed.stdout + completed.stderr)
    assert completed.returncode == 0, completed.stderr.decode("utf-8", "replace")


def author_packet(request_dir, ordinal):
    request = read_json(request_dir / "game-request.json")
    control = request["progressionControl"]
    narrative = "Проверочный след " + str(ordinal) + ": душа осмотрелась и сохранила память о берегe."
    report = {k: request[k] for k in ["sessionId", "requestId", "turnNumber"]}
    report.update(worldCyclesProcessed=0, factionCyclesProcessed=0,
                  newLastWorldSimulationTimeInMinutes=control["lastWorldSimulationTimeInMinutes"],
                  newLastFactionSimulationTimeInMinutes=control["lastFactionSimulationTimeInMinutes"],
                  afterlifeCatchupProcessed=False, afterlifeCatchupSummaryEventsProcessed=0)
    for contour in ["chaosSea", "guardianProject", "residentAgency", "shiningAbode", "shiningFaction", "shiningTrade"]:
        report[contour + "CyclesProcessed"] = control[contour + "CyclesExpectedThisTurn"]
        suffix = "SimulationOrdinal" if contour == "chaosSea" else "CycleOrdinal"
        report["newLast" + contour[0].upper() + contour[1:] + suffix] = control["next" + contour[0].upper() + contour[1:] + suffix]
    writes = {"game_state/control/progression_report.json": {"progressionProcessingReport": report},
              "output/narrative_response.json": {"response": narrative},
              "output/interface_updates.json": {"dialogueOptions": []},
              "output/debug_logs.json": {"gm_thoughts_markdown": "Детерминированная тестовая заготовка; провайдер не вызван."}}
    packet = {"Completion": "turn", "Writes": [], "FilesModified": list(writes)}
    for path, data in writes.items():
        target = session / path
        packet["Writes"].append({"Path": path, "ExpectedSHA256": sha(target.read_bytes()) if target.exists() else "missing", "Data": data})
    answer = out / ("authored-" + str(ordinal) + ".json")
    answer.write_text(json.dumps(packet, ensure_ascii=False, indent=2))
    (out / ("request-" + str(ordinal) + ".json")).write_bytes((request_dir / "game-request.json").read_bytes())
    worker("answer", request_dir, answer)
    return request, narrative


def exit_client(client):
    offset = len(client.raw)
    client.send("/options\r")
    fresh(client, "Игровое меню", offset)
    offset = len(client.raw)
    client.send("4\r")
    fresh(client, "Продолжить", offset)
    client.send("8\r")
    wait(lambda: client.process.poll() is not None, client.name + " normal exit")
    assert client.process.returncode == 0


client = bridge = daemon = None
original = original_record = None
record_path = base / ".boe_runtime/gm-runs/main.json"
try:
    dll = ship / "BookOfEternityClient/BookOfEternityClient.dll"
    client = Peer("client-first", ["dotnet", str(dll), str(base), "--plain-output"], True)
    fresh(client, "Тренировка QTE")
    offset = len(client.raw)
    client.send("\r")
    for marker, answer in [("Введите имя вашей души:", "Облачная Проверка"),
                           ("Опишите форму вашей души:", "Человеческий силуэт синего света."),
                           ("Выберите способ создания Хранителя:", "1"),
                           ("Опишите вашего хранителя:", "Спокойный Хранитель берега Моря Хаоса.")]:
        fresh(client, marker, offset)
        offset = len(client.raw)
        client.send(answer + "\r")
    wait(lambda: (session / "input/turn_request.json").exists(), "ordinary bootstrap pending", 40)
    fresh(client, "Мастер игры размышляет...", offset)
    offset = len(client.raw)
    client.send("\x1b")
    fresh(client, "Переходный ход отменён.", offset, 30)
    fresh(client, "Продолжить", offset)
    assert not (session / "input/turn_request.json").exists()
    launcher = str(ship / "BookOfEternityClient/Launcher/bookofeternity.ps1")
    bridge = Peer("bridge", ["pwsh", "-NoLogo", "-NoProfile", "-File", launcher, "start-bridge", "visible", "-SessionPath", str(session)])
    wait(lambda: record_path.exists() and (out / "tmp" / ("CoreFxPipe_" + pipe)).exists(), "original Bridge owner")
    wait(lambda: read_json(record_path)["Disposition"] in ["Running", "Uncertain"], "owner settled startup")
    record = read_json(record_path)
    assert record["Disposition"] == "Running", record
    original_record = record["Identity"]
    original = {k[0].lower() + k[1:]: v for k, v in original_record.items()}
    original["backend"] = 2
    wait(lambda: rpc({"command": "status"})["status"]["ready"], "derived relay readiness")
    daemon = Peer("daemon", ["pwsh", "-NoLogo", "-NoProfile", "-File", launcher, "start-daemon", "visible", "--timeout", "150", "--log", str(out / "daemon.log"), "-SessionPath", str(session)])
    fresh(daemon, "Waiting for turns...")

    for ordinal in [1, 2]:
        if ordinal == 2:
            assert client.process.poll() == 0, "First client still active"
            client = Peer("client-cold", ["dotnet", str(dll), str(base), "--plain-output"], True)
            fresh(client, "Продолжить")
            result["ColdRestart"] = True
        offset = len(client.raw)
        client.send("\r")
        fresh(client, "Ваш ход", offset)
        action = "Осматриваюсь и запоминаю берег, проверочный ход " + str(ordinal) + "."
        offset = len(client.raw)
        client.send(action + "\r")
        wait(lambda: (session / "input/turn_request.json").exists(), "actual player request " + str(ordinal), 30)
        req = read_json(session / "input/turn_request.json")
        assert req["playerAction"] == action and req["turnNumber"] == ordinal, req
        wait(lambda: len([p for p in queue.glob("request-*") if (p / "game-request.json").exists()]) == ordinal,
             "real daemon/relay delivery " + str(ordinal), 30)
        request_dir = sorted(queue.glob("request-*"), key=lambda p: p.stat().st_mtime)[-1]
        captured, narrative = author_packet(request_dir, ordinal)
        assert captured["requestId"] == req["requestId"]
        fresh(client, narrative, offset, 40)
        fresh(client, "Ваш ход", offset, 40)
        story_path = session / "stories/chaos_sea.jsonl"
        story_bytes = story_path.read_bytes()
        assert narrative in story_bytes.decode("utf-8-sig") and action in story_bytes.decode("utf-8-sig")
        assert not (session / "input/turn_request.json").exists(), "Acceptance still pending"
        entries = [json.loads(line) for line in story_bytes.decode("utf-8-sig").splitlines() if line.strip()]
        assert len(entries) == ordinal, entries
        execution = read_json(request_dir / "execution.json")
        assert execution["Executed"] and execution["ExitCode"] == 0 and execution["ChildExited"] and execution["IoDrained"], execution
        result["AcceptedTurns"].append({"Turn": ordinal, "SessionId": req["sessionId"], "RequestId": req["requestId"],
                                         "PlayerAction": action, "Narrative": narrative, "StorySHA256": sha(story_bytes), "StoryEntries": len(entries), "Execution": execution})
        (out / ("story-after-" + str(ordinal) + ".jsonl")).write_bytes(story_bytes)
        exit_client(client)
    assert result["AcceptedTurns"][0]["SessionId"] == result["AcceptedTurns"][1]["SessionId"]
    assert result["AcceptedTurns"][0]["RequestId"] != result["AcceptedTurns"][1]["RequestId"]
    result["ChainAssertionsPassed"] = True
except Exception:
    result["Failure"] = traceback.format_exc()
finally:
    try:
        if bridge is not None and bridge.process.poll() is None:
            worker("close", queue)
            wait(lambda: (queue / "closed.json").exists(), "relay actual close", 20)
            closed = read_json(queue / "closed.json")
            assert all(closed[k] for k in ["ExecutionDisabled", "ChildExited", "IoDrained"]), closed
            result["RelayClose"] = closed
            if original is not None:
                stop = rpc({"command": "shutdown", "rootKey": original["rootKey"], "expectedMainIdentity": original})
                assert stop["ok"] and stop["status"]["terminalStop"]["cleanupComplete"], stop
                wait(lambda: bridge.process.poll() is not None, "original Bridge stopped")
                assert read_json(record_path)["Disposition"] == "Stopped"
                assert read_json(record_path)["Identity"] == original_record
                result["OriginalStopped"] = True
        if daemon is not None:
            wait(lambda: daemon.process.poll() is not None, "original daemon exit", 20)
        if client is not None and client.process.poll() is None:
            # Failure cleanup targets only this driver's original foreground.
            client.send("\x03")
            wait(lambda: client.process.poll() is not None, "failed client foreground exit", 8)
        for p in peers:
            os.close(p.slave)
            p.slave = -1
        wait(lambda: all(len(p.closed) == len(p.streams) for p in peers), "all owned EOF", 8)
        result["IoDrained"] = True
    except Exception:
        result["CleanupFailure"] = traceback.format_exc()
    result["Peers"] = {}
    for p in peers:
        (out / (p.name + ".raw")).write_bytes(p.raw)
        (out / (p.name + "-echo.raw")).write_bytes(p.echo)
        result["Peers"][p.name] = {"ExitCode": p.process.poll(), "EOF": len(p.closed) == len(p.streams), "Pid": p.process.pid}
        for fd in [p.master, p.slave]:
            if fd >= 0:
                os.close(fd)
    result["ElapsedSeconds"] = time.monotonic() - started
    result["PASS"] = bool(result.get("ChainAssertionsPassed") and result.get("OriginalStopped") and result.get("IoDrained") and not result.get("Failure") and not result.get("CleanupFailure"))
    (out / "events.json").write_text(json.dumps(events, ensure_ascii=False, indent=2))
    (out / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2))
    print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result["PASS"] else 1)
