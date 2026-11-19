"""Pure authored DATA for the closure-only post-return observation; never IPC."""
import ast
import copy
import importlib.util
import json
import hashlib
import os
from pathlib import Path
import unittest
from types import SimpleNamespace
spec=importlib.util.spec_from_file_location('phase',Path(__file__).with_name('relay_phase_evidence.py'))
phase=importlib.util.module_from_spec(spec);spec.loader.exec_module(phase)

class ReturnedObservation(unittest.TestCase):
 def test_exact_fresh_complete_original_return_only(self):
  identity=dict(rootKey='/owned',runId='1'*32,generationId='2'*32,epoch=1,backend=2,hostInstanceId='3'*32,bootId='boot')
  close=dict(identity=identity,pinId='4'*32,closeId='5'*32,operationId='6'*32,outcome=0,closingFailed=False)
  expected=dict(nonce='7'*32,processId=123,daemonGuardOwnerId='8'*32,requestSHA256='a'*64,turnIdentity=dict(sessionId='session',requestId='request',turnNumber=42),mainIdentity=identity)
  event={k:copy.deepcopy(v) for k,v in expected.items() if k!='mainIdentity'}
  event.update(stage='daemon-turn-consumer-returned',requestAdmitted=True,consumerReturned=True,originalClose=copy.deepcopy(close),terminalClose=copy.deepcopy(close),closeObserved=True,lost=False,publicationUncertain=False,helperExitCode=0,replyReadJoined=True,stderrReadJoined=True,handledError=False,errorCountBefore=0,errorCountAfter=0)
  line=lambda e: b'BOE_MAIN_OPERATION_PS '+json.dumps(e,separators=(',',':')).encode()+b'\n'
  raw=line(event)
  # Absence is the initial feature RED, not import failure or fabricated authority.
  # Until implemented this assertion fails; all following matrix subcases are UNEXECUTED.
  self.assertTrue(callable(getattr(phase,'daemon_consumer_returned',None)), 'Missing pure correlated post-return predicate')
  check=lambda value:phase.daemon_consumer_returned(value,expected)
  self.assertEqual(check(raw),event)
  self.assertIsNone(check(raw[:-1]));self.assertIsNone(check(raw+raw))
  self.assertIsNone(check(raw+raw[:40]))
  for n in range(1,len(b'BOE_MAIN_OPERATION_PS ')+1):self.assertIsNone(check(raw+raw[:n]))
  for key in ('nonce','processId','daemonGuardOwnerId','requestSHA256','turnIdentity'):
   bad=copy.deepcopy(event);bad[key]='wrong';self.assertIsNone(check(line(bad)),key)
  for key,value in [('requestAdmitted',False),('consumerReturned',False),('closeObserved',False),('lost',True),('publicationUncertain',True),('helperExitCode',1),('helperExitCode',False),('replyReadJoined',False),('stderrReadJoined',False),('handledError',True),('errorCountAfter',1)]:
   bad=copy.deepcopy(event);bad[key]=value;self.assertIsNone(check(line(bad)),key)
  for field,value in [('outcome',1),('closingFailed',True),('pinId','9'*32),('identity',dict(identity,runId='9'*32))]:
   bad=copy.deepcopy(event);bad['terminalClose'][field]=value;self.assertIsNone(check(line(bad)),field)
  for key in ('originalClose','terminalClose'):
   for field,value in [('outcome',False),('closingFailed',0)]:
    bad=copy.deepcopy(event);bad[key][field]=value;self.assertIsNone(check(line(bad)))
   for field,value in [('epoch',True),('backend',2.0)]:
    bad=copy.deepcopy(event);bad[key]['identity'][field]=value;self.assertIsNone(check(line(bad)))
  self.assertIsNone(check(b'')) # caller must pass only fresh bytes after its actual action cut
  directory=Path(os.environ['BOE_TEST_DIAGNOSTIC_ROOT'])
  actual=phase.daemon_consumer_returned((directory/'daemon-return-wire.jsonl').read_bytes(),json.loads((directory/'daemon-return-wire-expected.json').read_text()))
  self.assertIsNotNone(actual,'Actual PS serializer wire failed independent Python predicate')
  self.assertEqual(actual['requestSHA256'],'A'*64)

  evidence={'SessionPath':'/owned/game_session'}
  def status_pair(prefix,settled,count):
   status=dict(sessionPath=evidence['SessionPath'],daemonGuardOwnerId='8'*32,daemonGuardVersion=1,status='stopped' if settled else 'running',terminalSettled=settled,errorCount=count)
   guard=dict(version=1,sessionPath=evidence['SessionPath'],ownerId='8'*32,settled=settled)
   for name,value in [('Status',status),('Guard',guard)]:
    encoded=json.dumps(value).encode();evidence[prefix+name+'RawHex']=encoded.hex();evidence[prefix+name+'SHA256']=hashlib.sha256(encoded).hexdigest()
  status_pair('Original',False,0);status_pair('Final',True,0)
  self.assertTrue(phase.daemon_settlement_proved(evidence))
  for count in (1,True,None):
   status_pair('Final',True,count);self.assertFalse(phase.daemon_settlement_proved(evidence))
 def test_actual_shutdown_block_does_not_fall_through_unknown_return(self):
  source=Path(__file__).with_name('live-mortal-phase.py').read_text()
  tree=ast.parse(source)
  nodes=[n for n in ast.walk(tree) if isinstance(n,ast.If) and isinstance(n.test,ast.Compare) and ast.unparse(n.test)=='original is not None' and any(isinstance(x,ast.AugAssign) and isinstance(x.target,ast.Name) and x.target.id=='shutdown_attempts' for x in ast.walk(n))]
  self.assertEqual(len(nodes),1)
  calls=[]
  def rpc(*args):calls.append(args);raise RuntimeError('Unexpected shutdown without return witness')
  def refused(*args,**kwargs):raise RuntimeError('Consumer return UNKNOWN')
  scope=dict(original={'rootKey':'/owned'},inventory_cleanup_required=False,result={},shutdown_attempts=0,rpc=rpc,wait_daemon_consumer_returned=refused)
  exec(compile(ast.fix_missing_locations(ast.Module(body=nodes,type_ignores=[])),'actual-shutdown-block','exec'),scope)
  self.assertEqual(scope['shutdown_attempts'],0)
  self.assertEqual(calls,[])
  def admitted():scope['result']['DaemonConsumerReturned']={'fixture':'observed'}
  scope['wait_daemon_consumer_returned']=admitted
  exec(compile(ast.fix_missing_locations(ast.Module(body=nodes,type_ignores=[])),'actual-shutdown-block','exec'),scope)
  self.assertEqual(scope['shutdown_attempts'],1)
  self.assertEqual(len(calls),1) # sentinel RPC proves gating, not a real shutdown receipt
  waits=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='wait_daemon_consumer_returned']
  self.assertEqual(len(waits),1)
  clock=[0]
  def late(*args):clock[0]=11;return {'fixture':'late'}
  request=dict(sessionId='session',requestId='request',turnNumber=42)
  state=dict(ActualRequest=request,ActualRequestSHA256='a'*64,DaemonSettlement={'OriginalStatusRawHex':json.dumps({'pid':123}).encode().hex()})
  wait_scope=dict(route={'Route':'birth'},result=state,daemon_return_cut=0,daemon=SimpleNamespace(capture=b'raw'),daemon_owner='8'*32,original={},env={'BOE_MAIN_STOP_DIAGNOSTIC_NONCE':'7'*32},start=0,client_cleanup_seconds=10,time=SimpleNamespace(monotonic=lambda:clock[0]),phase_evidence=SimpleNamespace(daemon_consumer_returned=late),json=json)
  exec(compile(ast.fix_missing_locations(ast.Module(body=waits,type_ignores=[])),'actual-consumer-wait','exec'),wait_scope)
  with self.assertRaises(TimeoutError):wait_scope['wait_daemon_consumer_returned']()
  self.assertNotIn('DaemonConsumerReturned',state)
  wait_scope['route']={'Route':'storage-local-proof'}
  wait_scope['wait_daemon_consumer_returned']() # no fictional turn required; no predicate/pump


if __name__=='__main__':unittest.main()
