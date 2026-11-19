"""T028 cheap authored inputs, no game, compiler, export or authority."""
import hashlib, importlib.util, json, tempfile, unittest
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

    def test_path_alias_and_traversal_refuse(self):
        for path in ('../output','/etc/passwd','x/../input.cs','./input.cs','x//input.cs','x\\input.cs'):
            with self.subTest(path=path),self.assertRaises(ValueError):prep.safe_member(self.source,path)
    def test_unsafe_modes_refuse(self):
        path=self.source/'input.cs';path.chmod(0o666)
        with self.assertRaisesRegex(ValueError,'mode'):prep.inventory(self.source)
        with self.assertRaisesRegex(ValueError,'mode'):self.admission()
    def test_exact_stage_order_cannot_itself_issue_prepared(self):
        calls=[]
        def run(stage):
            calls.append(stage)
            return {'exitCode':0,'settled':True,'stdoutEof':True,'stderrEof':True}
        self.assertEqual(prep.prepare_stages(run),{'prepared':False,'failedStage':None})
        self.assertEqual(calls,['native','publish-client','publish-bridge','publish-helper','export','deploy','verify'])
        self.assertFalse(self.dest.exists())
    def test_each_stage_failure_and_incomplete_log_fence_stops(self):
        for failed in prep.STAGES:
            for fault in ({'exitCode':1},{'stdoutEof':False},{'stderrEof':False}):
                calls=[]
                def run(stage):
                    calls.append(stage);result={'exitCode':0,'settled':True,'stdoutEof':True,'stderrEof':True}
                    if stage==failed:result.update(fault)
                    return result
                with self.subTest(stage=failed,fault=fault):
                    self.assertEqual(prep.prepare_stages(run),{'prepared':False,'failedStage':failed})
                    self.assertEqual(calls,list(prep.STAGES[:prep.STAGES.index(failed)+1]))
                    self.assertFalse(self.dest.exists())
    def test_native_source_binary_package_join(self):
        native=self.root/'native';native.mkdir(mode=0o700);assets=[]
        for binary,source in (('boe-lineage-supervisor','native/linux/boe-lineage-supervisor.c'),('host-guardian','tests/fixtures/LinuxHost/host-guardian.c')):
            path=self.source/source;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(source.encode())
            target=native/binary;target.write_bytes(binary.encode());target.chmod(0o700)
            assets.append({'source':source,'binary':binary,'sourceSha256':prep.digest(path),'binarySha256':prep.digest(target)})
        package={'sourceCommit':'a'*40,'sourceSha256':assets[0]['sourceSha256'],'binarySha256':assets[0]['binarySha256'],
                 'runtimeIdentifier':'linux-x64','guarantee':'ordinary-same-namespace-lineage'}
        (native/'build-provenance.json').write_text(json.dumps({'assets':assets}))
        (native/'package-manifest.json').write_text(json.dumps(package))
        self.assertEqual(len(prep.native_join(self.source,native,'a'*40)),4)
        for field in ('sourceCommit','sourceSha256','binarySha256'):
            altered=dict(package);altered[field]='f'*40
            (native/'package-manifest.json').write_text(json.dumps(altered))
            with self.subTest(field=field),self.assertRaises(ValueError):prep.native_join(self.source,native,'a'*40)
        (native/'package-manifest.json').write_text(json.dumps(package));(native/'host-guardian').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'hash join'):prep.native_join(self.source,native,'a'*40)
    def test_unexpected_stage_exception_never_finishes_or_creates_manifest(self):
        calls=[]
        def run(stage):
            calls.append(stage);raise OSError('injected stage start failure')
        with self.assertRaisesRegex(OSError,'injected'):prep.prepare_stages(run)
        self.assertEqual(calls,['native']);self.assertFalse(self.dest.exists())

    def test_published_files_join_producer_and_native_not_selfpins(self):
        import shutil
        native=self.root/'native';native.mkdir();published=self.root/'published';published.mkdir()
        for name in ('boe-lineage-supervisor','package-manifest.json'):(native/name).write_bytes(name.encode())
        producer={'Client/bin/Debug/net8.0/Client.dll':hashlib.sha256(b'managed').hexdigest(),
                  'Client/bin/Debug/net8.0/Client.deps.json':hashlib.sha256(b'deps').hexdigest()}
        (published/'Client.dll').write_bytes(b'managed');(published/'Client.deps.json').write_bytes(b'deps')
        shipped=published/'runtimes/linux-x64/native';shipped.mkdir(parents=True)
        for name in ('boe-lineage-supervisor','package-manifest.json'):shutil.copyfile(native/name,shipped/name)
        self.assertEqual(len(prep.published_join(self.source,published,'Client',producer,native)),4)
        for path in (published/'Client.dll',published/'Client.deps.json',shipped/'boe-lineage-supervisor',shipped/'package-manifest.json'):
            original=path.read_bytes()
            for mutation in ('alter','missing'):
                if mutation=='alter':path.write_bytes(b'foreign')
                else:path.unlink()
                with self.subTest(path=path.name,mutation=mutation),self.assertRaises(ValueError):
                    prep.published_join(self.source,published,'Client',producer,native)
                path.write_bytes(original)
    def test_relay_copy_matches_original_source(self):
        import shutil
        copied=self.root/'copied';shutil.copytree(self.source,copied)
        prep.copied_source_join(self.source,copied)
        (copied/'input.cs').write_bytes(b'altered')
        with self.assertRaises(ValueError):prep.copied_source_join(self.source,copied)
        (copied/'input.cs').unlink()
        with self.assertRaises(ValueError):prep.copied_source_join(self.source,copied)

    def test_missing_malformed_or_lost_guardian_never_proves_settlement(self):
        valid={'echild':True,'driverExitCode':0,'emergencySignals':0,'failures':0,'reaped':1,'deadline':False}
        self.assertEqual(prep.guardian_report_result(0,valid),(True,True))
        for report in (None,{},dict(valid,reaped=True),dict(valid,echild=False),dict(valid,extra=1)):
            with self.subTest(report=report):self.assertEqual(prep.guardian_report_result(0,report),(False,False))
        self.assertEqual(prep.guardian_report_result(98,valid),(False,False))
    def test_settled_deadline_or_emergency_is_never_stage_success(self):
        valid={'echild':True,'driverExitCode':0,'emergencySignals':0,'failures':0,'reaped':1,'deadline':False}
        for change in ({'deadline':True},{'emergencySignals':1},{'driverExitCode':7}):
            with self.subTest(change=change):self.assertEqual(prep.guardian_report_result(0,dict(valid,**change)),(True,False))

    def test_guardian_compiler_alias_preserves_recorded_invocation(self):
        from unittest.mock import patch
        source=self.source/'tests/fixtures/LinuxHost/host-guardian.c'
        source.parent.mkdir(parents=True);source.write_bytes(b'guardian source')
        binary=self.root/'host-guardian';binary.write_bytes(b'guardian binary');binary.chmod(0o700)
        compiler=self.root/'gcc-real';compiler.write_bytes(b'compiler binary');compiler.chmod(0o700)
        alias=self.root/'cc';alias.symlink_to(compiler)
        provenance=self.root/'build-provenance.json'
        provenance.write_text(json.dumps({'compiler':str(alias),'compilerVersion':'cc recorded version','flags':['-O2'],
            'assets':[{'binary':'host-guardian','source':'tests/fixtures/LinuxHost/host-guardian.c',
                       'sourceSha256':prep.digest(source),'binarySha256':prep.digest(binary)}]}))
        descriptor={'binaryPath':str(binary),'binarySHA256':prep.digest(binary),'sourceSHA256':prep.digest(source),
                    'provenancePath':str(provenance),'provenanceSHA256':prep.digest(provenance),
                    'compilerPath':str(compiler),'compilerSHA256':prep.digest(compiler)}
        def version(argv,**kwargs):
            return 'cc recorded version' if argv==[str(alias),'--version'] else 'gcc-real different invocation banner'
        with patch.object(prep.subprocess,'check_output',side_effect=version) as invocation:
            self.assertEqual(prep.guardian_tool(self.source,descriptor),binary)
            invocation.assert_called_once_with([str(alias),'--version'],text=True,timeout=10)
        alias=self.root/'gcc-alias';alias.symlink_to(compiler)
        data=json.loads(provenance.read_text());data['compiler']=str(alias);data['compilerVersion']='gcc-alias recorded version'
        provenance.write_text(json.dumps(data));descriptor['provenanceSHA256']=prep.digest(provenance)
        with patch.object(prep.subprocess,'check_output',return_value='gcc-alias recorded version') as invocation:
            self.assertEqual(prep.guardian_tool(self.source,descriptor),binary)
            invocation.assert_called_once_with([str(alias),'--version'],text=True,timeout=10)
        foreign=self.root/'foreign-compiler';foreign.write_bytes(b'different compiler');foreign.chmod(0o700)
        descriptor['compilerPath']=str(foreign);descriptor['compilerSHA256']=prep.digest(foreign)
        with patch.object(prep.subprocess,'check_output') as invocation:
            with self.assertRaisesRegex(ValueError,'compiler provenance'):prep.guardian_tool(self.source,descriptor)
            invocation.assert_not_called()
        descriptor['compilerPath']=str(compiler)
        descriptor['compilerSHA256']='0'*64
        with patch.object(prep.subprocess,'check_output') as invocation:
            with self.assertRaisesRegex(ValueError,'compiler provenance'):prep.guardian_tool(self.source,descriptor)
            invocation.assert_not_called()

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(PreparationTests)
    if suite.countTestCases()!=20:raise SystemExit('Expected exactly twenty preparation DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==20 and result.wasSuccessful() and not result.skipped else 1)
