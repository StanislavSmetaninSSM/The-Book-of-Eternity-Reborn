"""Four actual inert guardian/child controls; never a compiler, export or game.

Requires an independently prepared BOE_PREPARATION_GUARDIAN_MANIFEST descriptor;
this test does not compile or self-approve its guardian bootstrap.
"""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('prepare_mortal_live',Path(__file__).with_name('prepare-mortal-live.py'))
prep=importlib.util.module_from_spec(spec);spec.loader.exec_module(prep)

class PreparationProcessTests(unittest.TestCase):
    def setUp(self):
        self.repo=Path(__file__).resolve().parents[3]
        descriptor=prep.strict_settings(Path(os.environ['BOE_PREPARATION_GUARDIAN_MANIFEST']).read_bytes())
        self.guardian=prep.guardian_tool(self.repo,descriptor)
        diagnostics=Path(os.environ['BOE_TEST_DIAGNOSTIC_ROOT'])/'mortal-preparation-process'
        diagnostics.mkdir(parents=True,exist_ok=True)
        self.root=Path(tempfile.mkdtemp(dir=diagnostics));self.root.chmod(0o700)
    def run_child(self,script,seconds=3):
        result=prep.run_owned([str(Path(sys.executable).resolve()),'-c',script],self.repo,self.root,'child',self.guardian,seconds)
        self.assertEqual(json.loads((self.root/'child.outcome.json').read_text()),result)
        self.assertTrue(result['settled'],str(result));self.assertTrue(result['stdoutEof']);self.assertTrue(result['stderrEof'])
        self.assertTrue(result['report']['echild']);self.assertEqual(result['report']['failures'],0)
        return result
    def test_normal_child_logs_and_exact_settlement(self):
        result=self.run_child("import sys;print('out',flush=True);print('err',file=sys.stderr,flush=True)")
        self.assertEqual(result['exitCode'],0);self.assertEqual(result['report']['driverExitCode'],0)
        self.assertEqual(result['report']['emergencySignals'],0);self.assertFalse(result['report']['deadline'])
        self.assertEqual((self.root/'child.stdout').read_bytes(),b'out\n');self.assertEqual((self.root/'child.stderr').read_bytes(),b'err\n')
    def test_nonzero_child_is_settled_failure(self):
        result=self.run_child('import sys;sys.exit(7)')
        self.assertEqual(result['exitCode'],-1);self.assertEqual(result['report']['driverExitCode'],7)
        self.assertEqual(result['report']['emergencySignals'],0);self.assertFalse(result['report']['deadline'])
    def test_timeout_is_settled_but_never_success(self):
        result=self.run_child('import time;time.sleep(30)',seconds=.2)
        self.assertEqual(result['exitCode'],-1);self.assertTrue(result['report']['deadline'])
        self.assertGreater(result['report']['emergencySignals'],0)
    def test_adopted_new_session_survivor_is_not_hidden_success(self):
        result=self.run_child('''import os,time
r,w=os.pipe()
pid=os.fork()
if pid==0:
    os.close(r);os.setsid();os.write(w,b'r');os.close(w)
    time.sleep(30)
else:
    os.close(w);assert os.read(r,1)==b'r';os.close(r)
''')
        self.assertEqual(result['exitCode'],-1);self.assertEqual(result['report']['driverExitCode'],0)
        self.assertGreaterEqual(result['report']['reaped'],2);self.assertGreater(result['report']['emergencySignals'],0)
        self.assertFalse(result['report']['deadline'])

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(PreparationProcessTests)
    if suite.countTestCases()!=4:raise SystemExit('Expected exactly four inert preparation-process methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==4 and result.wasSuccessful() and not result.skipped else 1)
