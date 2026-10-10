"""Real shipped Program --web + built React + Chromium, local commands/save handlers.

The initial current-schema Mortal/item fixture is explicit, not a GM turn.
No HTTP route interception, service replacement, model or provider is used.
Every save/load/command mutation below is initiated through actual React controls.
"""
import fcntl
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import pty
import socket
import subprocess
import sys
import termios
import time
import traceback
import urllib.request
from playwright.sync_api import sync_playwright

mode = sys.argv[1]
assert mode in ["inventory", "saves"]
repo, out, ship = [Path(s).resolve() for s in sys.argv[2:5]]
base, session = out / "play", out / "play/game_session"
started = time.monotonic()
hosts, browsers, http, events = [], [], [], []
result = {"Scenario": "C3" if mode == "inventory" else "C4-save-load",
          "ProductionEntrypoint": "Program.cs --web → LocalWebUiHost; actual built React/Chromium",
          "InitialState": "explicit current-schema sealed item/Mortal fixture; not GM materialization",
          "Model": "none; local player commands only", "ModelCalls": 0,
          "WholeHostBrowserColdRestart": False}
env = dict(os.environ, TERM="dumb", NO_COLOR="1")
for key, name in [("TMPDIR", "tmp"), ("XDG_CONFIG_HOME", "config"),
                  ("XDG_DATA_HOME", "data"), ("XDG_CACHE_HOME", "cache")]:
    directory = out / name
    directory.mkdir()
    env[key] = str(directory)
with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
url = "http://127.0.0.1:" + str(port)
paths = ["game_state/inventory/items.json", "game_state/inventory/item_identity_index.json",
         "game_state/resources/resource_definitions.json", "game_state/resources/resource_state.json",
         "game_state/resources/resource_history.json", "game_state/resources/resource_owner_authority.json"]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def snapshot(label):
    saved = {}
    directory = out / "snapshots" / label
    directory.mkdir(parents=True)
    for name in paths:
        path = session / name
        if path.exists():
            data = path.read_bytes()
            saved[name] = hashlib.sha256(data).hexdigest()
            target = directory / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        else:
            saved[name] = "absent"
    saved["generation"] = read_json(base / ".boe_runtime/session-generation/current.json")["generationId"]
    (directory / "snapshot.json").write_text(json.dumps(saved, indent=2))
    result.setdefault("Snapshots", {})[label] = saved
    return saved


def children(pid):
    path = Path("/proc") / str(pid) / "task" / str(pid) / "children"
    if not path.exists():
        return []
    direct = [int(x) for x in path.read_text().split()]
    return direct + [c for p in direct for c in children(p)]


def chromium_pids():
    return [pid for pid in children(os.getpid())
            if (Path("/proc") / str(pid) / "comm").exists()
            and "chromium" in (Path("/proc") / str(pid) / "comm").read_text()]


def exited(pid):
    path = Path("/proc") / str(pid) / "stat"
    return not path.exists() or path.read_text().split(")", 1)[1].split()[0] == "Z"


def wait(predicate, label, seconds=12):
    deadline = time.monotonic() + seconds
    while not predicate():
        assert time.monotonic() < deadline, "Timeout: " + label
        assert time.monotonic() - started < 150, "Driver work budget exceeded"
        time.sleep(.05)
    events.append({"Observed": label, "Seconds": time.monotonic() - started})


def start_host(name):
    master, slave = pty.openpty()
    log = (out / (name + ".log")).open("wb")
    def own():
        os.setsid()
        fcntl.ioctl(slave, termios.TIOCSCTTY, 0)
        os.tcsetpgrp(slave, os.getpid())
    args = ["dotnet", str(ship / "BookOfEternityClient/BookOfEternityClient.dll"),
            str(base), "--web", "--web-url", url]
    process = subprocess.Popen(args, cwd=ship, env=env, stdin=slave, stdout=log,
                               stderr=subprocess.STDOUT, preexec_fn=own)
    os.close(slave)
    host = {"Name": name, "Process": process, "Master": master, "Log": log}
    hosts.append(host)
    events.append({"Host": name, "Pid": process.pid, "Args": args})
    def ready():
        assert process.poll() is None, "Actual web host exited"
        try:
            with urllib.request.urlopen(url, timeout=1) as response:
                return response.status == 200
        except (OSError, urllib.error.URLError):
            return False
    wait(ready, name + " actual HTTP startup")
    return host


def stop_host(host):
    assert host["Process"].poll() is None, "Web host unexpectedly exited before normal close"
    assert os.write(host["Master"], b"\x03") == 1
    wait(lambda: host["Process"].poll() is not None, host["Name"] + " foreground Ctrl+C exit")
    assert host["Process"].returncode == 0
    host["Log"].close()
    os.close(host["Master"])
    host["Closed"] = True


def record(response):
    if response.request.method != "POST" or "/api/" not in response.url:
        return
    try:
        http.append({"URL": response.url.removeprefix(url), "Status": response.status,
                     "Request": response.request.post_data_json, "Response": response.json()})
    except Exception:
        http.append({"URL": response.url.removeprefix(url), "Status": response.status,
                     "CaptureFailure": traceback.format_exc()})


def open_browser(playwright, name):
    browser = playwright.chromium.launch(executable_path="/usr/bin/chromium", headless=True,
                                         args=["--no-sandbox", "--disable-dev-shm-usage"], env=env)
    page = browser.new_page(viewport={"width": 1280, "height": 900}, reduced_motion="reduce")
    page.set_default_timeout(12000)
    page.on("response", record)
    page.on("pageerror", lambda error: events.append({"PageError": str(error)}))
    pids = chromium_pids()
    assert pids, "No real Chromium process observed"
    owned = {"Name": name, "Browser": browser, "Page": page, "Pids": pids, "Closed": False}
    browsers.append(owned)
    events.append({"Browser": name, "ChromiumPids": pids})
    page.goto(url, wait_until="domcontentloaded")
    # The real launcher button's accessible name includes its descriptive copy.
    page.locator('button[data-launcher-mode="continue"]').click()
    page.get_by_label("Команда или действие", exact=True).wait_for(state="visible")
    return owned


def close_browser(owned):
    owned["Browser"].close()
    wait(lambda: all(exited(pid) for pid in owned["Pids"]), owned["Name"] + " all recorded Chromium exited")
    owned["Closed"] = True


@contextmanager
def owned_playwright():
    with sync_playwright() as playwright:
        try:
            yield playwright
        except Exception:
            for owned in browsers:
                if not owned["Closed"]:
                    try:
                        owned["Page"].screenshot(path=str(out / (owned["Name"] + "-failure.png")))
                        (out / (owned["Name"] + "-failure.html")).write_text(owned["Page"].content())
                    except Exception:
                        result["FailureCapture"] = traceback.format_exc()
            raise
        finally:
            for owned in browsers:
                if not owned["Closed"]:
                    close_browser(owned)


def post_click(page, path, click):
    with page.expect_response(lambda r: r.request.method == "POST" and r.url == url + path) as pending:
        click()
    response = pending.value
    body = response.json()
    assert response.status == 200, {"Path": path, "Status": response.status, "Body": body}
    return body


def command(page, kind, quantity=None):
    composer = page.get_by_label("Команда или действие", exact=True)
    composer.fill("/inventory_" + kind + " gc_stack")
    post_click(page, "/api/explorer/command", lambda: page.get_by_role("button", name="Отправить", exact=True).click())
    page.locator("#prompt-item_identity").select_option("gc_stack")
    if quantity is not None:
        page.locator("#prompt-split_quantity").fill(str(quantity))
    page.locator("#prompt-confirm_inventory_" + kind).check()
    body = post_click(page, "/api/explorer/prompt-sessions/submit",
                      lambda: page.get_by_role("button", name="Отправить форму", exact=True).click())
    page.locator(".prompt-form").wait_for(state="detached")
    return body


def items():
    return read_json(session / paths[0])["items"]


def assert_split(original_receipt):
    current = items()
    assert sorted(i["count"] for i in current) == [2, 3], current
    parent = next(i for i in current if i["itemId"] == "gc_stack")
    child = next(i for i in current if i["itemId"] != "gc_stack")
    assert parent["count"] == 3 and parent["materializationReceipt"] == original_receipt
    assert child["materializationReceipt"]["instanceKind"] == "split_derived"
    assert child["materializationReceipt"]["parentItemIds"] == ["gc_stack"]
    index = read_json(session / paths[1])["entries"]
    assert len(index) == 2
    assert next(e for e in index if e["itemId"] == child["itemId"])["transitions"][-1]["kind"] == "split"


try:
    initial = snapshot("initial")
    original_receipt = items()[0]["materializationReceipt"]
    with owned_playwright() as playwright:
        first_host = start_host("host-first")
        first_browser = open_browser(playwright, "browser-first")
        page = first_browser["Page"]
        if mode == "saves":
            page.get_by_role("tab", name="Настройки (4)", exact=True).click()
            created = post_click(page, "/api/saves/create", lambda: page.get_by_role("button", name="Сохранить игру", exact=True).click())
            assert created["success"] and created["disposition"] == "Committed" and created["createdSaveId"]
            assert not created["continuationBlocked"] and not created["needsFollowUp"]
            page.get_by_text("Игра сохранена.", exact=True).wait_for(state="visible")
            result["CreatedSave"] = created
        command(page, "split", 2)
        assert_split(original_receipt)
        changed = snapshot("after-split")
        assert changed[paths[0]] != initial[paths[0]] and changed[paths[1]] != initial[paths[1]]
        expected = changed
        if mode == "saves":
            page.get_by_role("tab", name="Настройки (4)", exact=True).click()
            with page.expect_response(lambda r: r.request.method == "POST" and r.url == url + "/api/saves/load-complete") as completion:
                loaded = post_click(page, "/api/saves/load", lambda: page.get_by_role("button", name="Загрузить сохранение", exact=True).click())
            assert loaded["success"] and loaded["disposition"] == "Committed"
            assert not loaded["continuationBlocked"] and not loaded["needsFollowUp"]
            completed = completion.value
            assert completed.status == 200 and completed.json()["success"], completed.json()
            assert completed.request.post_data_json["operationId"] == loaded["lifecycleOperationId"]
            assert completed.request.post_data_json["establishedGeneration"] == loaded["establishedGeneration"]
            assert completed.request.post_data_json["refreshConfirmed"] is True
            page.get_by_label("Команда или действие", exact=True).wait_for(state="visible")
            assert len(items()) == 1 and items()[0]["count"] == 5 and items()[0]["materializationReceipt"] == original_receipt
            expected = snapshot("after-load")
            assert all(expected[p] == initial[p] for p in paths), "Load did not restore exact saved inventory/resources/authority"
            assert expected["generation"] != initial["generation"]
            result["LoadedSave"] = loaded
            result["RollbackScope"] = "UNRUN; happy-path save/load does not qualify injected rollback"
        close_browser(first_browser)
        stop_host(first_host)
        cold_host = start_host("host-cold")
        cold_browser = open_browser(playwright, "browser-cold")
        assert cold_host["Process"].pid != first_host["Process"].pid
        assert set(cold_browser["Pids"]).isdisjoint(first_browser["Pids"])
        assert snapshot("cold-before-command") == expected, "Whole host/browser restart changed previously saved bytes or generation"
        result["WholeHostBrowserColdRestart"] = True
        page = cold_browser["Page"]
        if mode == "inventory":
            assert_split(original_receipt)
            command(page, "merge")
            assert len(items()) == 1 and items()[0]["count"] == 5 and items()[0]["itemId"] == "gc_stack"
            index = read_json(session / paths[1])["entries"]
            assert next(e for e in index if e["itemId"] == "gc_stack")["transitions"][-1]["kind"] == "merge"
        else:
            command(page, "split", 2)
            assert_split(original_receipt)
        snapshot("cold-after-next-command")
        page.screenshot(path=str(out / "accepted-ui.png"))
        close_browser(cold_browser)
        stop_host(cold_host)
        result["ChainAssertionsPassed"] = True
except Exception:
    result["Failure"] = traceback.format_exc()
    for owned in browsers:
        if not owned["Closed"]:
            try:
                owned["Page"].screenshot(path=str(out / (owned["Name"] + "-failure.png")))
                (out / (owned["Name"] + "-failure.html")).write_text(owned["Page"].content())
            except Exception:
                result["FailureCapture"] = traceback.format_exc()
finally:
    failures = []
    for owned in browsers:
        if not owned["Closed"]:
            try:
                close_browser(owned)
            except Exception:
                failures.append(traceback.format_exc())
    for host in hosts:
        if not host.get("Closed"):
            try:
                stop_host(host)
            except Exception:
                failures.append(traceback.format_exc())
    if failures:
        result["CleanupFailure"] = failures
    result["Hosts"] = [{"Name": h["Name"], "Pid": h["Process"].pid, "ExitCode": h["Process"].poll(),
                        "OutputFileClosed": h["Log"].closed, "NormalClose": h.get("Closed", False)} for h in hosts]
    result["Browsers"] = [{"Name": b["Name"], "ChromiumPids": b["Pids"], "Closed": b["Closed"]} for b in browsers]
    result["ElapsedSeconds"] = time.monotonic() - started
    result["PASS"] = bool(result.get("ChainAssertionsPassed") and result["WholeHostBrowserColdRestart"]
                           and not result.get("Failure") and not failures)
    (out / "http.json").write_text(json.dumps(http, ensure_ascii=False, indent=2))
    (out / "events.json").write_text(json.dumps(events, ensure_ascii=False, indent=2))
    (out / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2))
    print(json.dumps(result, ensure_ascii=False))
sys.exit(0 if result["PASS"] else 1)
