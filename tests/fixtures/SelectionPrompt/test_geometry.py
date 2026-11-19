"""T034 inert descriptor controls using the maintained raw-frame grammar."""
import unittest
from smoke import inert_geometry_selected, inert_initial_pty_cut, inert_wrapped_load
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

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(GeometryTests)
    assert suite.countTestCases()==5
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==5 and not result.skipped else 1)
