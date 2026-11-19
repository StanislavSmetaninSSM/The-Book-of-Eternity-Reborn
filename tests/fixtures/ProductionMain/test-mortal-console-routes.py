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
        self.assertEqual(selected_frame(b'Menu\n> Alpha\n  Beta\r',0,console_routes.fixed_menu('Menu',['Alpha','Beta'],20)),'Alpha')
        for raw in (b'Menu\n> Alpha\nMenu\n',b'Menu\n> Alpha\n> Beta\n',
                    b'Menu\n\x1b[8m> Alpha\n',b'Menu\n> Alpha\x1b[',b'Menu\n> Alpha\n  Alpha\n',b'Menu\n> Alpha',b'Menu\n> Alpha\n',
                    b'Menu\n> Alpha\n(Move up and down to reveal more items)\r'):
            with self.subTest(raw=raw):self.assertIsNone(selected_frame(raw,0,console_routes.fixed_menu('Menu',['Alpha','Beta'],20)))
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
        ns['finite_select'](console_routes.fixed_menu('Menu',['Alpha','Beta'],20),'Beta',0,2)
        self.assertEqual(peer.keys,[b'\x1b[B',b'\r'])
        peer=Peer(False);ns['client']=peer
        with self.assertRaises(TimeoutError):ns['finite_select'](console_routes.fixed_menu('Menu',['Alpha','Beta'],20),'Beta',0,2)
        self.assertEqual(peer.keys,[b'\x1b[B'])
    def selection_sequence(self, transitions, target_index=3):
        # Authored exact UI shape, not a copied game capture or ownership proof.
        labels=['💰 Деньги: 500','⚔ ⚔️ Основная рука: Древний меч','📦 Древний меч [Оружие]','📦 🔒 Кладовая материалов совета (1 пр.)','← Назад']
        menu=console_routes.fixed_menu('🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)',labels,20)
        def frame(index):return ('\r\x1b[6A'+menu['header']+'\n'+'\n'.join(('> ' if n==index else '  ')+label for n,label in enumerate(labels))).encode()
        class Peer:
            def __init__(self):self.capture=bytearray(frame(0));self.keys=[];self.pending=[];self.turn=iter(transitions)
            def send(self,key,phase):
                self.keys.append(key)
                if key==b'\x1b[B':self.pending.extend(next(self.turn,[]))
        peer=Peer();peer.frame=frame
        def until(predicate,*unused):
            for _ in range(8):
                if predicate():return
                if peer.pending:
                    chunk=peer.pending.pop(0)
                    peer.capture.extend(chunk if isinstance(chunk,bytes) else b''.join(frame(i) for i in chunk))
            raise TimeoutError('Fixed original poll budget')
        node=next(n for n in ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='finite_select')
        ns={'client':peer,'console_routes':console_routes,'until':until};exec(compile(ast.Module(body=[node],type_ignores=[]),'actual finite selector','exec'),ns)
        return peer,lambda:ns['finite_select'](menu,labels[target_index],0,5)
    def test_duplicate_redraw_waits_for_transition_without_extra_key(self):
        for transitions in ([[[0],[1]],[[1],[2]],[[2],[3]]], [[[0,0,1]],[[1,2]],[[2,2,3]]]):
            with self.subTest(transitions=transitions):
                peer,run=self.selection_sequence(transitions);run()
                self.assertEqual(peer.keys,[b'\x1b[B']*3+[b'\r'])
    def test_repeated_same_selection_times_out_without_extra_key(self):
        peer,run=self.selection_sequence([[[0],[0],[0]]])
        with self.assertRaises(TimeoutError):run()
        self.assertEqual(peer.keys,[b'\x1b[B'])
    def test_skipped_backward_or_hidden_transition_refused(self):
        for transitions in ([[[2]]],[[[1]],[[0]]],[[[1,2,1]]]):
            with self.subTest(transitions=transitions):
                peer,run=self.selection_sequence(transitions)
                with self.assertRaises(RuntimeError):run()
                self.assertNotIn(b'\r',peer.keys)
                self.assertLessEqual(len(peer.keys),2)
    def test_truncated_newest_frame_cannot_accept_old_target(self):
        peer,run=self.selection_sequence([[[1],b'\r\x1b[6Aunfinished']],target_index=1)
        # Both chunks arrive together, so no earlier complete frame can be accepted.
        original_send=peer.send
        def send(key,phase):
            original_send(key,phase)
            if key==b'\x1b[B':peer.pending=[peer.frame(1)+b'\r\x1b[6Aunfinished']
        peer.send=send
        with self.assertRaises(TimeoutError):run()
        self.assertEqual(peer.keys,[b'\x1b[B'])
    def inventory_cleanup_fixture(self, prompt=True):
        source=ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text())
        nodes=[n for n in source.body if isinstance(n,ast.FunctionDef) and n.name in ('finite_select','settle_inventory_cleanup')]
        labels=['💰 Деньги: 500','⚔ ⚔️ Основная рука: Древний меч','📦 Древний меч [Оружие]','📦 🔒 Кладовая материалов совета (1 пр.)','← Назад']
        menu=console_routes.fixed_menu('🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)',labels,20)
        def frame(n):return ('\r\x1b[6A'+menu['header']+'\n'+'\n'.join(('> ' if i==n else '  ')+s for i,s in enumerate(labels))).encode()
        class Peer:
            def __init__(self):self.capture=bytearray(frame(0));self.keys=[];self.pending=[frame(0),frame(1)];self.index=1
            def send(self,key,phase):
                self.keys.append(key)
                if key==b'\x1b[B':self.index+=1;self.pending.append(frame(self.index))
                elif key==b'\r' and prompt:self.pending.append('Ваш ход\n > '.encode())
        peer=Peer();ctx={'menu':menu,'cut':0,'selected':labels[0],'pending_next':labels[1]}
        def until(predicate,*unused):
            for _ in range(10):
                if predicate():return
                if peer.pending:peer.capture.extend(peer.pending.pop(0))
            raise TimeoutError('Fixed cleanup deadline')
        def fresh_player(client,cut,*unused):until(lambda:'Ваш ход'.encode() in bytes(client.capture[cut:]),0,'prompt')
        ns={'client':peer,'console_routes':console_routes,'until':until,'fresh_player':fresh_player,'inventory_context':ctx,'client_prompt':False,'result':{'Failure':'original injected failure'}}
        exec(compile(ast.Module(body=nodes,type_ignores=[]),'actual inventory cleanup','exec'),ns)
        return ns,peer,frame
    def test_cleanup_finishes_pending_duplicate_then_back_without_ctrlc(self):
        ns,peer,_=self.inventory_cleanup_fixture();self.assertTrue(ns['settle_inventory_cleanup']())
        self.assertEqual(peer.keys,[b'\x1b[B']*3+[b'\r']);self.assertTrue(ns['client_prompt'])
        self.assertIsNone(ns['inventory_context']);self.assertEqual(ns['result']['Failure'],'original injected failure')
    def test_cleanup_unknown_or_partial_inventory_never_sends_keys(self):
        for bad in ('missing','partial','foreign'):
            with self.subTest(bad=bad):
                ns,peer,_=self.inventory_cleanup_fixture()
                if bad=='missing':ns['inventory_context']=None
                elif bad=='partial':peer.capture.extend(b'\r\x1b[6Apartial');peer.pending=[]
                else:peer.capture=bytearray(b'Submenu\n> different');peer.pending=[]
                with self.assertRaises((RuntimeError,TimeoutError)):ns['settle_inventory_cleanup']()
                self.assertEqual(peer.keys,[]);self.assertFalse(ns['client_prompt'])
    def test_cleanup_back_enter_without_prompt_is_not_retried(self):
        ns,peer,_=self.inventory_cleanup_fixture(prompt=False)
        with self.assertRaises(TimeoutError):ns['settle_inventory_cleanup']()
        keys=list(peer.keys);self.assertEqual(keys,[b'\x1b[B']*3+[b'\r'])
        with self.assertRaises(RuntimeError):ns['settle_inventory_cleanup']()
        self.assertEqual(peer.keys,keys);self.assertFalse(ns['client_prompt'])
    def test_cleanup_pending_skip_refuses_without_input(self):
        ns,peer,frame=self.inventory_cleanup_fixture();peer.capture=bytearray(frame(3));peer.pending=[]
        with self.assertRaises(RuntimeError):ns['settle_inventory_cleanup']()
        self.assertEqual(peer.keys,[])
    def test_inventory_context_records_pending_before_input_uncertainty(self):
        ns,peer,_=self.inventory_cleanup_fixture();ctx=ns['inventory_context'];ctx['pending_next']=None;peer.pending=[]
        def ambiguous_send(*unused):raise OSError('injected input uncertainty')
        peer.send=ambiguous_send
        with self.assertRaises(OSError):ns['finite_select'](ctx['menu'],ctx['menu']['choices'][3],0,5)
        self.assertEqual(ctx['pending_next'],ctx['menu']['choices'][1]);self.assertEqual(ctx['cut'],len(peer.capture))
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
        self.assertLess(text.index('validated_load_observation'),text.index('original_record =',text.index('validated_load_observation')))
        self.assertIn("observed['observation']['ReplacementFinish']['MainIdentity']",text)
        self.assertNotIn("result['OriginalRunningRecord'] =",text)
        self.assertNotIn("result['AcceptedGameTurns'] =",text)
    def test_actual_load_assignment_and_closing_assert_keep_wire_schemas_distinct(self):
        # Execute the shipped assignments and exact closing assertion, not a
        # duplicate comparison helper. This is DATA after the separate receipt join.
        tree=ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text())
        fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='storage_local_route')
        bind=next(n for n in fn.body if isinstance(n,ast.Assign) and
            any(isinstance(t,ast.Name) and t.id=='original_record' for t in n.targets) and
            not isinstance(n.value,ast.Constant))
        start=fn.body.index(bind)
        assignments=fn.body[start:start+3]
        self.assertTrue(all(isinstance(n,ast.Assign) for n in assignments))
        closing=[n for n in ast.walk(tree) if isinstance(n,ast.Assert) and
            'settled' in {x.id for x in ast.walk(n.test) if isinstance(x,ast.Name)} and
            'original_record' in {x.id for x in ast.walk(n.test) if isinstance(x,ast.Name)}]
        self.assertEqual(len(closing),1)
        durable={'RootKey':'/owned','RunId':'b'*32,'GenerationId':'2'*32,'Epoch':2,
                 'Backend':'LinuxSupervisor','HostInstanceId':'c'*32,'BootId':'boot'}
        observed=dict(durable,Backend=2)
        installed={'Disposition':'Running','Identity':dict(durable)}
        scope={'b':observed,'installed':installed,'settled':{'Disposition':'Stopped','Identity':dict(durable)}}
        exec(compile(ast.Module(body=assignments,type_ignores=[]),'actual Load binding','exec'),scope)
        self.assertEqual(scope['original'],{k[0].lower()+k[1:]:v for k,v in observed.items()})
        check=compile(ast.Module(body=closing,type_ignores=[]),'actual closing assertion','exec')
        exec(check,scope)
        self.assertEqual(scope['original_record'],durable)
        self.assertEqual(installed['Identity']['Backend'],'LinuxSupervisor')
        self.assertEqual(observed['Backend'],2)
        for field,replacement in [('RootKey','/foreign'),('RunId','a'*32),('Backend',2)]:
            scope['settled']={'Disposition':'Stopped','Identity':dict(durable,**{field:replacement})}
            with self.subTest(field=field),self.assertRaises(AssertionError):exec(check,scope)
    def test_save_result_interval_refuses_committed_followup_before_next_local_move(self):
        success='✅ Игра сохранена'
        warning='Требуется служебная проверка хранилища сохранений.'
        self.assertTrue(console_routes.clean_save_result(success+'\nВаш ход\n >  '))
        for text in (success+'\n'+warning+'\nВаш ход\n >  ',warning+'\nВаш ход\n >  ',
                     success+'\n',success+'\n'+success+'\nВаш ход\n >  '):
            with self.subTest(text=text):self.assertFalse(console_routes.clean_save_result(text))
        tree=ast.parse(pathlib.Path(__file__).with_name('live-mortal-phase.py').read_text())
        fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='storage_local_route')
        guards=[n for n in fn.body if isinstance(n,ast.If) and any(isinstance(c,ast.Call) and
            isinstance(c.func,ast.Attribute) and c.func.attr=='clean_save_result' for c in ast.walk(n.test))]
        self.assertEqual(len(guards),1)
        guard=guards[0];index=fn.body.index(guard)
        earlier=ast.unparse(ast.Module(body=fn.body[:index],type_ignores=[]))
        later=ast.unparse(ast.Module(body=fn.body[index+1:],type_ignores=[]))
        self.assertIn('acknowledge actual Save result',earlier)
        self.assertTrue(earlier.rfind('fresh_player')>earlier.index('acknowledge actual Save result'))
        self.assertIn('move(False)',later)
        class Peer:
            def text(self,cut):return success+'\n'+warning+'\nВаш ход\n >  '
        ns={'console_routes':console_routes,'client':Peer(),'save_result_cut':1}
        with self.assertRaises(RuntimeError):exec(compile(ast.Module(body=[guard],type_ignores=[]),'actual Save result refusal','exec'),ns)
    def test_local_route_does_not_accept_story_or_queue_change(self):
        before={'StorySHA256':'a','History':{'present':False,'sha256':None},'QueuePins':{}}
        require_local_unchanged(before,dict(before))
        for field in before:
            after=dict(before);after[field]='changed'
            with self.subTest(field=field),self.assertRaises(ValueError):require_local_unchanged(before,after)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(RouteTests)
    if suite.countTestCases()!=23:raise SystemExit('Expected exactly twenty-three finite-route DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==23 and result.wasSuccessful() and not result.skipped else 1)
