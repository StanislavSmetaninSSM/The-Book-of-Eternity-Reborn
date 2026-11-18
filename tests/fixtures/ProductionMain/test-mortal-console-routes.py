"""T024 pure route DATA tests, no terminal/GM/ownership fabrication."""
import ast, pathlib, unittest
import mortal_console_routes as console_routes
from mortal_console_routes import checked_route, storage_target, fresh_selection, require_local_unchanged, client_environment, selected_frame, item_position

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
        self.assertEqual(selected_frame(b'Menu\n> Alpha\n  Beta\r',0,'Menu'),'Alpha')
        for raw in (b'Menu\n> Alpha\nMenu\n',b'Menu\n> Alpha\n> Beta\n',
                    b'Menu\n\x1b[8m> Alpha\n',b'Menu\n> Alpha\x1b[',b'Menu\n> Alpha\n  Alpha\n',b'Menu\n> Alpha',b'Menu\n> Alpha\n',
                    b'Menu\n> Alpha\n(Move up and down to reveal more items)\r'):
            with self.subTest(raw=raw):self.assertIsNone(selected_frame(raw,0,'Menu'))
    def test_exact_item_position_cannot_accept_duplicate_or_missing(self):
        location=self.state();inventory={'items':[]}
        self.assertEqual(item_position(location,inventory,'st1','it1'),'storage')
        item=location['locationStorages'][0]['contents'].pop();inventory['items'].append(item)
        self.assertEqual(item_position(location,inventory,'st1','it1'),'inventory')
        location['locationStorages'][0]['contents'].append(item)
        with self.assertRaises(ValueError):item_position(location,inventory,'st1','it1')
        with self.assertRaises(ValueError):item_position(location,inventory,'st1','wrong')
    def test_actual_finite_selector_requires_new_frame_for_each_key(self):
        source=pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text()
        node=next(n for n in ast.parse(source).body if isinstance(n,ast.FunctionDef) and n.name=='finite_select')
        class Peer:
            def __init__(self,redraw=True):self.capture=bytearray(b'Menu\n> Alpha\n  Beta\r');self.keys=[];self.redraw=redraw
            def send(self,key,phase):
                self.keys.append(key)
                if key==b'\x1b[B' and self.redraw:self.capture.extend(b'Menu\n  Alpha\n> Beta\r')
        def until(predicate,*unused):
            if not predicate():raise TimeoutError('No fresh frame')
        peer=Peer();ns={'client':peer,'console_routes':console_routes,'until':until}
        exec(compile(ast.Module(body=[node],type_ignores=[]),'actual finite selector','exec'),ns)
        ns['finite_select']('Menu','Beta',0,2)
        self.assertEqual(peer.keys,[b'\x1b[B',b'\r'])
        peer=Peer(False);ns['client']=peer
        with self.assertRaises(TimeoutError):ns['finite_select']('Menu','Beta',0,2)
        self.assertEqual(peer.keys,[b'\x1b[B'])
    def test_actual_local_branch_is_separate_from_genuine_gm_send(self):
        tree=ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text())
        matches=[n for n in ast.walk(tree) if isinstance(n,ast.If) and
            any(isinstance(c,ast.Call) and isinstance(c.func,ast.Name) and c.func.id=='storage_local_route' for x in n.body for c in ast.walk(x))]
        self.assertEqual(len(matches),1)
        branch=matches[0]
        self.assertEqual(len(branch.body),1)
        self.assertIn('storage-local-proof',ast.unparse(branch.test))
        self.assertIn('one genuine game action',ast.unparse(ast.Module(body=branch.orelse,type_ignores=[])))
    def test_load_owner_is_installed_only_after_original_observation_validation(self):
        tree=ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text())
        fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='storage_local_route')
        text=ast.unparse(fn)
        self.assertLess(text.index('original = None'),text.index('one actual Load'))
        self.assertLess(text.index('validated_load_observation'),text.index('original_record = b'))
        self.assertIn("observed['observation']['ReplacementFinish']['MainIdentity']",text)
        self.assertNotIn("result['OriginalRunningRecord'] =",text)
        self.assertNotIn("result['AcceptedGameTurns'] =",text)
    def test_local_route_does_not_accept_story_or_queue_change(self):
        before={'StorySHA256':'a','HistorySHA256':'b','QueuePins':{}}
        require_local_unchanged(before,dict(before))
        for field in before:
            after=dict(before);after[field]='changed'
            with self.subTest(field=field),self.assertRaises(ValueError):require_local_unchanged(before,after)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(RouteTests)
    if suite.countTestCases()!=12:raise SystemExit('Expected exactly twelve finite-route DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==12 and result.wasSuccessful() and not result.skipped else 1)
