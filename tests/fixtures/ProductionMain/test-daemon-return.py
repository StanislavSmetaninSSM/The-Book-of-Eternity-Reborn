"""Pure authored DATA for the closure-only post-return observation; never IPC."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest
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
  self.assertTrue(callable(getattr(phase,'daemon_consumer_returned',None)), 'Missing pure correlated post-return predicate')
  check=lambda value:phase.daemon_consumer_returned(value,expected)
  self.assertEqual(check(raw),event)
  self.assertIsNone(check(raw[:-1]));self.assertIsNone(check(raw+raw))
  self.assertIsNone(check(raw+raw[:40]))
  for key in ('nonce','processId','daemonGuardOwnerId','requestSHA256','turnIdentity'):
   bad=copy.deepcopy(event);bad[key]='wrong';self.assertIsNone(check(line(bad)),key)
  for key,value in [('requestAdmitted',False),('consumerReturned',False),('closeObserved',False),('lost',True),('publicationUncertain',True),('helperExitCode',1),('helperExitCode',False),('replyReadJoined',False),('stderrReadJoined',False),('handledError',True),('errorCountAfter',1)]:
   bad=copy.deepcopy(event);bad[key]=value;self.assertIsNone(check(line(bad)),key)
  for field,value in [('outcome',1),('closingFailed',True),('pinId','9'*32),('identity',dict(identity,runId='9'*32))]:
   bad=copy.deepcopy(event);bad['terminalClose'][field]=value;self.assertIsNone(check(line(bad)),field)
  self.assertIsNone(check(b'')) # caller must pass only fresh bytes after its actual action cut
if __name__=='__main__':unittest.main()
