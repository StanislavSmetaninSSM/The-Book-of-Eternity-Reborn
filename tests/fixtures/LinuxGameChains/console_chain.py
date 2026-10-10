"""Real Linux console/Bridge/daemon/relay, authored no-provider turns and cold restart.

The model is replaced by an explicit deterministic packet author. Game input,
main ownership, relay/helper, validation, application and persistence are real.
An outer host-guardian owns this driver and all of its descendant processes.
"""
import errno
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pty
import re
import select
import signal
import socket
import struct
import subprocess
import sys
import termios
import time
import traceback
import uuid

repo, out, ship = map(lambda s: Path(s).resolve(), sys.argv[1:4])
scenario = sys.argv[4] if len(sys.argv) > 4 else "console"
assert scenario in ["console", "console-musings", "browser-relay"]
base = out / "play"
session = base / "game_session"
session.mkdir(parents=True)
queue = out / "queue-1"
queue.mkdir()
started = time.monotonic()
peers, events = [], []
result = {"Scenario": "C5" if scenario == "browser-relay" else "C1", "GuardianRoute": "addMusings" if scenario == "console-musings" else "thoughtJournal", "Model": "deterministic-authored-fixture",
          "ModelCalls": 0, "AcceptedTurns": [], "ClientColdRestart": False,
          "ProductionEntrypoint": "BookOfEternityClient/Program.cs → GameEngine.RunAsync"}
env = dict(os.environ)
env.update(TERM="dumb", NO_COLOR="1")
for key, name in [("TMPDIR", "tmp"), ("XDG_CONFIG_HOME", "config"),
                  ("XDG_DATA_HOME", "data"), ("XDG_CACHE_HOME", "cache")]:
    directory = out / name
    directory.mkdir()
    env[key] = str(directory)
pipe = "gc-" + uuid.uuid4().hex[:12]
failure_nonce = uuid.uuid4().hex
relay = repo / "tools/gm-relay"
env["BOE_TEST_GAME_CHAIN_QUEUE"] = str(queue)
command = "& '/usr/bin/python3' '" + str(relay / "relay_cli.py") + "' --session '" + str(session) + "' --queue $env:BOE_TEST_GAME_CHAIN_QUEUE --model 'deterministic-no-provider'"
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

        peer_env = dict(env)
        if scenario == "browser-relay" and name.startswith("client-"):
            peer_env["BOE_TEST_GAME_LOOP_FAILURE_NONCE"] = failure_nonce
        self.process = subprocess.Popen(argv, cwd=ship, env=peer_env, stdin=self.slave,
                                        stdout=subprocess.PIPE if captured else self.slave,
                                        stderr=subprocess.STDOUT if captured else self.slave, preexec_fn=own)
        self.streams = {self.master: "echo" if captured else "output"}
        if captured:
            self.streams[self.process.stdout.fileno()] = "output"
        peers.append(self)
        events.append({"Peer": name, "Argv": argv, "Pid": self.process.pid})
        os.close(self.slave)
        self.slave = -1

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
                offset = len(target)
                target.extend(data)
                assert len(target) < 4194304, "Owned output exceeds 4MiB"
                if kind == "output" and scenario == "browser-relay" and p.name.startswith("client-"):
                    events.append({"OutputPeer": p.name, "RawStart": offset, "RawEnd": len(target),
                                   "AtSeconds": time.monotonic() - started})
                    failure = original_game_loop_failure(p)
                    if failure is not None and "OriginalGameLoopFailure" not in result:
                        result["OriginalGameLoopFailure"] = failure
                        result["OriginalGameLoopFailureObservedAtSeconds"] = time.monotonic() - started
                        result["OriginalGameLoopFailureObservedDuringCleanup"] = "CleanupStartedAtSeconds" in result
    assert time.monotonic() - started < 240, "Driver work/cleanup budget exceeded"


def wait(predicate, label, seconds=20):
    deadline = time.monotonic() + seconds
    while not predicate():
        assert time.monotonic() < deadline, "Timeout: " + label
        pump()
    events.append({"Observed": label, "AtSeconds": time.monotonic() - started})


def original_game_loop_failure(peer):
    complete_lines = [line for line in peer.text().splitlines(keepends=True) if line.endswith(("\r", "\n"))]
    diagnostic = next((line.strip() for line in complete_lines
                       if line.strip().startswith('{"kind":"boe-game-loop-fixture-failure",')), None)
    if diagnostic is None:
        return None
    failure = json.loads(diagnostic)
    assert failure["nonce"] == failure_nonce and failure["pid"] == peer.process.pid
    return failure


def fresh(peer, marker, offset=0, seconds=20):
    def observed():
        assert peer.process.poll() is None, peer.name + " exited before " + marker
        failure = original_game_loop_failure(peer)
        if failure is not None:
            raise AssertionError("Original GameLoop exception: " + failure["exception"])
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
    narrative = "Проверочный след " + str(ordinal) + ": Хранитель наблюдает за душой у берега."
    report = {k: request[k] for k in ["sessionId", "requestId", "turnNumber"]}
    report.update(worldCyclesProcessed=0, factionCyclesProcessed=0,
                  newLastWorldSimulationTimeInMinutes=control["lastWorldSimulationTimeInMinutes"],
                  newLastFactionSimulationTimeInMinutes=control["lastFactionSimulationTimeInMinutes"],
                  afterlifeCatchupProcessed=False, afterlifeCatchupSummaryEventsProcessed=0)
    for contour in ["chaosSea", "guardianProject", "residentAgency", "shiningAbode", "shiningFaction", "shiningTrade"]:
        report[contour + "CyclesProcessed"] = control[contour + "CyclesExpectedThisTurn"]
        suffix = "SimulationOrdinal" if contour == "chaosSea" else "CycleOrdinal"
        next_key = "nextChaosSeaTurnOrdinal" if contour == "chaosSea" else "next" + contour[0].upper() + contour[1:] + suffix
        report["newLast" + contour[0].upper() + contour[1:] + suffix] = control[next_key]
    timestamp = datetime.now(timezone.utc).isoformat()
    guardians = read_json(session / "game_state/meta/guardians.json")
    guardian = guardians["guardians"][0]
    actor = guardian["canonicalName"]
    thought = "Я запомню самостоятельный выбор души осмотреть берег, проверочный след " + str(ordinal) + "."
    # The documented separate thought-journal route avoids direct Guardian mutations.
    journal_path = session / "game_state/meta/guardian_thought_journal.json"
    journal = read_json(journal_path) if journal_path.exists() else {"entries": []}
    journal["guardianThoughtJournalUpdates"] = [{"entryId": "gc_thought_" + str(ordinal),
        "guardianId": guardian["guardianId"], "turn": ordinal, "timestamp": timestamp,
        "title": "Наблюдение у берега", "summary": thought, "eventType": "soul_assessment",
        "consequence": "Душа сохраняет самостоятельность.", "attitude": "intrigued",
        "intent": "Остаться рядом без вмешательства."}]
    if scenario == "console-musings":
        guardians["UpdateGuardians"] = [{"command": "addMusings", "guardianId": guardian["guardianId"],
            "musings": [{"turn": ordinal, "topic": "soul_assessment", "mood": "intrigued", "text": thought}]}]
    thoughts = "\n".join(["## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + actor,
                           "- Why relevant: Хранитель наблюдает за выбором души и сохраняет свою реакцию.",
                           "- Actors outside scope: нет", "- Why outside scope: Самостоятельные акторы не участвуют.",
                           "", "## Reasoning", "### " + actor,
                           "- Current location: Море Хаоса; перемещения нет.",
                           "- Situation: Душа осматривается у берега, Хранитель наблюдает.",
                           "- Profile inputs: Существующий свободный Хранитель сопровождает душу; искусства не применяются.",
                           "- Motivation: Дать душе пространство для самостоятельного решения.",
                           "- Constraints: Без новых сил, ресурсов, предметов или ран.",
                           "- Thoughts: " + thought,
                           "- Strategy options:",
                           "1. Наблюдать. Benefit: сохранить самостоятельность. Risk: душа не попросит помощи.",
                           "2. Вмешаться. Benefit: дать совет. Risk: навязать направление.",
                           "- Chosen strategy: Наблюдать.",
                           "- Rejected alternatives: Душа не просила совета.",
                           "- Actions: Хранитель наблюдает и запоминает выбор души.",
                           "- State changes: guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json: одна новая first-person запись, предыдущие entries сохраняются.",
                           "- Детерминированная тестовая заготовка; провайдер не вызван."])
    writes = {"game_state/meta/guardian_thought_journal.json": journal,
              "game_state/control/progression_report.json": {"progressionProcessingReport": report},
              "output/narrative_response.json": {"response": narrative, "timestamp": timestamp},
              "output/interface_updates.json": {"dialogueOptions": [], "timestamp": timestamp},
              "output/debug_logs.json": {"gm_thoughts_markdown": thoughts, "timestamp": timestamp}}
    if scenario == "console-musings":
        del writes["game_state/meta/guardian_thought_journal.json"]
        writes["game_state/meta/guardians.json"] = guardians
        writes["output/debug_logs.json"]["gm_thoughts_markdown"] = thoughts.replace(
            "guardianThoughtJournalUpdates в game_state/meta/guardian_thought_journal.json: одна новая first-person запись, предыдущие entries сохраняются.",
            "UpdateGuardians.addMusings в game_state/meta/guardians.json: одна новая first-person запись, прежние musings и activeGuardian mirror сохраняются.")
    packet = {"Completion": "turn", "Writes": [], "FilesModified": list(writes)}
    for path, data in writes.items():
        target = session / path
        packet["Writes"].append({"Path": path, "ExpectedSHA256": sha(target.read_bytes()) if target.exists() else "missing", "Data": data})
    answer = out / ("authored-" + str(ordinal) + ".json")
    answer.write_text(json.dumps(packet, ensure_ascii=False, indent=2))
    (out / ("request-" + str(ordinal) + ".json")).write_bytes((request_dir / "game-request.json").read_bytes())
    worker("answer", request_dir, answer, "--adapter", "deterministic-authored-game-chain-v1")
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


def submit_browser_action(dll, action, ordinal):
    """Actual React action with original engine waiting, Bridge/daemon/relay ready.

    Requires real turn delivery after the browser write, not merely HTTP success.
    Missing production handoff must remain FAIL; no console-input substitute.
    """
    global client
    import urllib.request
    from playwright.sync_api import sync_playwright
    with socket.socket() as available:
        available.bind(("127.0.0.1", 0))
        url = "http://127.0.0.1:" + str(available.getsockname()[1])
    name = "web-action-" + str(ordinal)
    web = Peer(name, ["dotnet", str(dll), str(base), "--web", "--web-url", url], True)
    def ready():
        assert web.process.poll() is None, "Actual web host exited before browser action"
        try:
            with urllib.request.urlopen(url, timeout=1) as response:
                return response.status == 200
        except (OSError, urllib.error.URLError):
            return False
    wait(ready, "actual web host startup")
    if ordinal == 1:
        # Cooperatively hold only this original input consumer outside ownership.
        # SIGSTOP can freeze unrelated in-flight work needed by the real sidecar.
        wait(lambda: queued_cut_path.exists(), "original queued input idle ACK")
        queued_ack = read_json(queued_cut_path)
        assert queued_ack == {"schemaVersion": 1, "nonce": queued_cut_nonce,
                              "pid": client.process.pid, "outsideParticipation": True}
        result["QueuedIdleWitness"] = queued_ack
    with sync_playwright() as driver:
        browser = driver.chromium.launch(executable_path="/usr/bin/chromium", headless=True,
                                         args=["--no-sandbox", "--disable-dev-shm-usage"], env=env)
        page = browser.new_page(reduced_motion="reduce")
        page.set_default_timeout(12000)
        def inventory():
            cdp = browser.new_browser_cdp_session()
            try:
                rows = cdp.send("SystemInfo.getProcessInfo")["processInfo"]
                assert any(row["type"] == "browser" for row in rows)
                return [int(row["id"]) for row in rows]
            finally:
                cdp.detach()
        pids = inventory()
        owned = {"Name": name, "WebPid": web.process.pid, "RealChromiumPids": pids, "Closed": False}
        result.setdefault("Browsers", []).append(owned)
        try:
            page.goto(url, wait_until="domcontentloaded")
            # The real click changes this SPA route. Assert the actual composer
            # rather than waiting for unrelated scheduled navigation completion.
            page.locator('button[data-launcher-mode="continue"]').click(no_wait_after=True)
            page.get_by_label("Команда или действие", exact=True).wait_for(state="visible")
            page.get_by_label("Команда или действие", exact=True).fill(action)
            with page.expect_response(lambda r: r.request.method == "POST" and r.url == url + "/api/explorer/player-action") as response:
                page.get_by_role("button", name="Отправить", exact=True).click()
            reply = response.value
            body = reply.json()
            submission = {"Name": name, "Status": reply.status, "Request": reply.request.post_data_json,
                          "Response": body}
            result.setdefault("BrowserSubmissions", []).append(submission)
            assert reply.status == 200 and body["success"], body
            pending_path = session / "input/pending_player_action.json"
            pending = read_json(pending_path)
            assert pending["playerAction"] == action and pending["source"] == "browser-composer", pending
            pending_artifact = out / (name + "-pending-action.json")
            pending_artifact.write_bytes(pending_path.read_bytes())
            submission["AuthoritativePending"] = pending
            if ordinal == 2:
                request_path = Path(str(idle_cut_path) + ".request")
                temporary_request_path = Path(str(request_path) + "." + idle_cut_nonce + ".tmp")
                temporary_request_path.write_text(json.dumps({
                    "schemaVersion": 1, "nonce": idle_cut_nonce, "pid": client.process.pid,
                    "actionId": pending["actionId"], "requestId": pending["actionId"],
                    "generation": pending["sessionGeneration"]}))
                os.replace(temporary_request_path, request_path)
            submission["OriginalDerivedReady"] = rpc({"command": "status"})["status"]["ready"]
            if ordinal == 1:
                assert pending["status"] == "queued", pending
                assert not (session / "input/turn_request.json").exists()
                previous = client
                os.kill(previous.process.pid, signal.SIGKILL)
                wait(lambda: previous.process.poll() is not None, "queued original input owner crash")
                wait(lambda: len(previous.closed) == len(previous.streams), "queued input owner EOF")
                assert previous.process.returncode == -signal.SIGKILL
                assert pending_path.read_bytes() == pending_artifact.read_bytes()
                client = Peer("client-queued-cold", ["dotnet", str(dll), str(base), "--plain-output"], True)
                fresh(client, "Продолжить")
                assert pending_path.read_bytes() == pending_artifact.read_bytes(), "queued cold startup rewrote original action"
                client.send("\r")
                result.setdefault("InterruptedColdCuts", []).append({"Phase": "queued", "OriginalPid": previous.process.pid,
                    "ColdPid": client.process.pid, "OriginalExitCode": previous.process.returncode,
                    "OriginalEOF": True, "ActionId": pending["actionId"], "Generation": pending["sessionGeneration"],
                    "OriginalPendingSHA256": sha(pending_artifact.read_bytes()), "NoRequestBeforeCrash": True})
            page.screenshot(path=str(out / (name + "-submitted.png")))
            (out / (name + "-submitted.html")).write_text(page.content())
            try:
                wait(lambda: (session / "input/turn_request.json").exists(), "browser action becomes actual engine turn request", 20)
            except Exception:
                submission["BrowserHandoff"] = {"TurnRequestCreated": False,
                    "PendingExactBytesPreserved": pending_path.read_bytes() == pending_artifact.read_bytes(),
                    "RelayRequestCount": len(list(queue.glob("request-*"))),
                    "ConsoleStillAtPlayerInput": "Ваш ход" in client.text(),
                    "EngineAlive": client.process.poll() is None,
                    "BridgeAlive": bridge.process.poll() is None,
                    "DaemonAlive": daemon.process.poll() is None,
                    "AcceptedContinuationColdRestart": "UNRUN: browser action never reached actual turn request"}
                raise
        except Exception:
            try:
                page.screenshot(path=str(out / (name + "-failure.png")))
                (out / (name + "-failure.html")).write_text(page.content())
            except Exception:
                owned["FailureCapture"] = traceback.format_exc()
            raise
        finally:
            pids = sorted(set(pids + inventory()))
            browser.close()
            def all_exited():
                for pid in pids:
                    path = Path("/proc") / str(pid) / "stat"
                    if path.exists() and path.read_text().split(")", 1)[1].split()[0] != "Z":
                        return False
                return True
            wait(all_exited, "actual C5 browser process exit")
            owned.update(RealChromiumPids=pids, Closed=True)
            assert web.process.poll() is None, "Actual web host unexpectedly exited"
            web.send("\x03")
            wait(lambda: web.process.poll() is not None, name + " normal foreground exit")
            assert web.process.returncode == 0
            wait(lambda: len(web.closed) == len(web.streams), name + " EOF barrier")
            owned.update(WebExitCode=web.process.returncode, WebEOF=True)


client = bridge = daemon = None
original = original_record = None
record_path = base / ".boe_runtime/gm-runs/main.json"


def stop_chain():
    completed = result.setdefault("OriginalStops", [])
    prior_stop = next((s for s in completed if s["Identity"] == original_record), None)
    if bridge is not None and original_record is not None and prior_stop is None:
        assert bridge.process.poll() is None, "Original Bridge exited without its own terminal stop proof"
        worker("close", queue)
        wait(lambda: (queue / "closed.json").exists(), "relay actual close", 20)
        closed = read_json(queue / "closed.json")
        assert all(closed[k] for k in ["ExecutionDisabled", "ChildExited", "IoDrained"]), closed
        result.setdefault("RelayCloses", []).append(closed)
        if original is not None:
            events.append({"OriginalShutdownRequested": original_record, "AtSeconds": time.monotonic() - started})
            stop = rpc({"command": "shutdown", "rootKey": original["rootKey"], "expectedMainIdentity": original})
            assert stop["ok"] and stop["status"]["terminalStop"]["cleanupComplete"], stop
            wait(lambda: bridge.process.poll() is not None, "original Bridge stopped")
            assert read_json(record_path)["Disposition"] == "Stopped"
            assert read_json(record_path)["Identity"] == original_record
            completed.append({"Identity": original_record, "BridgePid": bridge.process.pid,
                              "RelayClose": closed, "ShutdownReply": stop,
                              "RecordAfter": read_json(record_path)})
    elif prior_stop is not None:
        assert bridge.process.poll() == 0, "Previously stopped original Bridge is still alive or failed"
    if daemon is not None and daemon.process.poll() is None:
        daemon.send("\x03")
        wait(lambda: daemon.process.poll() is not None, "original daemon foreground exit", 12)


def start_chain(suffix):
    global bridge, daemon, original, original_record
    previous = original_record
    bridge = Peer("bridge-" + suffix, ["pwsh", "-NoLogo", "-NoProfile", "-File", launcher, "start-bridge", "visible", "-SessionPath", str(session)])
    wait(lambda: record_path.exists() and (out / "tmp" / ("CoreFxPipe_" + pipe)).exists(), "original Bridge owner")
    wait(lambda: read_json(record_path)["Disposition"] == "Running" and
         (previous is None or read_json(record_path)["Identity"]["RunId"] != previous["RunId"]), "new original Running owner")
    record = read_json(record_path)
    original_record = record["Identity"]
    if previous is not None:
        assert original_record["GenerationId"] == previous["GenerationId"]
        assert original_record["Epoch"] == previous["Epoch"] + 1
    original = {k[0].lower() + k[1:]: v for k, v in original_record.items()}
    original["backend"] = 2
    wait(lambda: rpc({"command": "status"})["status"]["ready"], "derived relay readiness")
    daemon = Peer("daemon-" + suffix, ["pwsh", "-NoLogo", "-NoProfile", "-File", launcher, "start-daemon", "visible", "--timeout", "150", "--log", str(out / ("daemon-" + suffix + ".log")), "-SessionPath", str(session)])
    fresh(daemon, "Waiting for turns...")
    result.setdefault("OriginalRuns", []).append(original_record)

try:
    dll = ship / "BookOfEternityClient/BookOfEternityClient.dll"
    if scenario == "browser-relay":
        queued_cut_nonce = uuid.uuid4().hex
        queued_cut_path = out / "browser-queued-idle-ack.json"
        env["BOE_TEST_BROWSER_QUEUED_CUT_PATH"] = str(queued_cut_path)
        env["BOE_TEST_BROWSER_QUEUED_CUT_NONCE"] = queued_cut_nonce
    client = Peer("client-first", ["dotnet", str(dll), str(base), "--plain-output"], True)
    env.pop("BOE_TEST_BROWSER_QUEUED_CUT_PATH", None)
    env.pop("BOE_TEST_BROWSER_QUEUED_CUT_NONCE", None)
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
    start_chain("first")
    preserved_story = b""
    preserved_musings = []

    for ordinal in [1, 2, 3]:
        if ordinal > 1:
            assert client.process.poll() == 0, "First client still active"
            if ordinal == 3:
                old_pids = {p.name: p.process.pid for p in [client, bridge, daemon]}
                stop_chain()
                queue = out / "queue-2"
                queue.mkdir()
                env["BOE_TEST_GAME_CHAIN_QUEUE"] = str(queue)
                start_chain("whole-cold")
                result["WholeChainColdRestart"] = {"PreviousPids": old_pids, "BridgePid": bridge.process.pid, "DaemonPid": daemon.process.pid}
            if scenario == "browser-relay" and ordinal == 2:
                idle_cut_nonce = uuid.uuid4().hex
                idle_cut_path = out / "browser-original-idle-ack.json"
                env["BOE_TEST_BROWSER_IDLE_CUT_PATH"] = str(idle_cut_path)
                env["BOE_TEST_BROWSER_IDLE_CUT_NONCE"] = idle_cut_nonce
            client = Peer("client-cold-" + str(ordinal), ["dotnet", str(dll), str(base), "--plain-output"], True)
            # Only this original client receives the diagnostic gate. The browser
            # host, Chromium and every later cold child run the ordinary path.
            env.pop("BOE_TEST_BROWSER_IDLE_CUT_PATH", None)
            env.pop("BOE_TEST_BROWSER_IDLE_CUT_NONCE", None)
            fresh(client, "Продолжить")
            assert (session / "stories/chaos_sea.jsonl").read_bytes() == preserved_story, "Cold startup changed prior full story"
            if scenario == "console-musings":
                cold_guardians = read_json(session / "game_state/meta/guardians.json")
                assert cold_guardians["guardians"][0]["musings"] == preserved_musings
                assert cold_guardians["activeGuardian"]["musings"] == preserved_musings
            result["ClientColdRestart"] = True
        offset = len(client.raw)
        client.send("\r")
        fresh(client, "Ваш ход", offset)
        if scenario == "console-musings":
            before_guardians = read_json(session / "game_state/meta/guardians.json")
            before_musings = before_guardians["guardians"][0]["musings"]
            if ordinal == 1:
                preserved_musings = before_musings
            assert before_musings == preserved_musings, "Ordinary entry changed the old musings before submission"
            assert before_guardians["activeGuardian"]["musings"] == preserved_musings
            (out / ("guardians-before-" + str(ordinal) + ".json")).write_bytes((session / "game_state/meta/guardians.json").read_bytes())
        action = "Осматриваюсь и запоминаю берег, проверочный ход " + str(ordinal) + "."
        offset = len(client.raw)
        if scenario == "browser-relay":
            submit_browser_action(dll, action, ordinal)
            offset = len(client.raw)
        else:
            client.send(action + "\r")
        wait(lambda: (session / "input/turn_request.json").exists(), "actual player request " + str(ordinal), 30)
        req = read_json(session / "input/turn_request.json")
        assert req["playerAction"] == action and req["turnNumber"] == ordinal, req
        if scenario == "browser-relay":
            submitted = result["BrowserSubmissions"][-1]["AuthoritativePending"]
            assert req["requestId"] == submitted["actionId"], "consumer replaced original browser action identity"
        wait(lambda: len([p for p in queue.glob("request-*") if (p / "game-request.json").exists()]) == (ordinal if ordinal < 3 else 1),
             "real daemon/relay delivery " + str(ordinal), 30)
        request_dir = sorted(queue.glob("request-*"), key=lambda p: p.stat().st_mtime)[-1]
        if scenario == "browser-relay" and ordinal == 2:
            # Original request delivered, authored GM response still withheld.
            # Cut only at the actual successfully closed staging idle boundary.
            wait(lambda: idle_cut_path.exists(), "actual staging close/disposal idle ACK", 15)
            idle_ack = read_json(idle_cut_path)
            assert idle_ack["schemaVersion"] == 1 and idle_ack["nonce"] == idle_cut_nonce
            assert idle_ack["pid"] == client.process.pid
            assert all(idle_ack[k] for k in ["observed", "remote", "topLevelStagingAwaitReturned", "outsideParticipation"])
            assert idle_ack["close"]["outcome"] == 0 and not idle_ack["close"]["closingFailed"], idle_ack
            assert idle_ack["close"]["identity"] == original, idle_ack
            assert read_json(record_path)["Identity"] == original_record and read_json(record_path)["Disposition"] == "Running"
            assert not (session / "ready/turn_complete.json").exists() and not (session / "ready/turn_error.json").exists()
            for path, expected_hash in idle_ack["artifactHashes"].items():
                assert sha((session / path).read_bytes()) == expected_hash.lower(), path
            pending_path = session / "input/pending_player_action.json"
            pending_bytes = pending_path.read_bytes()
            staged = read_json(pending_path)
            assert staged["status"] == "staged", staged
            assert idle_ack["actionId"] == idle_ack["requestId"] == staged["actionId"] == req["requestId"]
            assert idle_ack["generation"] == staged["sessionGeneration"]
            request_bytes = (session / "input/turn_request.json").read_bytes()
            previous = client
            os.kill(previous.process.pid, signal.SIGKILL)
            wait(lambda: previous.process.poll() is not None, "staged original client crash")
            wait(lambda: len(previous.closed) == len(previous.streams), "staged original client EOF")
            assert previous.process.returncode == -signal.SIGKILL
            client = Peer("client-staged-cold", ["dotnet", str(dll), str(base), "--plain-output"], True)
            fresh(client, "Продолжить")
            assert pending_path.read_bytes() == pending_bytes
            assert (session / "input/turn_request.json").read_bytes() == request_bytes
            assert (session / "stories/chaos_sea.jsonl").read_bytes() == preserved_story
            offset = len(client.raw)
            client.send("\r")
            # The retained-stage idle path emits no prompt before completion.
            # Its eventual original narrative/prompt below proves session entry.
            assert pending_path.read_bytes() == pending_bytes
            assert (session / "input/turn_request.json").read_bytes() == request_bytes
            assert len(list(queue.glob("request-*"))) == 2, "cold recovery dispatched a second original request"
            result.setdefault("InterruptedColdCuts", []).append({"Phase": "staged", "OriginalPid": previous.process.pid,
                "ColdPid": client.process.pid, "OriginalExitCode": previous.process.returncode, "OriginalEOF": True,
                "ActionId": staged["actionId"], "Generation": staged["sessionGeneration"], "RequestId": req["requestId"],
                "OriginalPendingSHA256": sha(pending_bytes), "OriginalRequestSHA256": sha(request_bytes), "RelayRequests": 2,
                "IdleCloseWitness": idle_ack, "SameOriginalRunBeforeCut": original_record})
        captured, narrative = author_packet(request_dir, ordinal)
        assert captured == req, "relay/helper consumed a substituted original request"
        fresh(client, narrative, offset, 40)
        fresh(client, "Ваш ход", offset, 40)
        story_path = session / "stories/chaos_sea.jsonl"
        story_bytes = story_path.read_bytes()
        prefix_sha = sha(preserved_story) if preserved_story else None
        assert story_bytes.startswith(preserved_story), "Previous full history bytes were changed or lost"
        preserved_story = story_bytes
        assert narrative in story_bytes.decode("utf-8-sig") and action in story_bytes.decode("utf-8-sig")
        assert not (session / "input/turn_request.json").exists(), "Acceptance still pending"
        entries = [json.loads(line) for line in story_bytes.decode("utf-8-sig").splitlines() if line.strip()]
        assert len(entries) == ordinal, entries
        if scenario == "console-musings":
            accepted_guardians = read_json(session / "game_state/meta/guardians.json")
            musings = accepted_guardians["guardians"][0]["musings"]
            assert musings[:len(preserved_musings)] == preserved_musings, "Accepted musings rewrote the full old prefix"
            assert len(musings) == len(preserved_musings) + 1 and musings[-1]["turn"] == ordinal, "Original command appended other than once"
            assert accepted_guardians["activeGuardian"]["musings"] == musings, "Active Guardian mirror drifted"
            assert not any(command.get("command") == "addMusings" for command in accepted_guardians.get("UpdateGuardians", []))
            preserved_musings = musings
            (out / ("guardians-after-" + str(ordinal) + ".json")).write_bytes((session / "game_state/meta/guardians.json").read_bytes())
            result.setdefault("GuardianMusings", []).append({"Turn": ordinal, "RequestId": req["requestId"],
                "FullPrefixPreserved": True, "CanonicalMirrorEqual": True, "BeforeCount": len(before_musings), "Count": len(musings)})
        if scenario == "browser-relay":
            row = entries[-1]
            assert row["requestId"] == req["requestId"] == submitted["actionId"], row
            assert row["sessionGeneration"] == submitted["sessionGeneration"], row
            assert not (session / "input/pending_player_action.json").exists(), "accepted action still queued"
            delivered = [read_json(path / "game-request.json") for path in queue.glob("request-*") if (path / "game-request.json").exists()]
            expected_ids = [turn["RequestId"] for turn in result["AcceptedTurns"]] + [req["requestId"]] if ordinal < 3 else [req["requestId"]]
            assert sorted(packet["requestId"] for packet in delivered) == sorted(expected_ids), "duplicate or substituted relay dispatch after acceptance"
            if ordinal in [1, 2]:
                cut = result["InterruptedColdCuts"][-1]
                assert req["requestId"] == cut["ActionId"]
                if ordinal == 2:
                    assert cut["RequestId"] == req["requestId"]
                assert cut["Generation"] == row["sessionGeneration"]
                cut["OriginalIdentityConsumedOnce"] = True
                cut["RelayIdsAfterAcceptance"] = sorted(packet["requestId"] for packet in delivered)
        execution = read_json(request_dir / "execution.json")
        assert execution["Executed"] and execution["ExitCode"] == 0 and execution["ChildExited"] and execution["IoDrained"], execution
        result["AcceptedTurns"].append({"Turn": ordinal, "SessionId": req["sessionId"], "RequestId": req["requestId"],
                                         "PlayerAction": action, "Narrative": narrative, "StorySHA256": sha(story_bytes), "PreservedFullStoryPrefixSHA256": prefix_sha, "StoryEntries": len(entries), "Execution": execution})
        (out / ("story-after-" + str(ordinal) + ".jsonl")).write_bytes(story_bytes)
        exit_client(client)
    assert len({t["SessionId"] for t in result["AcceptedTurns"]}) == 1
    assert len({t["RequestId"] for t in result["AcceptedTurns"]}) == 3
    assert len(result["OriginalRuns"]) == 2
    result["ChainAssertionsPassed"] = True
except Exception:
    result["Failure"] = traceback.format_exc()
    if client is not None and "Main run metadata or original owner admission is unavailable" in client.text():
        # Existing diagnostic entry point records the actual production stack.
        # It is diagnostic only and does not qualify an interactive game chain.
        try:
            script = out / "diagnostic-script.json"
            script.write_text('{"steps":[]}')
            diagnostic = Peer("client-diagnostic", ["dotnet", str(dll), str(base), "--plain-output", "--e2e-script", str(script), "--e2e-artifacts", str(out / "diagnostic")], True)
            wait(lambda: diagnostic.process.poll() is not None, "diagnostic actual client exit", 15)
        except Exception:
            result["DiagnosticFailure"] = traceback.format_exc()
finally:
    result["CleanupStartedAtSeconds"] = time.monotonic() - started
    cleanup_errors = []
    # Release cooperative fixture gates before ordinary failed-client cleanup.
    for path_name, nonce_name in [("queued_cut_path", "queued_cut_nonce"), ("idle_cut_path", "idle_cut_nonce")]:
        if path_name in globals():
            release_path = Path(str(globals()[path_name]) + ".release")
            temporary_release = Path(str(release_path) + ".tmp")
            temporary_release.write_text(globals()[nonce_name])
            os.replace(temporary_release, release_path)
    try:
        stop_chain()
    except Exception:
        cleanup_errors.append(traceback.format_exc())
    for p in peers:
        if p.process.poll() is None:
            try:
                p.send("\x03")
                wait(lambda: p.process.poll() is not None, p.name + " failed foreground exit", 8)
            except Exception:
                cleanup_errors.append(traceback.format_exc())
    try:
        wait(lambda: all(len(p.closed) == len(p.streams) for p in peers), "all owned EOF", 8)
        result["IoDrained"] = True
    except Exception:
        cleanup_errors.append(traceback.format_exc())
    if cleanup_errors:
        result["CleanupFailure"] = cleanup_errors
    result["Peers"] = {}
    for p in peers:
        (out / (p.name + ".raw")).write_bytes(p.raw)
        (out / (p.name + "-echo.raw")).write_bytes(p.echo)
        result["Peers"][p.name] = {"ExitCode": p.process.poll(), "EOF": len(p.closed) == len(p.streams), "Pid": p.process.pid}
        for fd in [p.master, p.slave]:
            if fd >= 0:
                os.close(fd)
    result["ElapsedSeconds"] = time.monotonic() - started
    result["OriginalStopped"] = bool(result.get("OriginalRuns") and
        len(result.get("OriginalStops", [])) == len(result["OriginalRuns"]) and
        all(any(stop["Identity"] == identity and stop["RecordAfter"]["Disposition"] == "Stopped"
                for stop in result["OriginalStops"]) for identity in result["OriginalRuns"]))
    result["PASS"] = bool(result.get("ChainAssertionsPassed") and result["OriginalStopped"] and result.get("IoDrained") and not result.get("Failure") and not result.get("CleanupFailure"))
    (out / "events.json").write_text(json.dumps(events, ensure_ascii=False, indent=2))
    (out / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2))
    print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result["PASS"] else 1)
