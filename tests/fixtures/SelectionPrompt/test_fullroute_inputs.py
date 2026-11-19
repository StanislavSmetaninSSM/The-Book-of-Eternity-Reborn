"""Bounded DATA controls for the actual full-route input observer. No child/UI."""
import unittest
import types
from unittest.mock import patch
from fullroute_inputs import (OPTIONS, DESCRIPTIONS, OPTIONS_FOOTER, options_frame,
                             ordered_transition, retained_selection, clean_save_interval, require_time, wait_observation, send_current, AcknowledgedTerminalView, options_cells, save_outcome, returned_save_boundary, fresh_route_key, complete_save_observation)
import mortal_console_routes as routes


def options(selected, initial=True):
    text = '\x1b[2J\x1b[3J\x1b[1;1H── ⚙️ Игровое меню ──\r\n\r\nEsc — назад\r\n\r\n' if initial else '\x1b[5;1H'
    for label, description in zip(OPTIONS, DESCRIPTIONS):
        text += '    ' + ('➤ ' if selected == label else '◆ ') + label + '  \r\n'
        if description:
            text += '       ' + description + '\r\n \r\n'
    return (text + '\r\n  ' + OPTIONS_FOOTER).encode()


def padding_case(split=17):
    # Authored exact source-shaped five-row Spectre redraw, not a live-world input.
    # The excluded prefix models the recorded action cut; only this frame is parsed.
    header='🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)'
    choices=['💰 Деньги: 500','⚔ ⚔️ Основная рука: Древний меч','📦 Древний меч [Оружие]','📦 🔒 Запертая кладовая разметочных материалов (1 пр.)','← Назад']
    menu=routes.entry_menu(routes.fixed_menu(header,choices,20),command='/storage_move')
    styled='\r\x1b[6A\x1b[1m🎒 📦 Инвентарь\x1b[0m  \x1b[2m(⚖ 1.8kg/45.0kg кг)\x1b[0m  \x1b[2m(выберите для просмотра / управления)\x1b[0m'
    rows=[' '*75,'  '+choices[0]+' '*59,'> '+choices[1]+' '*43,'  '+choices[2]+' '*50,'  '+choices[3]+' '*19,'  '+choices[4]+' '*66]
    frame=(styled+'\r\n'+'\r\n'.join(rows)).encode();assert len(frame)==718
    raw=b'X'*17001+frame;end=len(raw)-66+split;captured=bytearray(raw[:end])
    view=AcknowledgedTerminalView(None,captured);view.pending=False;view.cut=end;view.seq=57
    view.cursor={'x':9+split,'y':24};view.lines=['']*18+[header]+rows
    view.modes={'applicationCursorKeysMode':True,'applicationKeypadMode':True}
    frames=routes.selected_frames(bytes(captured),17001,menu);assert frames==[choices[1]]
    proof={'raw':bytes(captured),'menu':menu,'selected':choices[1],'cut':17001,'frames':list(frames),
           'ack':{'cut':view.cut,'seq':view.seq,'cursor':dict(view.cursor),'lines':list(view.lines)},'view':view}
    return raw,captured,view,proof


class FullRouteInputData(unittest.TestCase):
    def test_same_window_retained_padding_all_split_points(self):
        for split in range(66):
            with self.subTest(split=split):
                raw,captured,view,proof=padding_case(split)
                if split==17:self.assertEqual((len(proof['raw']),len(raw)),(17670,17719))
                self.assertEqual(retained_selection(bytes(captured),proof),proof['selected'])
                captured[:]=raw;view.cut=len(raw);view.seq=59;view.cursor={'x':75,'y':24}
                self.assertEqual(routes.selected_frames(raw,17001,proof['menu']),proof['frames'])
                self.assertEqual(retained_selection(raw,proof),proof['selected'])

    def test_first_frame_written_footer_padding_completion(self):
        for split in range(66):
            with self.subTest(split=split):
                raw,captured,view,proof=padding_case(split)
                short='  ← Назад'+' '*split
                proof['ack']['lines'][-1]=short;view.lines[-1]=short
                captured[:]=raw;view.cut=len(raw);view.seq=59;view.cursor={'x':75,'y':24}
                view.lines[-1]='  ← Назад'+' '*66
                self.assertEqual(retained_selection(raw,proof),proof['selected'])
        for defect in ('short-before-cursor','extra-after-cursor','changed-prefix','other-row'):
            with self.subTest(defect=defect):
                raw,captured,view,proof=padding_case()
                proof['ack']['lines'][-1]='  ← Назад'+' '*17
                captured[:]=raw;view.cut=len(raw);view.seq=59;view.cursor={'x':75,'y':24}
                if defect=='short-before-cursor':proof['ack']['lines'][-1]=proof['ack']['lines'][-1][:-1]
                if defect=='extra-after-cursor':view.lines[-1]+=' '
                if defect=='changed-prefix':view.lines[-1]='X'+view.lines[-1][1:]
                if defect=='other-row':view.lines[0]=' '
                self.assertIsNone(retained_selection(raw,proof))

    def test_actual_client_final_drain_padding_sends_one_current_down(self):
        import sys,pathlib
        sys.path.insert(0,str(pathlib.Path(__file__).with_name('terminal')))
        from client_session import ClientSession
        raw,captured,view,proof=padding_case();sent=[];session=ClientSession.__new__(ClientSession)
        session.capture=captured;session.view=view
        session.terminal=types.SimpleNamespace(_time=lambda:None,key=lambda data,label,**kw:sent.append(data))
        def final_drain():
            captured[:]=raw;view.cut=len(raw);view.seq=59;view.cursor={'x':75,'y':24}
        session.drain=final_drain;failure=None;cut=None
        try:cut=session.send(lambda:view.arrow(),'fixed menu observed next row',lambda:retained_selection(bytes(captured),proof)==proof['selected'])
        except ValueError as error:failure=str(error)
        self.assertIsNone(failure,'Actual final-drain padding must not be mistaken for a newer frame')
        self.assertEqual(sent,[b'\x1bOB']);self.assertEqual(cut,17719)

    def test_retained_padding_refuses_new_partial_control_or_changed_selection(self):
        raw,captured,view,proof=padding_case()
        invalid=[b'\r',b'\b',b'\x1b[2K ',b'\x1b[1C ',b'\r\x1b[6A'+b'partial',b' '*100]
        for tail in invalid:
            with self.subTest(tail=tail):
                captured[:]=proof['raw']+tail;view.cut=len(captured);view.seq=59;view.cursor={'x':75,'y':24}
                try:value=retained_selection(bytes(captured),proof)
                except ValueError:value=None
                self.assertIsNone(value)
        for defect in ('pending','cut','seq','cursor-x','cursor-y','cells'):
            with self.subTest(defect=defect):
                raw,captured,view,proof=padding_case();captured[:]=raw;view.cut=len(raw);view.seq=59;view.cursor={'x':75,'y':24}
                if defect=='pending':view.pending=True
                if defect=='cut':view.cut-=1
                if defect=='seq':view.seq=proof['ack']['seq']
                if defect=='cursor-x':view.cursor['x']=74
                if defect=='cursor-y':view.cursor['y']=23
                if defect=='cells':view.lines[21]='> changed selected row'
                try:value=retained_selection(bytes(captured),proof)
                except ValueError:value=None
                self.assertIsNone(value)
        raw,captured,view,proof=padding_case();view.seq=59
        frame=raw[17001:];changed=frame.replace(('> '+proof['selected']).encode(),('  '+proof['selected']).encode()).replace('  📦 Древний меч [Оружие]'.encode(),'> 📦 Древний меч [Оружие]'.encode())
        for tail in (b' '*49+frame[:80],b' '*49+changed):
            captured[:]=proof['raw']+tail;view.cut=len(captured)
            try:value=retained_selection(bytes(captured),proof)
            except ValueError:value=None
            self.assertIsNone(value)
        with self.assertRaises(ValueError):retained_selection(b'changed-prefix'+raw,proof)

    def test_coalesced_transition_retains_current_without_replaying_history(self):
        self.assertTrue(ordered_transition(['archive', 'archive', 'Back'], 'archive', 'Back'))
        menu = routes.fixed_menu('Load', ['archive', 'Back'], 15)
        raw = b'Load\n> archive\n  Back\r\x1b[3ALoad\n  archive\n> Back'
        proof = {'raw': raw, 'menu': menu, 'selected': 'Back'}
        self.assertEqual('Back', retained_selection(raw, proof))
        self.assertIsNone(retained_selection(raw + b'\r\x1b[3ALoad\n> ar', proof))
        with self.assertRaises(ValueError):
            retained_selection(raw + b'\r\x1b[3ALoad\n> archive\n  Back', proof)

    def test_transition_refuses_reverse_and_skip(self):
        for frames in (['Back', 'archive'], ['third']):
            with self.assertRaises(ValueError):
                ordered_transition(frames, 'archive', 'Back')
        self.assertFalse(ordered_transition(['archive'], 'archive', 'Back'))

    def test_complete_initial_and_fresh_options_grid(self):
        self.assertTrue(options_frame(options(OPTIONS[0]), 0, OPTIONS[0], initial=True))
        old = options(OPTIONS[0])
        self.assertTrue(options_frame(old + options(OPTIONS[1], False), len(old), OPTIONS[1], initial=False))

    def test_options_refuses_missing_reordered_title_description_footer(self):
        original = options(OPTIONS[0])
        # RenderGenericMenuStaticFrame emits WriteLine after the Esc MarkupLine.
        self.assertFalse(options_frame(original.replace(b'\r\n\r\n    ', b'\r\n    ', 1), 0, OPTIONS[0], initial=True))
        for text in ('⚙️ Игровое меню', 'Esc — назад', DESCRIPTIONS[0], OPTIONS_FOOTER):
            self.assertFalse(options_frame(original.replace(text.encode(), b''), 0, OPTIONS[0], initial=True))
        changed = original.replace(OPTIONS[1].encode(), b'TEMP').replace(OPTIONS[2].encode(), OPTIONS[1].encode()).replace(b'TEMP', OPTIONS[2].encode())
        self.assertFalse(options_frame(changed, 0, OPTIONS[0], initial=True))

    def test_options_refuses_stale_partial_and_late_controls(self):
        old = options(OPTIONS[0])
        for tail in (b' ', options(OPTIONS[0], False)[:-10], options(OPTIONS[0], False) + b'\x1b[2K'):
            self.assertFalse(options_frame(old + tail, len(old), OPTIONS[0], initial=False))

    def test_save_warning_is_checked_through_returned_stream_boundary(self):
        success = '✅ Игра сохранена\n'
        self.assertTrue(clean_save_interval(success + 'inventory'))
        warning = 'Требуется служебная проверка хранилища сохранений.'
        for split in range(len(warning) + 1):
            collected = success + warning[:split] + warning[split:] + '\ninventory'
            self.assertFalse(clean_save_interval(collected))
        self.assertFalse(clean_save_interval(success * 2))

    def test_options_requires_fresh_whole_raw_and_exact_acknowledged_cells(self):
        raw=bytearray(options(OPTIONS[0]));view=AcknowledgedTerminalView(None,raw)
        grid=['── ⚙️ Игровое меню ──','Esc — назад']
        for label,description in zip(OPTIONS,DESCRIPTIONS):
            grid.append(('➤ ' if label==OPTIONS[0] else '◆ ')+label)
            if description:grid.append(description)
        grid.append(OPTIONS_FOOTER);view.lines=grid+['']*(25-len(grid));view.pending=False;view.cut=len(raw)
        self.assertTrue(view.options(0,OPTIONS[0],True))
        cut=len(raw)
        for tail in (b'\x1b[5;1H',b'\x1b[0m',options(OPTIONS[0],False)[:-8]):
            raw[:]=options(OPTIONS[0])+tail;view.cut=len(raw)
            self.assertFalse(view.options(cut,OPTIONS[0],False))
        raw[:]=options(OPTIONS[0])+options(OPTIONS[0],False);view.cut=len(raw)
        self.assertTrue(view.options(cut,OPTIONS[0],False))
        # Complete OLD Save redraw arriving during final drain is before the
        # returned physical action cut, never evidence for numeric1's new draw.
        terminal=types.SimpleNamespace(key=lambda *args:None)
        def drain_old():
            raw.extend(options(OPTIONS[0],False));view.cut=len(raw)
        resolved=fresh_route_key(terminal,view,drain_old,b'1','numeric1',lambda:view.options(0,OPTIONS[0],True),float('inf'))
        action_cut=len(raw) # Actual run assigns cut only AFTER send returns.
        self.assertEqual(resolved,b'1');self.assertEqual(action_cut,len(raw))
        self.assertFalse(view.options(action_cut,OPTIONS[0],False))
        view.lines[2]='◆ '+OPTIONS[0]
        self.assertFalse(view.options(cut,OPTIONS[0],False))
        view.pending=True
        self.assertFalse(view.options(cut,OPTIONS[0],False))

    def test_arrow_intent_is_resolved_after_mode_changing_drain(self):
        for up in (False,True):
            for mode in (False,True):
                captured=bytearray(b'complete')
                view=AcknowledgedTerminalView(None,captured);view.pending=False;view.cut=len(captured)
                view.modes={'applicationCursorKeysMode':not mode};sent=[]
                terminal=types.SimpleNamespace(key=lambda data,label:sent.append((data,label)))
                def drain():
                    captured.extend(b'newmode');view.cut=len(captured);view.modes['applicationCursorKeysMode']=mode
                fresh_route_key(terminal,view,drain,lambda:view.arrow(up),'arrow',lambda:True,float('inf'))
                expected=(b'\x1bO' if mode else b'\x1b[')+(b'A' if up else b'B')
                self.assertEqual(sent,[(expected,'arrow')])

    def test_final_drain_partial_or_changed_ui_never_sends(self):
        for pending,predicate in ((True,True),(False,False)):
            view=AcknowledgedTerminalView(None,bytearray(b'old'));view.pending=False;view.cut=3
            sent=[];terminal=types.SimpleNamespace(key=lambda *args:sent.append(args))
            def drain():
                view.captured.extend(b'new');view.cut=len(view.captured);view.pending=pending
            with self.assertRaises(ValueError):
                fresh_route_key(terminal,view,drain,b'x','forbidden',lambda:predicate,float('inf'))
            self.assertEqual(sent,[])

    def test_save_exact_outcome_and_returned_inventory_reject_interleaving(self):
        success='✅ Игра сохранена\r\n'.encode()
        self.assertTrue(save_outcome(b'\r\n'+success))
        # Exercise the real wait predicate at EVERY raw read split, including the
        # formerly premature visible-text-before-CRLF boundary.
        for split in range(len(success)+1):
            collected=bytearray(success[:split]);pumps=[]
            view=types.SimpleNamespace(prompt=lambda text,cut:text.encode() in collected[cut:])
            def pump():pumps.append(True);collected.extend(success[split:])
            observed=wait_observation(lambda:complete_save_observation(view,bytes(collected),0),pump,float('inf'),'split success')
            self.assertTrue(observed)
            self.assertEqual(len(pumps),0 if split==len(success) else 1)

        menu=routes.fixed_menu('Inventory',['chalk','Back'],20)
        inventory=b'Inventory\n> chalk\n  Back'
        self.assertEqual(returned_save_boundary(success+inventory,0,menu),len(success))
        header='🎒 📦 Инвентарь  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)'
        real_menu=routes.fixed_menu(header,['chalk','Back'],20)
        title,weight,help_text=header.split('  ')
        real_entry=(b'\x1b[?25l\x1b[1m'+title.encode()+b'\x1b[0m  \x1b[2m'+weight.encode()+b'\x1b[0m  \x1b[2m'+help_text.encode()+b'\x1b[0m\r\n> chalk\r\n  Back')
        self.assertEqual(returned_save_boundary(success+real_entry,0,real_menu),len(success))
        for extra in (title.encode()+b'\r\nwarning\r\n',b'unknown\r\n','Требуется служебная проверка хранилища сохранений.\r\n'.encode(),b'\x1b[1D'):
            self.assertIsNone(returned_save_boundary(success+extra+real_entry,0,real_menu))

        warning='Требуется служебная проверка хранилища сохранений.'
        for text in (warning,warning[:8]+'\x1b[0m'+warning[8:],warning[:8]+'\x1b[1D'+warning[8:],'unknown outcome'):
            raw=success+text.encode()+b'\r\n'
            self.assertFalse(save_outcome(raw))
            self.assertIsNone(returned_save_boundary(raw+inventory,0,menu))
        self.assertFalse(save_outcome(success*2))
        self.assertFalse(save_outcome(success.replace('сохранена'.encode(),b'\x1b[0m'+'сохранена'.encode())))
        self.assertIsNone(returned_save_boundary(success+inventory[:-3],0,menu))

    def test_deadline_equality_and_late_observation_refuse(self):
        require_time(45, 44.999)
        for now in (45, 45.001, 60):
            with self.assertRaises(TimeoutError):
                require_time(45, now)

    def test_real_wait_does_not_accept_expired_or_slow_positive(self):
        calls = []
        with patch('fullroute_inputs.time.monotonic', return_value=45):
            with self.assertRaises(TimeoutError):
                wait_observation(lambda: calls.append('predicate') or True, lambda: None, 45, 'test')
        self.assertEqual([], calls)
        with patch('fullroute_inputs.time.monotonic', side_effect=[44, 45]):
            with self.assertRaises(TimeoutError):
                wait_observation(lambda: True, lambda: None, 45, 'test')

    def test_real_send_never_calls_writer_after_deadline(self):
        calls = []
        with patch('fullroute_inputs.time.monotonic', return_value=45):
            with self.assertRaises(TimeoutError):
                send_current(lambda *args: calls.append(args), b'\r', 'Enter', 45)
        self.assertEqual([], calls)



if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(FullRouteInputData)
    assert suite.countTestCases() == 17
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    assert result.testsRun == 17 and not result.skipped
    raise SystemExit(0 if result.wasSuccessful() else 1)
