"""Fake observer callbacks only: no build, test host, game, or fixture operations."""
from contextlib import contextmanager
from pathlib import Path
import ast
import copy
import hashlib
import itertools
import json
import runpy
import subprocess

root = Path(__file__).resolve().parents[3]
runner = Path(__file__).with_name('t148-m01-run-phase-20261018.py')
original_sha = '8b1b80f76603a11fd6280e20246262019da8b85f'
original = subprocess.check_output(['git', 'show', original_sha + ':' + str(runner.relative_to(root))], cwd=root).decode()
old_main = next(node for node in ast.parse(original).body if isinstance(node, ast.FunctionDef) and node.name == 'main')
old_try = next(node for node in old_main.body if isinstance(node, ast.Try))
# Execute the original final record-write/print/return statements themselves.
tail = old_try.finalbody[-1:] + old_main.body[old_main.body.index(old_try) + 1:]
fn = ast.FunctionDef(name='old_finalize', args=ast.arguments(posonlyargs=[], args=[ast.arg(arg='record'), ast.arg(arg='phase')], kwonlyargs=[], kw_defaults=[], defaults=[]), body=tail, decorator_list=[])
scope = {'json': json}
exec(compile(ast.fix_missing_locations(ast.Module(body=[fn], type_ignores=[])), '<original-finalization-tail>', 'exec'), scope)
new = runpy.run_path(str(runner))


class FakePhase:
    def __init__(self, callback):
        self.callback = callback

    def __truediv__(self, name):
        assert name == 'record.json'
        return self

    def write_text(self, value):
        self.callback(json.loads(value))


def callbacks(persist_fails, emit_fails):
    saved = []

    def persist(record):
        if persist_fails:
            raise OSError('fake record failure')
        saved.append(copy.deepcopy(record))

    def emit(record, **kwargs):
        if emit_fails:
            raise BrokenPipeError('fake summary failure')

    return saved, persist, emit


baseline = []
for native, persist_fails, emit_fails in itertools.product([0, 7], [False, True], [False, True]):
    saved, persist, emit = callbacks(persist_fails, emit_fails)
    scope['print'] = emit
    record = dict(nativeExit=native, observerErrors=[])
    try:
        status = scope['old_finalize'](record, FakePhase(persist))
        error = None
    except BaseException as exc:
        status, error = None, type(exc).__name__
    assert (error is not None) == (persist_fails or emit_fails)
    baseline.append(dict(nativeExit=native, persistFails=persist_fails, emitFails=emit_fails, returned=status, unhandled=error))

corrected = []
for native, close_fails, persist_fails, emit_fails in itertools.product([None, 0, 7], [False, True], [False, True], [False, True]):
    saved, persist, emit = callbacks(persist_fails, emit_fails)
    record = dict(nativeExit=None, observerErrors=[])
    calls = []

    @contextmanager
    def outputs():
        yield ('fake stdout', 'fake stderr')
        if close_fails:
            raise OSError('fake output close failure')

    def launch(out, err):
        calls.append((out, err))
        if native is None:
            raise OSError('fake pre-native launch failure')
        return native

    new['capture_native'](record, outputs, launch)
    status = new['finalize'](record, persist, emit)
    assert len(calls) == 1 and record['nativeExit'] == native
    failure = native is None or close_fails or persist_fails or emit_fails
    assert status == (7 if native == 7 else 2 if failure else 0)
    assert bool(record['observerErrors']) == failure
    if not persist_fails:
        assert saved[-1] == record
    corrected.append(dict(nativeExit=native, closeFails=close_fails, persistFails=persist_fails, emitFails=emit_fails,
                          returned=status, observerErrors=record['observerErrors'], nativeCalls=len(calls), savedRecords=len(saved)))

# Original output-close exception handler only recorded launchOrControllerError.
record = dict(nativeExit=0, observerErrors=[])
exec(compile(ast.fix_missing_locations(ast.Module(body=old_try.handlers[0].body, type_ignores=[])), '<original-output-handler>', 'exec'), {}, {'record': record, 'error': OSError('fake close failure')})
scope['print'] = lambda *args, **kwargs: None
assert scope['old_finalize'](record, FakePhase(lambda value: None)) == 0 and not record['observerErrors']
report = dict(kind='M01_CONTROLLER_FAKE_CALLBACK_PROOF', baselineSource=original_sha,
              baselineRunnerSha256=hashlib.sha256(original.encode()).hexdigest(),
              correctedRunnerSha256=hashlib.sha256(runner.read_bytes()).hexdigest(),
              baseline=baseline, originalCloseFailureIncorrectlyReturned=0,
              corrected=corrected, checkedCases=len(corrected), outcome='PASS',
              scope='Observer source harness only; zero game/test/build/native-phase launches; no M01/L01 runtime qualification.')
destination = Path(__file__).with_suffix('.json')
destination.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(dict(outcome='PASS', baselineCases=len(baseline), correctedCases=len(corrected), report=str(destination))))
