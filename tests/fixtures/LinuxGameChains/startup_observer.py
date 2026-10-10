"""One finite ready-launcher observation and one conditional owned stack report."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time


STATE_PATHS = ["/api/main-menu", "/api/session", "/api/game-screen", "/api/audio/settings",
               "/api/client/settings", "/api/explorer/command-coverage"]
READY_LAUNCHER = """() => {
  const shell = document.querySelector('main.browser-shell.is-launcher-route');
  const button = shell?.querySelector('button[data-launcher-mode="continue"]');
  return Boolean(button && !button.disabled && button.getClientRects().length &&
    getComputedStyle(button).visibility !== 'hidden' && !shell.querySelector('.loading-shimmer'));
}"""


class FullC5LauncherPrerequisite:
    """Only the full duration diagnostic: separate real startup from its normal click."""
    def __init__(self, page, name, ordinal, result):
        self.start = time.monotonic()
        self.deadline = self.start + 60
        self.requests, self.trusted = {}, []
        self.collecting_initial = True
        self.record = {"Name": name, "Ordinal": ordinal, "StartupSeconds": 60,
                       "ClickMilliseconds": 12000, "Anchor": "before initial page.goto",
                       "BeginMonotonic": self.start, "NormalClickInvoked": False, "NormalClickAPIReturned": False,
                       "SixInitialGETs": [], "TrustedContinueEvents": self.trusted}
        result.setdefault("BrowserStartupPrerequisites", []).append(self.record)

        def request_started(request):
            path = next((p for p in STATE_PATHS if request.url.endswith(p)), None)
            if self.collecting_initial and path is not None and request.method == "GET":
                row = {"Path": path, "Url": request.url, "Method": request.method,
                       "ObserverRequestSequence": len(self.requests) + 1,
                       "BeginMonotonic": time.monotonic(), "Status": None, "Terminal": None}
                self.requests[request] = row
                self.record["SixInitialGETs"].append(row)

        def response_received(response):
            if response.request in self.requests:
                self.requests[response.request].update(Status=response.status, ResponseMonotonic=time.monotonic())

        def request_ended(request, terminal):
            if request in self.requests:
                self.requests[request].update(Terminal=terminal, EndMonotonic=time.monotonic(),
                                              Failure=request.failure if terminal == "requestfailed" else None)

        def console(message):
            prefix = "__boe_full_c5_continue__"
            if message.text.startswith(prefix):
                self.trusted.append(json.loads(message.text[len(prefix):]))

        page.on("request", request_started)
        page.on("response", response_received)  # Metadata only; no unfinished body reads.
        page.on("requestfinished", lambda request: request_ended(request, "requestfinished"))
        page.on("requestfailed", lambda request: request_ended(request, "requestfailed"))
        page.on("console", console)
        page.add_init_script("""document.addEventListener('click', event => {
          const button = event.target instanceof Element
            ? event.target.closest('button[data-launcher-mode="continue"]') : null;
          if (button) console.debug('__boe_full_c5_continue__' + JSON.stringify({
            isTrusted: event.isTrusted, disabled: button.disabled, browserNow: performance.now(),
            browserTimeOrigin: performance.timeOrigin, wallMilliseconds: Date.now() }));
        }, true);""")

    def wait_ready(self, page):
        while True:
            remaining = self.deadline - time.monotonic()
            if remaining <= 0:
                self.record.update(StartupDeadlineReached=True, StartupElapsedSeconds=time.monotonic() - self.start)
                raise RuntimeError("Full C5 actual ready launcher exceeded the separate60s startup prerequisite")
            # Timeout is a refusal of readiness; it never counts as a completed click.
            page.wait_for_function(READY_LAUNCHER, timeout=remaining * 1000, polling=100)
            initial = [next((r for r in self.requests.values() if r["Path"] == path), None) for path in STATE_PATHS]
            if all(row is not None and row["Terminal"] is not None for row in initial):
                if any(row["Terminal"] != "requestfinished" or row["Status"] != 200 for row in initial):
                    raise RuntimeError("Full C5 initial state GET failed; normal Continue was not invoked")
                elapsed = time.monotonic() - self.start
                if elapsed > 60:
                    raise RuntimeError("Full C5 readiness proof exceeded its separate60s startup prerequisite")
                self.collecting_initial = False
                self.record["SixInitialGETs"] = initial
                self.record.update(ReadyLauncher=True, ContinueVisible=True, ContinueEnabled=True,
                                   StartupElapsedSeconds=elapsed, ReadyMonotonic=time.monotonic())
                return
            page.wait_for_timeout(max(1, min(100, (self.deadline - time.monotonic()) * 1000)))

    def before_click(self):
        self.record.update(NormalClickInvoked=True, ClickBeginMonotonic=time.monotonic())

    def click_returned(self):
        self.record.update(NormalClickAPIReturned=True, ClickReturnMonotonic=time.monotonic())

    def assert_normal_action(self, page):
        page.get_by_label("Команда или действие", exact=True).wait_for(state="visible")
        trusted = [row for row in self.trusted if row["isTrusted"] and not row["disabled"]]
        self.record.update(ComposerVisible=True, TrustedContinueClicks=len(trusted))
        assert len(trusted) == 1, self.record


class StartupProbe:
    def __init__(self, observer, web, environment, stack_tool):
        self.trace, self.web, self.environment = observer, web, environment
        self.tool = Path(stack_tool).resolve()
        if not self.tool.is_file():
            raise RuntimeError("Startup diagnostic requires the prepared owned dotnet-stack tool")
        self.start = time.monotonic()
        self.deadline = self.start + 60
        self.stack = None
        self.stack_started = None
        self.stack_files = []
        self.stack_attempted = False
        self.web_starttime = self.linux_starttime(web.process.pid)
        self.trace.mark("StartupObservationBegin", StartupSeconds=60, ClickMilliseconds=12000,
                        StackAtSeconds=30, StackChildSeconds=15, Anchor="before initial page.goto",
                        WebPid=web.process.pid, WebLinuxStartTime=self.web_starttime,
                        Tool=str(self.tool), ToolSHA256=hashlib.sha256(self.tool.read_bytes()).hexdigest())

    @staticmethod
    def linux_starttime(pid):
        return Path(f"/proc/{pid}/stat").read_text().rpartition(")")[2].split()[19]

    def server_rows(self):
        path = self.trace.out / (self.trace.name + "-server-http.jsonl")
        # The writer may currently be appending its last row. Parse complete rows only.
        return [json.loads(line) for line in path.read_text().splitlines(keepends=True) if line.endswith("\n")]

    def game_pending(self):
        rows = [r for r in self.server_rows() if r.get("Path") == "/api/game-screen"
                and "Chrome/" in (r.get("UserAgent") or "")]
        return any(r["Event"].endswith("HttpRequestIn.Start") for r in rows) and not any(
            r["Event"].endswith("HttpRequestIn.Stop") for r in rows)

    def state_outcomes(self):
        server, state = self.server_rows(), []
        for path in STATE_PATHS:
            request = next((r for r in self.trace.rows if r["Event"] == "Network.requestWillBeSent"
                            and r["Params"]["request"]["url"].endswith(path)), None)
            rid = request["Params"]["requestId"] if request else None
            response = next((r for r in self.trace.rows if r["Event"] == "Network.responseReceived"
                             and r["Params"]["requestId"] == rid), None)
            terminal = next((r for r in self.trace.rows if r["Event"] in ["Network.loadingFinished", "Network.loadingFailed"]
                             and r["Params"]["requestId"] == rid), None)
            start = next((r for r in server if r.get("Path") == path and r["Event"].endswith("HttpRequestIn.Start")
                          and "Chrome/" in (r.get("UserAgent") or "")), None)
            stop = next((r for r in server if start and r.get("TraceIdentifier") == start["TraceIdentifier"]
                         and r["Event"].endswith("HttpRequestIn.Stop")), None)
            state.append({"Path": path, "CDPRequestId": rid,
                          "CDPRequestTimestamp": request["Params"]["timestamp"] if request else None,
                          "CDPResponseTimestamp": response["Params"]["timestamp"] if response else None,
                          "BrowserStatus": response["Params"]["response"]["status"] if response else None,
                          "BrowserTerminal": terminal["Event"] if terminal else None,
                          "CDPTerminalTimestamp": terminal["Params"]["timestamp"] if terminal else None,
                          "TraceIdentifier": start["TraceIdentifier"] if start else None,
                          "ActivityTraceId": start["ActivityTraceId"] if start else None,
                          "ServerStatus": stop["Status"] if stop else None,
                          "ServerElapsedSeconds": (stop["StopwatchTicks"] - start["StopwatchTicks"]) / start["StopwatchFrequency"] if stop else None})
        return state

    def start_stack(self):
        self.stack_attempted = True
        if self.web.process.poll() is not None or self.linux_starttime(self.web.process.pid) != self.web_starttime:
            raise RuntimeError("Owned server identity changed before stack report")
        paths = [self.trace.out / (self.trace.name + "-managed-stack." + suffix) for suffix in ["stdout", "stderr"]]
        self.stack_files = [path.open("xb") for path in paths]
        command = [str(self.tool), "report", "--process-id", str(self.web.process.pid)]
        # The resource bound affects only this owned reporter child; exec retains
        # its PID. No preexec callback runs in the multithreaded fixture parent.
        wrapper = "import os,resource,sys;resource.setrlimit(resource.RLIMIT_FSIZE,(2097152,2097152));os.execv(sys.argv[1],sys.argv[1:])"
        child_env = dict(self.environment)
        child_env["DOTNET_ROOT"] = str(self.tool.parent.parent / "dotnet")
        self.stack_started = time.monotonic()
        self.stack = subprocess.Popen([sys.executable, "-c", wrapper, *command], env=child_env,
                                      stdin=subprocess.DEVNULL, stdout=self.stack_files[0], stderr=self.stack_files[1])
        try:
            reporter_starttime = self.linux_starttime(self.stack.pid)
        except FileNotFoundError:
            reporter_starttime = None  # A fast reporter exit is retained, not attributed a guessed identity.
        self.trace.mark("ManagedStackReportBegin", Command=command, ReporterPid=self.stack.pid,
                        ReporterLinuxStartTime=reporter_starttime, ReporterExitedBeforeIdentityRead=reporter_starttime is None,
                        WebPid=self.web.process.pid,
                        WebLinuxStartTime=self.web_starttime, TMPDIR=child_env["TMPDIR"], ChildSeconds=15,
                        EachOutputFileByteCap=2097152, EventPipeThreadStacksOnly=True,
                        AsyncWaitAbsenceCannotBeInferred=True)

    def finish_stack(self, wait=False):
        if self.stack is None:
            return
        elapsed = time.monotonic() - self.stack_started
        if wait and self.stack.poll() is None:
            try:
                self.stack.wait(timeout=max(0.001, min(15 - elapsed, self.deadline - time.monotonic())))
            except subprocess.TimeoutExpired:
                pass
        if self.stack.poll() is None and not wait and elapsed < 15:
            return
        deadline = self.stack.poll() is None
        if deadline:
            self.stack.kill()  # Only the owned reporter; never the server or browser.
            self.stack.wait(timeout=2)
        code = self.stack.returncode
        for stream in self.stack_files:
            stream.close()
        self.trace.mark("ManagedStackReportEnd", ReporterPid=self.stack.pid, ExitCode=code, Joined=True,
                        StdoutClosed=True, StderrClosed=True, IoSettled=True,
                        ChildDeadlineReached=deadline, CaptureIncomplete=deadline or code != 0,
                        ElapsedSeconds=time.monotonic() - self.stack_started,
                        OutputFiles=[{"Path": str(stream.name), "Bytes": Path(stream.name).stat().st_size}
                                     for stream in self.stack_files])
        self.stack = None

    def wait_ready(self, page):
        from playwright.sync_api import TimeoutError
        while True:
            now = time.monotonic()
            self.finish_stack()
            if now >= self.deadline:
                self.trace.mark("StartupObservationDeadline", ElapsedSeconds=now - self.start,
                                GameScreenPending=self.game_pending(), SixInitialGETs=self.state_outcomes(),
                                NormalClickInvoked=False)
                raise RuntimeError("Actual ready launcher not observed within the separate60s startup budget")
            if not self.stack_attempted and now >= self.start + 30 and self.game_pending():
                self.start_stack()
            slice_end = self.deadline
            if not self.stack_attempted and now < self.start + 30:
                slice_end = min(slice_end, self.start + 30)
            if self.stack is not None:
                slice_end = min(slice_end, self.stack_started + 15)
            try:
                page.wait_for_function(READY_LAUNCHER, timeout=max(1, (slice_end - now) * 1000), polling=100)
                state = self.state_outcomes()
                if all(s["BrowserTerminal"] is not None and s["ServerStatus"] is not None for s in state):
                    break
                # Dispatch already completed network callbacks within the same finite startup budget.
                page.wait_for_timeout(max(1, min(100, (self.deadline - time.monotonic()) * 1000)))
            except TimeoutError:
                self.trace.mark("StartupObservationSliceElapsed", ElapsedSeconds=time.monotonic() - self.start,
                                NormalClickInvoked=False)
        self.finish_stack(wait=True)
        self.trace.mark("SixInitialGETOutcomes", Outcomes=state)
        if any(s["BrowserTerminal"] != "Network.loadingFinished" or s["BrowserStatus"] != 200 or s["ServerStatus"] != 200 for s in state):
            raise RuntimeError("Initial state GET did not complete successfully")
        if time.monotonic() > self.deadline:
            raise RuntimeError("Actual ready launcher proof exceeded the separate60s startup budget")
        result = {"ReadyLauncher": True, "ContinueVisible": True, "ContinueEnabled": True,
                  "StartupElapsedSeconds": time.monotonic() - self.start, "SixInitialGETs": state,
                  "ManagedStackReportAttempted": self.stack_attempted, "ClickMilliseconds": 12000}
        self.trace.mark("ActualReadyLauncher", Observation=result)
        return result

    def close(self):
        self.finish_stack(wait=True)
