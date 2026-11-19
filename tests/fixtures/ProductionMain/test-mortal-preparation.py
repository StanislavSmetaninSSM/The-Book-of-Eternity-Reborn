"""T028 cheap authored inputs, no game, compiler, export or authority."""
import hashlib, importlib.util, tempfile, unittest
from pathlib import Path
from relay_phase_evidence import require_native_bootstrap_path_budget

spec=importlib.util.spec_from_file_location("prepare_mortal_live",Path(__file__).with_name("prepare-mortal-live.py"))
prep=importlib.util.module_from_spec(spec);spec.loader.exec_module(prep)

class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.source=self.root/'source';self.outputs=self.root/'outputs'
        self.source.mkdir(mode=0o700);self.outputs.mkdir(mode=0o700)
        (self.source/'input.cs').write_bytes(b'source');(self.outputs/'client.dll').write_bytes(b'output')
        self.sp={'input.cs':hashlib.sha256(b'source').hexdigest()}
        self.op={'client.dll':hashlib.sha256(b'output').hexdigest()}
        self.dest=self.root/'fresh'
    def admission(self):
        return prep.admit_inputs(self.dest,self.source,self.outputs,self.sp,self.op)
    def test_exact_fresh_input_admission_does_not_create_destination(self):
        result=self.admission()
        self.assertFalse(self.dest.exists());self.assertEqual(result,{'admitted':True,'reason':None})
    def test_existing_empty_destination_refused_unchanged(self):
        self.dest.mkdir(mode=0o700);before=self.dest.stat()
        result=self.admission()
        self.assertEqual(list(self.dest.iterdir()),[]);self.assertEqual(self.dest.stat().st_ino,before.st_ino)
        self.assertEqual(result,{'admitted':False,'reason':'destination-exists'})
    def test_source_and_output_drift_refused_before_destination(self):
        for path in (self.source/'input.cs',self.outputs/'client.dll'):
            with self.subTest(path=path.name):
                original=path.read_bytes();path.write_bytes(original+b'changed')
                result=self.admission();path.write_bytes(original)
                self.assertFalse(self.dest.exists());self.assertEqual(result,{'admitted':False,'reason':'input-drift'})
    def test_inventory_exact_size_hash_mode_and_relative_name(self):
        (self.source/'input.cs').chmod(0o600)
        self.assertEqual(prep.inventory(self.source),[{'path':'input.cs','size':6,'sha256':self.sp['input.cs'],'mode':0o600}])
    def test_inventory_link_not_followed(self):
        (self.source/'linked').symlink_to(self.outputs/'client.dll')
        with self.assertRaisesRegex(ValueError,'link'):prep.inventory(self.source)
    def test_injected_stage_failure_stops_before_publish_export_and_manifest(self):
        calls=[]
        def run(stage):
            calls.append(stage)
            return {'exitCode':2,'settled':True,'stdoutEof':True,'stderrEof':True}
        result=prep.prepare_stages(run)
        self.assertEqual(calls,['native']);self.assertEqual(result,{'prepared':False,'failedStage':'native'})
        self.assertFalse(self.dest.exists())
    def test_injected_unsettled_stage_never_advances_even_exit_zero(self):
        calls=[]
        def run(stage):
            calls.append(stage)
            return {'exitCode':0,'settled':False,'stdoutEof':False,'stderrEof':True}
        result=prep.prepare_stages(run)
        self.assertEqual(calls,['native']);self.assertEqual(result,{'prepared':False,'failedStage':'native'})
    def test_socket_budget_ascii_boundary(self):
        suffix=len('/boe-native-'+32*'0'+'/owner');root='/'+('a'*(107-suffix-1))
        self.assertEqual(require_native_bootstrap_path_budget(root),107)
        with self.assertRaises(ValueError):require_native_bootstrap_path_budget(root+'a')
    def test_socket_budget_utf8_boundary(self):
        suffix=len('/boe-native-'+32*'0'+'/owner');root='/'+('é'*20)+('a'*(107-suffix-41))
        self.assertEqual(require_native_bootstrap_path_budget(root),107)
        with self.assertRaises(ValueError):require_native_bootstrap_path_budget(root+'a')

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(PreparationTests)
    if suite.countTestCases()!=9:raise SystemExit('Expected exactly nine preparation DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==9 and result.wasSuccessful() and not result.skipped else 1)
