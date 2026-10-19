"""Exact-source lifecycle fault proofs. All process/handle callbacks are inert fakes."""
from pathlib import Path
import ast
import hashlib
import json
import types

HERE = Path(__file__).resolve().parent

def functions(path, names, env):
    tree = ast.parse(path.read_text())
    nodes = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in names]
    assert {n.name for n in nodes} == set(names)
    exec(compile(ast.fix_missing_locations(ast.Module(body=nodes, type_ignores=[])), str(path), 'exec'), env)
    return env

def main():
    source = HERE / 'watchdog.py'
    tree = ast.parse(source.read_text())
    main_node = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'main')
    outer = next(n for n in main_node.body if isinstance(n, ast.Try))
    branch = next(n for n in outer.body if isinstance(n, ast.If) and isinstance(n.test, ast.Name) and n.test.id == 'cancellation')
    launch = branch.orelse
    first = next(i for i,n in enumerate(launch) if isinstance(n, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'child' for t in n.targets))
    last = next(i for i,n in enumerate(launch) if isinstance(n, ast.While))
    def code(nodes):
        return compile(ast.fix_missing_locations(ast.Module(body=nodes, type_ignores=[])), str(source), 'exec')
    launch_code, cleanup_code = code(launch[first:last]), code(outer.finalbody)
    observers = functions(HERE.parent/'l01-managed-stack-s01/smoke.py', ['close_observed','observe_collector'], {'json':json})
    finalize = functions(HERE.parent/'t148-m01-run-phase-20261018.py', ['finalize'], {})['finalize']
    class FakeFile:
        def __truediv__(self, key): return self
        def read_text(self): return ''
    class FakeProc:
        def __init__(self, path): self.path = path
        def resolve(self, strict): return '/createdump'
        def read_bytes(self): return b'createdump\0target\0dump\0'
    class FakeChild:
        pid = 4242
        def __init__(self, native): self.native = native; self.waits = []; self.signals = []
        def poll(self): return self.native
        def kill(self): self.signals.append('direct-child'); self.native = -9
        def wait(self, timeout):
            self.waits.append(timeout)
            if self.native is None: raise TimeoutError('fake own child remains live')
            return self.native
    results = []
    for case in ('pidfd-setup-failure','natural7-before-finally','forced-cleanup','known7-observer-error','late-createdump-after-join'):
        child = FakeChild(7 if case in ('natural7-before-finally','known7-observer-error','late-createdump-after-join') else None)
        launches = []; censuses = []; dumps = {}; dump_terminal = []
        record = dict(nativeExit=None, observerErrors=[], collector=None, collectorLaunches=0, diagnosticRequests=0, apiOutcome='UNREACHED')
        def popen(*a, **k): launches.append('Popen'); return child
        def pidfd(pid): raise OSError('injected pidfd setup failure')
        def send(fd, sig):
            if fd == 4: dump_terminal.append(fd)
            else: child.signals.append('pidfd'); child.native = -sig
        def select(read, write, error, timeout): return ([fd for fd in read if fd in dump_terminal or fd == 3 and child.native is not None], [], [])
        def census():
            censuses.append('exact-target-only')
            if case == 'late-createdump-after-join' and len(censuses) == 2:
                assert record['collector']['joined']
                dumps[6060] = dict(identity=dict(pid=6060,parentPid=100),command=['createdump','target','dump'],fd=4)
        env = dict(child=None, collector_fd=None, out=None, err=None, dumps=dumps, record=record, command=['inert'], root=FakeFile(), env={}, started=0,
            time=types.SimpleNamespace(monotonic=lambda:100.0), os=types.SimpleNamespace(pidfd_open=pidfd,getpid=lambda:100,close=lambda fd:None),
            subprocess=types.SimpleNamespace(Popen=popen,DEVNULL=-3), select=types.SimpleNamespace(select=select),
            signal=types.SimpleNamespace(SIGKILL=9,pidfd_send_signal=send), observe_children=census,Path=FakeProc,cfg=dict(Createdump='/createdump'),
            lib={'identity':lambda p:dict(pid=p,parentPid=100),'close_observed':observers['close_observed'],'observe_collector':observers['observe_collector']})
        if case == 'pidfd-setup-failure':
            try: exec(launch_code, env)
            except BaseException as e: record['observerErrors'].append(repr(e))
        else:
            env.update(child=child, collector_fd=3)
            record.update(collectorLaunches=1, collector=dict(nativeExit=None,joined=False))
            record['observerErrors'].append('injected observer error before next poll')
            if case == 'known7-observer-error': record['nativeExit'] = record['collector']['nativeExit'] = 7
        exec(cleanup_code, env)
        returned = finalize(record, lambda r:None, lambda r:None)
        common = record['collectorLaunches'] == 1 and record['collector'] is not None and record['collector']['joined'] and len(censuses) == 2 and all(0 < x <= 5 for x in child.waits)
        if case in ('natural7-before-finally','known7-observer-error','late-createdump-after-join'):
            passed = common and returned == record['nativeExit'] == record['collector']['nativeExit'] == 7 and not child.signals
            if case == 'late-createdump-after-join': passed = passed and dump_terminal == [4] and record['ownedCleanupSignals'] == ['late-createdump']
        else:
            passed = common and record['nativeExit'] is None and record['collector']['cleanupExit'] == -9 and len(child.signals) == 1 and returned == 2
        results.append(dict(case=case,passed=passed,record=record,simulatedLaunches=len(launches),simulatedSignals=child.signals,waits=child.waits,finalizerExit=returned))
    result = dict(kind='G01_EXACT_SOURCE_FAKE_LIFECYCLE',sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),cases=results,
        passed=sum(r['passed'] for r in results),nativeProcesses=0,gameCases=0,apiRequests=0)
    print(json.dumps(result,indent=2))
    return 0 if result['passed'] == len(results) else 1

if __name__ == '__main__': raise SystemExit(main())
