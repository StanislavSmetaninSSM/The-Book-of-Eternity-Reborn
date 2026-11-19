"""Pure/temporary-file closed operational settings tests; no game or old-world writes."""
import hashlib,importlib.util,json,pathlib,tempfile,unittest
HERE=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('closed_rebind',HERE/'rebind-cold-settings.py');actual=importlib.util.module_from_spec(spec);spec.loader.exec_module(actual)

def config():
    p=pathlib.Path('/workspace/shared/relay-alternative/mortal-t034/cold-load-admission/mm30-outcome.json')
    raw=p.read_bytes();assert hashlib.sha256(raw).hexdigest()=='3c77a0e263ba6463397839bfa3e6fd1aad83b631b869909464623e7274b265e5'
    c=pathlib.Path('/workspace/shared/mm30-cold-load-20261119/play/game_session/config.json');data=(p.parent/'mm30-accepted-config.json').read_bytes();assert hashlib.sha256(data).hexdigest()==json.loads(raw)['Artifacts'][str(c)];return data

class ClosedRebindData(unittest.TestCase):
    def test_actual_three_field_patch_constructs_only_new_root_command(self):
        f=getattr(actual,'build_closed_settings_patch',None);self.assertTrue(callable(f),'missing operational patch')
        before=config();h=hashlib.sha256(before).hexdigest()
        with tempfile.TemporaryDirectory() as t:
            root=pathlib.Path(t);phase=root/'recovered-live';pipe='og-abcdef123456'
            patch=f(before,h,root,phase,pipe)
            old=json.loads(before);new=json.loads(patch);changed={k for k in old.keys()|new.keys() if old.get(k)!=new.get(k)}
            self.assertEqual(changed,{'GmCliLaunchCommand','GmBridgeShellWorkingDirectory','GmBridgePipeNameOverride'})
            self.assertEqual(new['GmBridgeShellWorkingDirectory'],str(root/'play/game_session'));self.assertEqual(new['GmBridgePipeNameOverride'],pipe)
            self.assertIn(str(phase/'relay/relay_cli.py'),new['GmCliLaunchCommand']);self.assertIn(str(phase/'queue'),new['GmCliLaunchCommand'])
            self.assertNotIn('/workspace/shared/mm28-',patch.decode());self.assertEqual(before,config())
            for digest,target,name in [('0'*64,phase,pipe),(h,root.parent/'foreign',pipe),(h,phase,'bad pipe'),(h,root/'recovered-live/../recovered-live',pipe)]:
                with self.assertRaises(ValueError):f(before,digest,root,target,name)
            duplicate=before.decode('utf-8-sig').replace('{','{"gmclilaunchcommand":"foreign",',1).encode()
            with self.assertRaises(ValueError):f(duplicate,hashlib.sha256(duplicate).hexdigest(),root,phase,pipe)

    def test_actual_closed_gate_and_atomic_write_preserve_all_other_data(self):
        gate=getattr(actual,'require_closed_rebind',None);write=getattr(actual,'write_closed_settings_patch',None)
        self.assertTrue(callable(gate) and callable(write),'missing closed gate/atomic writer')
        before=config();h=hashlib.sha256(before).hexdigest()
        guardian={'echild':True,'driverExitCode':0,'emergencySignals':0,'failures':0,'deadline':False}
        probe={'Success':True,'Failure':None,'CleanupFailures':[],'EOF':True,'ChildRestoredConfiguredBaseline':True,'OwnerRestoredOriginal':True,'TerminalNormalClose':True,'TerminalExitCode':0,'ChildExitCode':0}
        with tempfile.TemporaryDirectory() as t:
            root=pathlib.Path(t);session=root/'play/game_session';session.mkdir(parents=True);p=session/'config.json';p.write_bytes(before);sentinel=session/'untouched.json';sentinel.write_bytes(b'{"data":1}')
            phase=root/'recovered-live';gate(root,phase,guardian,probe)
            for badg,badp in [(dict(guardian,driverExitCode=1),probe),(guardian,dict(probe,EOF=False))]:
                with self.assertRaises(ValueError):gate(root,phase,badg,badp)
            for rel in ['.boe_runtime/gm-runs/main.json','game_session/game_state/control/gm_bridge_status.json','game_session/input/turn_request.json','game_session/game_state/control/pending_turn_snapshot.json','game_session/game_state/control/pending_turn_snapshot.authority.json','game_session/game_state/control/validation_repair_request.json']:
                x=root/'play'/rel;x.parent.mkdir(parents=True,exist_ok=True);x.write_text('{}')
                with self.assertRaises(ValueError):gate(root,phase,guardian,probe)
                x.unlink()
            phase.mkdir()
            with self.assertRaises(ValueError):gate(root,phase,guardian,probe)
            phase.rmdir()
            patch=actual.build_closed_settings_patch(before,h,root,phase,'og-abcdef123456')
            with self.assertRaises(ValueError):write(p,'0'*64,patch)
            self.assertEqual(p.read_bytes(),before);write(p,h,patch);self.assertEqual(p.read_bytes(),patch);self.assertEqual(sentinel.read_bytes(),b'{"data":1}')
            with self.assertRaises(ValueError):write(p,h,patch)

if __name__=='__main__':unittest.main()
