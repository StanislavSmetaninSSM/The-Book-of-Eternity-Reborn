#!/usr/bin/env python3
"""Strict detached outcome parser controls, not actual publication or ownership proof."""
import copy, importlib.util, pathlib, unittest
spec=importlib.util.spec_from_file_location('evidence',pathlib.Path(__file__).with_name('relay_phase_evidence.py'))
e=importlib.util.module_from_spec(spec);spec.loader.exec_module(e)

class OutcomeTests(unittest.TestCase):
    def setUp(self):
        self.request=dict(sessionId='s',requestId='r',turnNumber=42)
        self.value=dict(valid=True,publicationAuthority=False,kind='NotRequired',sessionId='s',requestId='r',turn=42,
            snapshotToken='A'*64,priorCount=0,currentCount=0,historyBefore=dict(present=False,sha256=None),
            historyAfter=dict(present=False,sha256=None),applicability=dict(originalMinute=480,resultMinute=500,
                lastWorldMinute=480,lastFactionMinute=480,worldCycleMinutes=240,factionCycleMinutes=1440))
    def test_absent_and_present_unchanged(self):
        self.assertEqual(e.checked_mortal_outcome(self.value,self.request),self.value)
        self.value.update(priorCount=1,currentCount=1,historyBefore=dict(present=True,sha256='a'*64),historyAfter=dict(present=True,sha256='a'*64))
        self.assertEqual(e.checked_mortal_outcome(self.value,self.request),self.value)
    def test_append_is_distinct(self):
        v=self.value;v.pop('applicability');v.update(kind='ReceiptAppended',currentCount=1,historyAfter=dict(present=True,sha256='b'*64),declarationFingerprint='sha256:'+'c'*64,receiptFingerprint='sha256:'+'d'*64)
        self.assertEqual(e.checked_mortal_outcome(v,self.request),v)
    def test_mixed_missing_and_false_branches_refuse(self):
        for change in [dict(kind='unknown'),dict(receiptFingerprint='sha256:'+'a'*64),dict(currentCount=1),dict(turn=True),dict(publicationAuthority=True),dict(snapshotToken='')]:
            with self.subTest(change=change),self.assertRaises(ValueError):e.checked_mortal_outcome(dict(self.value,**change),self.request)
    def test_due_pending_backwards_and_bool_refuse(self):
        for change in [dict(resultMinute=720),dict(lastWorldMinute=0),dict(resultMinute=479),dict(worldCycleMinutes=True),dict(worldCycleMinutes=0)]:
            v=copy.deepcopy(self.value);v['applicability'].update(change)
            with self.subTest(change=change),self.assertRaises(ValueError):e.checked_mortal_outcome(v,self.request)
    def test_changed_or_null_history_refuses(self):
        for h in [None,dict(present=False,sha256='a'*64),dict(present=True,sha256=None),dict(present=0,sha256=None),dict(present=True,sha256='a'*64)]:
            with self.subTest(history=h),self.assertRaises(ValueError):e.checked_mortal_outcome(dict(self.value,historyAfter=h),self.request)
    def test_final_pin_join_including_absence(self):
        p='game_state/world/mortal_living_world_history.json'
        self.assertTrue(e.history_witness_matches_pins(self.value['historyAfter'],{}))
        self.assertFalse(e.history_witness_matches_pins(self.value['historyAfter'],{p:'a'*64}))
        self.assertTrue(e.history_witness_matches_pins(dict(present=True,sha256='a'*64),{p:'a'*64}))
        self.assertFalse(e.history_witness_matches_pins(dict(present=True,sha256='a'*64),{p:'b'*64}))
    def test_optional_bytes_preserve_empty_and_bom(self):
        self.assertNotEqual(e.optional_history_witness(None),e.optional_history_witness(b''))
        self.assertNotEqual(e.optional_history_witness(b'{}'),e.optional_history_witness(b'\xef\xbb\xbf{}'))

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(OutcomeTests)
    assert suite.countTestCases()==7
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==7 and not result.skipped else 1)
