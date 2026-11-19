"""T034 inert descriptor controls using the maintained raw-frame grammar."""
import unittest
from smoke import inert_geometry_selected, inert_initial_pty_cut, inert_wrapped_load, noecho_baseline, require_noecho, measured_load_profile
ROWS=('s | Ход 43 | Хозяйственный двор у нижней лестницы | 18.11.2026 18:00 | 99999KB','↩️ Назад')
MENU={'header':'Выберите сохранение:','choices':ROWS,'page_size':15}
HEADER='Выберите сохранение:\n'
class GeometryTests(unittest.TestCase):
    def test_padded_complete_rows(self):
        for selected in (0,1):
            body='  \n\n'+'\n'.join(('> ' if i==selected else '  ')+row+'   ' for i,row in enumerate(ROWS))
            self.assertEqual(inert_geometry_selected((HEADER+body).encode(),0,MENU),ROWS[selected])
    def test_wrapped_continuation_is_not_joined(self):
        body='> '+ROWS[0][:65]+'\n  '+ROWS[0][65:]+'\n  '+ROWS[1]
        self.assertIsNone(inert_geometry_selected((HEADER+body).encode(),0,MENU))
    def test_partial_or_control_tail_never_falls_back(self):
        good=(HEADER+'> '+ROWS[0]+'\n  '+ROWS[1]).encode()
        for tail in (b'\n',b'\x1b[2J',b'\x1b[1A',b'\x1b[',b'\n'+HEADER.encode()+b'> s'):
            self.assertIsNone(inert_geometry_selected(good+tail,0,MENU))

    def test_initial_prefix_exact_and_keyboard_bound(self):
        prefix=b'\x1b[?1h\x1b='
        self.assertEqual(inert_initial_pty_cut(prefix+b'frame',{'cursor':True,'keypad':True}),len(prefix))
        for raw in (prefix[:-1],b'x'+prefix,b'\x1b[?1l\x1b=',b'\x1b[?1049h'+prefix):
            self.assertIsNone(inert_initial_pty_cut(raw,{'cursor':True,'keypad':True}))
        self.assertIsNone(inert_initial_pty_cut(prefix,{'cursor':False,'keypad':True}))
    def test_wrapped_raw_exact_and_controls_refused(self):
        menu=dict(MENU,previous_height=3,load_clear=True)
        prefix='\r'+('\x1b[2K\x1b[1A'*3)+'\x1b[2K\x1b[?25h\x1b[2J\x1b[3J\x1b[1;1H── 📂 Загрузка игры ──\r\n\r\n\x1b[?25l'
        body=HEADER.replace('\n','\r\n')+'  \r\n> '+ROWS[0][:-7]+'  \r\n99999KB  \r\n  '+ROWS[1]+'  '
        good=(prefix+body).encode()
        self.assertTrue(inert_wrapped_load(good,0,menu))
        self.assertTrue(inert_wrapped_load(good+('\r\x1b[4A'+body).encode(),0,menu))
        for tail in (b'\x1b[2J',b'\x1b[1A',b'\x1b[',b'\r\x1b[4A'+HEADER.encode()):
            self.assertFalse(inert_wrapped_load(good+tail,0,menu))
        self.assertFalse(inert_wrapped_load(good.replace(b'99999KB',b'other'),0,menu))

    def test_noecho_preserves_original_and_all_other_attributes(self):
        import termios,copy
        original=[1,2,3,termios.ECHO|termios.ECHONL|termios.ICANON,4,5,[b'x',b'y',7]]
        saved=copy.deepcopy(original);configured=noecho_baseline(original)
        self.assertEqual(original,saved)
        self.assertEqual(configured[:3]+configured[4:],original[:3]+original[4:])
        self.assertEqual(configured[3],original[3]&~(termios.ECHO|termios.ECHONL))
        configured[6][0]=b'z';self.assertEqual(original,saved)
    def test_reenabled_echo_refuses_without_mutation(self):
        import termios,copy
        good=[1,2,3,termios.ICANON,4,5,[b'x']]
        require_noecho(good)
        for flag in (termios.ECHO,termios.ECHONL,termios.ECHO|termios.ECHONL):
            bad=copy.deepcopy(good);bad[3]|=flag;saved=copy.deepcopy(bad)
            with self.assertRaisesRegex(ValueError,'input refused'):require_noecho(bad)
            self.assertEqual(bad,saved)

    def test_closed_profile_boundaries_and_default(self):
        import mortal_console_routes as routes
        name='Хозяйственный двор у нижней лестницы'
        with self.assertRaises(ValueError):routes.local_load_menu(name,43,99999)
        self.assertTrue(routes.local_load_menu(name,43,123)) # exact76 default
        self.assertTrue(routes.local_load_menu(name,43,99999,profile=routes.OWNED_PTY_LOAD_PROFILE))
        with self.assertRaises(ValueError):routes.local_load_menu(name+'X',43,99999,profile=routes.OWNED_PTY_LOAD_PROFILE)
        with self.assertRaises(ValueError):routes.local_load_menu(name,43,99999,profile='unknown')
    def test_profile_revalidated_after_transition(self):
        import mortal_console_routes as routes
        menu=routes.local_load_menu('Хозяйственный двор у нижней лестницы',43,99999,profile=routes.OWNED_PTY_LOAD_PROFILE)
        menu=routes.entry_menu(routes.after_menu(menu,menu),load_clear=True)
        raw=(HEADER+'> '+ROWS[0]+'\n  '+ROWS[1]).encode()
        self.assertEqual(routes.selected_frame(raw,0,menu),ROWS[0])
        for bad in (dict(menu,profile='unknown'),{k:v for k,v in menu.items() if k!='profile'}):
            self.assertIsNone(routes.selected_frame(raw,0,bad))
    def test_profile_cannot_expand_other_menu(self):
        import mortal_console_routes as routes
        with self.assertRaises(ValueError):routes.fixed_menu('Inventory',['X'*78,'Back'],20,routes.OWNED_PTY_LOAD_PROFILE)
    def test_measurement_requires_exact_geometry_noecho_all_stdio(self):
        measured=dict(ConsoleWidth=100,SpectreWidth=100,ConsoleHeight=25,SpectreHeight=25,InputRedirected=False,OutputRedirected=False,ErrorRedirected=False)
        attrs=noecho_baseline([1,2,3,0,4,5,[]])
        self.assertTrue(measured_load_profile(measured,[25,100,0,0],attrs))
        for key,value in [('ConsoleWidth',80),('SpectreWidth',80),('OutputRedirected',True),('InputRedirected',True),('ErrorRedirected',True),('ConsoleHeight',24)]:
            with self.assertRaises(ValueError):measured_load_profile(dict(measured,**{key:value}),[25,100,0,0],attrs)
        with self.assertRaises(ValueError):measured_load_profile(measured,[25,80,0,0],attrs)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(GeometryTests)
    assert suite.countTestCases()==11
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==11 and not result.skipped else 1)
