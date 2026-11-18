"""Source-only patcher controls. No PowerShell script or daemon is executed."""
import pathlib
import runpy
import unittest

HERE=pathlib.Path(__file__).resolve().parent
PATCH=runpy.run_path(str(HERE/'instrument-finally.py'))['instrument']
SOURCE=(HERE.parents[2]/'BookOfEternityClient/game_master_daemon.ps1').read_bytes()

class FinallyPatcherTests(unittest.TestCase):
    def test_actual_source_and_crlf_restore_exactly(self):
        for raw in (SOURCE,SOURCE.replace(b'\n',b'\r\n')):
            result, ledger=PATCH(raw)
            self.assertTrue(ledger['SourceRestorationByteExact'])
            self.assertEqual(len(ledger['Markers']),len(set(ledger['Markers'])))
            self.assertNotEqual(ledger['OriginalSHA256'],ledger['InstrumentedSHA256'])
            self.assertIn(b'$env:BOE_DAEMON_FINALLY_TRACE',result)
    def test_reinstrumentation_refused(self):
        with self.assertRaises(ValueError): PATCH(PATCH(SOURCE)[0])
    def test_missing_or_duplicated_anchor_refused(self):
        for raw in (SOURCE.replace(b'$daemonWorkSignal = $null',b'$daemonWorkSignal = $missing',1),
                    SOURCE+b'$daemonWorkSignal = $null\n'):
            with self.assertRaises(ValueError): PATCH(raw)
    def test_changed_final_suffix_refused(self):
        with self.assertRaises(ValueError): PATCH(SOURCE+b'\n')

if __name__=='__main__': unittest.main()
