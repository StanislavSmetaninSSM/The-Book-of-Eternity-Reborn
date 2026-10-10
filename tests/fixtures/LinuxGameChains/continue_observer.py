"""Passive instrumentation for one real Continue click; no replacement actions."""
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import time


class ContinueObserver:
    def __init__(self, out, name):
        self.out, self.name = out, name
        self.rows = []
        self.events = (out / (name + "-continue-events.jsonl")).open("x", buffering=1)
        self.mark("ClockAnchor")

    def mark(self, event, **values):
        row = {"Event": event, "PythonMonotonic": time.monotonic(),
               "Utc": datetime.now(timezone.utc).isoformat(), **values}
        self.rows.append(row)
        self.events.write(json.dumps(row, ensure_ascii=False) + "\n")

    @contextmanager
    def driver_logging(self):
        settings = {"DEBUG": "pw:api,pw:protocol,pw:browser",
                    "DEBUG_FILE": str(self.out / (self.name + "-playwright-driver.log")),
                    "DEBUG_COLORS": "0"}
        old = {key: os.environ.get(key) for key in settings}
        os.environ.update(settings)
        try:
            yield
        finally:
            for key, value in old.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value
            self.mark("DriverStoppedClockAnchor")
            self.events.close()

    def attach(self, page):
        page.context.tracing.start(screenshots=True, snapshots=True, sources=True)
        page.on("request", lambda request: self.mark("Request", Url=request.url,
                Method=request.method, ResourceType=request.resource_type))
        page.on("response", lambda response: self.mark("Response", Url=response.url,
                Status=response.status, Headers=response.headers))
        page.on("requestfailed", lambda request: self.mark("RequestFailed", Url=request.url,
                Failure=request.failure))
        page.on("pageerror", lambda error: self.mark("PageError", Error=str(error)))
        page.on("console", lambda message: self.mark("Console", Type=message.type, Text=message.text))
        self.cdp = page.context.new_cdp_session(page)
        for event in ["Network.requestWillBeSent", "Network.responseReceived", "Network.loadingFinished",
                      "Network.loadingFailed", "Page.frameNavigated", "Page.domContentEventFired",
                      "Page.loadEventFired", "Log.entryAdded"]:
            self.cdp.on(event, lambda params, event=event: self.mark(event, Params=params))
        self.cdp.send("Network.enable")
        self.cdp.send("Page.enable")
        self.cdp.send("Log.enable")
        page.add_init_script("""(() => {
          const report = value => console.debug('__boe_continue_observe__' + JSON.stringify({
            browserNow: performance.now(), browserTimeOrigin: performance.timeOrigin,
            wallMilliseconds: Date.now(), ...value }));
          for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
            document.addEventListener(type, event => {
              const button = event.target instanceof Element
                ? event.target.closest('button[data-launcher-mode="continue"]') : null;
              if (button) report({ event: type, isTrusted: event.isTrusted,
                button: event.button, detail: event.detail, disabled: button.disabled });
            }, true);
          }
          let last = false;
          new MutationObserver(() => {
            const composer = Boolean(document.querySelector('textarea[aria-label="Команда или действие"]'));
            if (composer !== last) {
              last = composer;
              report({ event: 'composer-dom-change', composer,
                activeTab: document.querySelector('main.browser-shell')?.dataset.activeTab ?? null });
            }
          }).observe(document, { childList: true, subtree: true });
        })();""")

    def returned_click_oracle(self, page, server_path):
        # Only called after the original click API actually returned successfully.
        trusted = []
        for row in self.rows:
            if row["Event"] == "Console" and row["Text"].startswith("__boe_continue_observe__"):
                payload = json.loads(row["Text"].split("__boe_continue_observe__", 1)[1])
                if payload["event"] == "click" and payload["isTrusted"]:
                    trusted.append(payload)
        server_rows = [json.loads(line) for line in server_path.read_text().splitlines()]
        required = ["/api/main-menu", "/api/session", "/api/game-screen"]
        browser_responses = {path: [r for r in self.rows if r["Event"] == "Response"
                             and r["Url"].endswith(path) and r["Status"] == 200] for path in required}
        server_responses = {path: [r for r in server_rows if r["Path"] == path and r["Status"] == 200
                            and "Stop" in r["Event"] and "Chrome/" in (r["UserAgent"] or "")] for path in required}
        oracle = {"NormalClickAPIReturned": True, "TrustedContinueClicks": len(trusted),
                  "ComposerVisible": page.get_by_label("Команда или действие", exact=True).is_visible(),
                  "BrowserStateResponses": {p: len(rows) for p, rows in browser_responses.items()},
                  "CorrelatedServerStateStops": {p: [r["TraceIdentifier"] for r in rows] for p, rows in server_responses.items()},
                  "ServerObserverErrors": sum(r["Event"] == "ObserverError" for r in server_rows),
                  "CauseEstablishedByPassingDiagnostic": False}
        self.mark("NormalActionOracle", Oracle=oracle)
        assert len(trusted) == 1 and oracle["ComposerVisible"], oracle
        assert all(browser_responses.values()) and all(server_responses.values()), oracle
        assert oracle["ServerObserverErrors"] == 0, oracle
        return oracle

    def stop(self, page):
        self.mark("TraceStopBegin")
        page.context.tracing.stop(path=str(self.out / (self.name + "-continue-trace.zip")))
        self.cdp.detach()
        self.mark("TraceStopEnd")
        import playwright
        package = Path(playwright.__file__).parent
        files = [package / "driver/package/lib/coreBundle.js", package / "_impl/_transport.py",
                 package / "_impl/_driver.py", package / "sync_api/_generated.py"]
        provenance = {"Playwright": importlib.metadata.version("playwright"),
                      "OriginalAction": 'Locator.click(no_wait_after=True), unchanged default12000ms',
                      "Instrumentation": "DEBUG_FILE pw:api/protocol/browser; real CDP passive network/events; Playwright trace; passive DOM trusted input/composer; owned ASP.NET DiagnosticListener startup observer",
                      "BodyReadsInEventCallbacks": False, "SyntheticOrRepeatedClick": False,
                      "InstalledDriverFiles": [{"Path": str(p), "Bytes": p.stat().st_size,
                                                 "SHA256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]}
        (self.out / (self.name + "-continue-provenance.json")).write_text(json.dumps(provenance, indent=2))
