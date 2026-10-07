"""One startup-only Codex observation via ordinary M1; no terminal input or replay.

Run only under the independently owned, pinned host-guardian. Arguments name
this diagnostic's own runtime-only package and synthetic root, never user data.
"""
import errno
import fcntl
import json
import os
from pathlib import Path
import pty
import select
import socket
import struct
import subprocess
import sys
import termios
import time
import uuid

out = Path(sys.argv[1]).resolve()
ship = out / "ship"
scratch = out / "empty-codex-scratch"
root = out / "fixture-root"
session = root / "game_session"
assert not scratch.exists() and not root.exists()
scratch.mkdir()
session.mkdir(parents=True)
assert not list(scratch.iterdir())
assert os.environ.get("TERM") == "dumb", "Do not replace the inherited presentation"
pipe = "q1-" + uuid.uuid4().hex
quote = lambda value: "'" + str(value).replace("'", "''") + "'"
command = "& " + quote("/opt/codex/bin/codex") + " '--no-daemon' '-C' " + quote(scratch)
config = {
    "GmBridgeEnabled": True, "GmBridgeBackend": "OwnedTerminal",
    "GmMainOwnerBackend": "NativeLineage", "GmBridgeAutoStart": False,
    "GmBridgePipeNameOverride": pipe, "GmCliLaunchCommand": command,
    "GmBridgeShellWorkingDirectory": str(scratch), "GmWorkerBridgeProfiles": [],
    "MusicEnabled": False, "SoundEnabled": False,
    # Unsupported default observation profile: do not invent Codex readiness.
    "GmCliInputProfile": {"IdleMarker": "", "PromptPrefix": "", "WorkingMarker": ""},
}
(session / "config.json").write_text(json.dumps(config, indent=2) + "\n")
record_path = root / ".boe_runtime/gm-runs/main.json"
master, slave = pty.openpty()
fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 25, 100, 0, 0))
initial = termios.tcgetattr(slave)
started = time.monotonic()
captured = bytearray()
rpc_journal = []
result = {"Success": False, "InheritedTERM": os.environ["TERM"], "Dimensions": [100, 25],
          "InputJournal": [], "KeyboardBytes": 0, "ModelPromptsSent": 0,
          "ProfileIsSupported": False, "ConfiguredCommand": command,
          "NoNeutralPackage": True, "Scope": "ordinary-same-namespace-lineage"}
process = None
eof = False
original_identity = None


def elapsed():
    return time.monotonic() - started


def receive(delay=.02):
    global eof
    if not eof and select.select([master], [], [], delay)[0]:
        try:
            data = os.read(master, 65536)
        except OSError as ex:
            if ex.errno != errno.EIO:
                raise
            data = b""
        if not data:
            eof = True
        captured.extend(data)
        if len(captured) > 262144:
            raise RuntimeError("Startup capture exceeded 256KiB bound")


def rpc(payload, seconds):
    """One send, one reply; drain foreground output while the original peer waits."""
    assert payload["command"] in ["status", "diagnostics", "shutdown"]
    rpc_journal.append({"AtSeconds": round(elapsed(), 3), "Request": payload})
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as peer:
        peer.settimeout(.5)
        peer.connect("/tmp/CoreFxPipe_" + pipe)
        peer.sendall(json.dumps(payload).encode() + b"\n")
        peer.setblocking(False)
        reply = bytearray()
        deadline = min(started + 25, time.monotonic() + seconds)
        while time.monotonic() < deadline:
            readable, _, _ = select.select([peer] + ([] if eof else [master]), [], [], .02)
            if master in readable:
                receive(0)
            if peer in readable:
                data = peer.recv(8192)
                if not data:
                    raise EOFError("Original pipe closed before complete receipt")
                reply.extend(data)
                if len(reply) > 262144:
                    raise RuntimeError("RPC response exceeded bound")
                if reply.endswith(b"\n"):
                    response = json.loads(reply)
                    rpc_journal[-1]["Response"] = response
                    return response
        raise TimeoutError("Original RPC outcome unknown; never resend")


def own_foreground():
    os.setsid()
    fcntl.ioctl(slave, termios.TIOCSCTTY, 0)
    os.tcsetpgrp(slave, os.getpid())


try:
    args = ["pwsh", "-NoLogo", "-NoProfile", "-File",
            str(ship / "BookOfEternityClient/Launcher/bookofeternity.ps1"),
            "start-bridge", "visible", "-SessionPath", str(session)]
    result["Argv"] = args
    process = subprocess.Popen(args, cwd=ship, stdin=slave, stdout=slave, stderr=slave,
                               preexec_fn=own_foreground)
    result["OriginalLauncherPid"] = process.pid
    gate = None
    # Capture once; no reply to trust/access/update/terms/TERM or terminal query.
    while elapsed() < 8:
        receive()
        if b"Continue anyway?" in captured:
            gate = "TERM confirmation"
            break
        lower = bytes(captured).lower()
        if any(marker in lower for marker in [b"do you trust", b"trust this", b"accept the terms", b"sign in to codex"]):
            gate = "Other CLI confirmation/authentication gate"
            break
        if process.poll() is not None:
            break
    result["ObservationEndedAtSeconds"] = round(elapsed(), 3)
    result["Gate"] = gate or "No qualified readiness within bounded observation"
    running = json.loads(record_path.read_bytes())
    result["RunningRecord"] = running
    assert running["Disposition"] == "Running", "M1 did not reach acknowledged Running"
    original_identity = {k[0].lower() + k[1:]: v for k, v in running["Identity"].items()}
    original_identity["backend"] = 2
    status = rpc({"command": "status"}, 2)
    diagnostics = rpc({"command": "diagnostics"}, 2)
    result["StartupStatus"] = status
    result["StartupDiagnostics"] = diagnostics
    assert status["ok"] and not status["status"]["ready"]
    assert status["status"]["terminalRunId"] == original_identity["runId"]
    assert status["status"]["cliLaunchCommand"] == command
    assert status["status"]["shellWorkingDirectory"] == str(scratch)
    result["ShutdownRequestAtSeconds"] = round(elapsed(), 3)
    stopped = rpc({"command": "shutdown", "rootKey": original_identity["rootKey"],
                   "expectedMainIdentity": original_identity}, 12)
    result["ShutdownReceipt"] = stopped
    assert stopped["ok"], "Original stop unconfirmed"
    proof = stopped["status"]["terminalStop"]
    assert proof["identity"]["runId"] == original_identity["runId"]
    assert proof["cleanupComplete"] and not proof["authorityRetained"]
    while process.poll() is None and elapsed() < 25:
        receive()
    assert process.poll() == 0, "Original launcher did not exit successfully"
    # Only master remains after original launcher and owned terminal exit.
    os.close(slave)
    slave = -1
    while not eof and elapsed() < 25:
        receive()
    settled = json.loads(record_path.read_bytes())
    result["StoppedRecord"] = settled
    assert settled["Disposition"] == "Stopped" and settled["Identity"] == running["Identity"]
    assert eof, "Foreground PTY was not actually drained"
    result["OriginalScopedStopReceiptObserved"] = True
    result["Success"] = True
except Exception as ex:
    # No retry, broad signals, PID-based cleanup or false logical settlement.
    # Independent guardian owns emergency physical cleanup after this driver exits.
    result["Failure"] = type(ex).__name__ + ": " + str(ex)
finally:
    result["LauncherExitCode"] = process.poll() if process is not None else None
    result["ForegroundPtyEOF"] = eof
    result["ForegroundTermiosRestored"] = termios.tcgetattr(master) == initial
    if slave >= 0:
        os.close(slave)
    os.close(master)
    result["ForegroundDescriptorsClosed"] = True
    result["ScratchFilesAfter"] = sorted(str(p.relative_to(scratch)) for p in scratch.rglob("*"))
    result["ElapsedSeconds"] = round(elapsed(), 3)
    result["CapturedBytes"] = len(captured)
    (out / "startup.raw").write_bytes(captured)
    (out / "startup-readable.txt").write_text(captured.decode("utf-8", errors="replace").replace("\x1b", "<ESC>"))
    (out / "rpc-journal.json").write_text(json.dumps(rpc_journal, indent=2) + "\n")
    (out / "probe-result.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"Success": result["Success"], "Gate": result.get("Gate"), "Failure": result.get("Failure"),
                      "KeyboardBytes": 0, "ElapsedSeconds": result["ElapsedSeconds"]}))
sys.exit(0 if result["Success"] else 1)
