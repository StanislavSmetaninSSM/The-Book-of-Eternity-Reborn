"""T024 pure route DATA tests, no terminal/GM/ownership fabrication."""
import unittest
from mortal_console_routes import checked_route, storage_target, fresh_selection, require_local_unchanged, client_environment, selected_frame

class RouteTests(unittest.TestCase):
    def state(self):
        return {'locationId':'loc1','locationStorages':[{'storageId':'st1','name':'Council store',
            'hasFullAccess':True,'contents':[{'itemId':'it1','name':'Rope','quantity':1}]}]}
    def test_only_finite_routes(self):
        self.assertEqual(checked_route(None),{'Route':'birth'})
        self.assertEqual(checked_route({'Route':'storage-local-proof','StorageId':'st1','ItemId':'it1'})['ItemId'],'it1')
        for bad in ({'Route':'script','Keys':['Enter']},{'Route':'birth','Keys':[]},
                    {'Route':'storage-local-proof','StorageId':'','ItemId':'it1'}):
            with self.subTest(bad=bad),self.assertRaises(ValueError):checked_route(bad)
    def test_exact_canonical_target_and_current_label(self):
        t=storage_target(self.state(),'st1','it1',True)
        self.assertEqual(t['StorageLabel'],'📦 Council store (1 пр.) → управление')
        self.assertEqual(t['ItemLabel'],'Rope')
        with self.assertRaises(ValueError):storage_target(self.state(),'foreign','it1',True)
        with self.assertRaises(ValueError):storage_target(self.state(),'st1','foreign',True)
    def test_duplicate_labels_and_ids_refuse(self):
        state=self.state();state['locationStorages'].append(dict(state['locationStorages'][0],storageId='st2'))
        with self.assertRaises(ValueError):storage_target(state,'st1','it1',True)
        state=self.state();state['locationStorages'][0]['contents'].append({'itemId':'it2','name':'Rope'})
        with self.assertRaises(ValueError):storage_target(state,'st1','it1',True)
    def test_permission_is_observed_not_assumed(self):
        state=self.state();state['locationStorages'][0]['hasFullAccess']=False
        self.assertIn('🔒',storage_target(state,'st1','it1',False)['StorageLabel'])
        with self.assertRaises(ValueError):storage_target(state,'st1','it1',True)
    def test_fresh_selection_exact_rows_not_echo_or_substring(self):
        old='Inventory\n> Other\n  Rope\n'
        new='Inventory\n  Other\n> Rope\n'
        self.assertTrue(fresh_selection((old+new).encode(),len(old),'Inventory','Rope'))
        self.assertFalse(fresh_selection(old.encode(),len(old),'Inventory','Rope'))
        for bad in ('Inventory\n> Rope extra\n','Rope\n','Inventory\n> Rope\n  Rope\n',
                    'Inventory\n> Rope\nInventory\n> Other\n  Rope\n'):
            self.assertFalse(fresh_selection(bad.encode(),0,'Inventory','Rope'))
    def test_client_environment_does_not_change_other_peers(self):
        original={'TERM':'dumb','NONCE':'retained'}
        client=client_environment(original)
        self.assertEqual(client,{'TERM':'xterm-256color','NONCE':'retained'})
        self.assertEqual(original,{'TERM':'dumb','NONCE':'retained'})
    def test_selected_frame_never_uses_old_or_ambiguous_selection(self):
        self.assertEqual(selected_frame(b'Menu\n> Alpha\n  Beta\n',0,'Menu'),'Alpha')
        for raw in (b'Menu\n> Alpha\nMenu\n',b'Menu\n> Alpha\n> Beta\n',
                    b'Menu\n\x1b[8m> Alpha\n',b'Menu\n> Alpha\x1b[',b'Menu\n> Alpha\n  Alpha\n'):
            with self.subTest(raw=raw):self.assertIsNone(selected_frame(raw,0,'Menu'))
    def test_local_route_does_not_accept_story_or_queue_change(self):
        before={'StorySHA256':'a','HistorySHA256':'b','QueuePins':{}}
        require_local_unchanged(before,dict(before))
        for field in before:
            after=dict(before);after[field]='changed'
            with self.subTest(field=field),self.assertRaises(ValueError):require_local_unchanged(before,after)

if __name__=='__main__':unittest.main()
