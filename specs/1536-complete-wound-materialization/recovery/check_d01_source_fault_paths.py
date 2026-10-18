"""Read-only AST-extracted Python fault proof; no fixture/native process launch."""
import ast,contextlib,hashlib,io,json
from pathlib import Path
R=Path('/workspace/The-Book-of-Eternity-Reborn');V=Path('/tmp/1536-cloud-tools/review');D=R/'specs/1536-complete-wound-materialization/recovery'
source=(D/'l01-delivery-d01/phase.py').read_bytes();(V/'d01-phase-original-source-0db1f418.py').write_bytes(source)
module=ast.parse(source);main=next(x for x in module.body if isinstance(x,ast.FunctionDef) and x.name=='main');ret=next(x for x in main.body if isinstance(x,ast.Return));emitter=ret.value.args[2]
finalsource=(D/'t148-m01-run-phase-20261018.py').read_text();fm=ast.parse(finalsource);fn=next(x for x in fm.body if isinstance(x,ast.FunctionDef) and x.name=='finalize');ns={};exec(compile(ast.Module(body=[fn],type_ignores=[]),'<exact-finalize>','exec'),ns)
class BufferThenFail:
 def __init__(self):self.text='';self.flushes=0
 def write(self,text):self.text+=text;return len(text)
 def flush(self):self.flushes+=1;raise BrokenPipeError('fake shutdown flush failure')
rows=[]
for native in [0,7]:
 r={'status':'test','nativeExit':native,'observerErrors':[],'durationSeconds':1};sink=BufferThenFail();saved=[]
 with contextlib.redirect_stdout(sink):
  emit=eval(compile(ast.Expression(body=emitter),'<exact-phase-emitter>','eval'),{'json':json})
  result=ns['finalize'](r,lambda v:saved.append(json.loads(json.dumps(v))),emit)
 before=list(r['observerErrors']);outside=None
 try:sink.flush()
 except BrokenPipeError as e:outside=type(e).__name__
 rows.append({'native':native,'finalizeReturned':result,'observerErrorsBeforeShutdown':before,'pendingBufferedChars':len(sink.text),'subsequentFlushFailureOutsideFinalize':outside,'recordStillHasNative':r['nativeExit'],'provesUnobservedShutdownFailure':result==native and not before and outside=='BrokenPipeError'})
# The AST proof establishes the error is outside FINALIZE; it does not simulate a real interpreter exit code.
out={'kind':'D01_ORIGINAL_BUFFERED_EMITTER_FAKE_SOURCE_PROOF','sourceSha256':hashlib.sha256(source).hexdigest(),'source':'0db1f4186fd190c4940b018ec186cc9667727cfd','fixtureProcessesStarted':0,'helperOrDotnetExecuted':False,'proof':rows,'allAssertionsPassed':all(x['provesUnobservedShutdownFailure'] for x in rows),'limit':'No actual interpreter shutdown/native run; AST-exact emit and finalize prove deferred flush is outside guarded reporting. Python runtime shutdown exit-code behavior is a separate documented consequence.'}
p=V/'d01-source-buffered-emitter-proof-0db1f418.json';p.write_text(json.dumps(out,indent=2)+'\n');print(json.dumps({'proof':str(p),'sourceSha256':out['sourceSha256'],'passed':out['allAssertionsPassed']}))
